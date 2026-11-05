using MainCore.Commands.Features.ClaimVillageQuest;
using MainCore.Commands.Features.StartAdventure;
using MainCore.Commands.Navigate;
using MainCore.Infrasturecture.Extensions;
using MainCore.Tasks.Base;

namespace MainCore.Tasks
{
    /// <summary>
    /// "Poner a levear": levels the hero up without letting him die.
    /// Loop: hero home? -> read HP -> adventure (HP >= 50) / collect tasks (HP &lt; 50) /
    /// wait for healing / hunt animals in the "Oasis" cow list after a clean simulation.
    /// Every doubt means "do not send". See docs/ARCHITECTURE.md (Hero leveling).
    /// </summary>
    [Handler]
    public sealed partial class HeroLevelingTask(
        IDbContextFactory<AppDbContext> contextFactory,
        IChromeBrowser browser,
        ILogger logger,
        OpenAdventurePanelCommand.Handler openAdventurePanelCommand,
        ExploreAdeventureCommand.Handler exploreAdeventureCommand,
        OpenVillageQuestPageCommand.Handler openVillageQuestPageCommand,
        SwitchTabCommand.Handler switchTabCommand)
    {
        public sealed class Task(AccountId accountId) : AccountTask(accountId)
        {
            protected override string TaskName => "Hero leveling";

            /// <summary>Estimated return of the hero after a raid we sent.</summary>
            public DateTime? ExpectedReturnAt { get; set; }

            public override bool CanStart(AppDbContext context)
            {
                return HeroLevelingStore.IsEnabled(context, AccountId);
            }
        }

        private const int MaxTaskCollects = 15;
        private const int MaxOasisChecks = 20;
        private static readonly TimeSpan NotHomeRetry = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan UnknownRetry = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan NoSafeOasisRetry = TimeSpan.FromMinutes(30);

        private readonly record struct OasisTarget(int Id, string Url);

        private async ValueTask<Result> HandleAsync(Task task, CancellationToken cancellationToken)
        {
            var accountId = task.AccountId;
            if (!IsEnabled(accountId)) return Result.Ok();

            var server = GetServer(accountId);
            if (server is null) return Stop.Error.WithError("Account server not found");

            var targets = GetOasisTargets(accountId, out var listMessage);
            if (targets is null)
            {
                // Fail closed: without the user's own list we never pick targets.
                Disable(accountId, listMessage);
                logger.Warning("Hero leveling stopped: {Message}", listMessage);
                return Result.Ok();
            }

            // Two passes at most: collecting tasks can level the hero up and restore HP.
            for (var pass = 0; pass < 2; pass++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!await HeroParser.IsHeroHome(browser.CurrentPage))
                {
                    var at = task.ExpectedReturnAt is DateTime expected && expected > DateTime.Now
                        ? expected.AddMinutes(1)
                        : DateTime.Now.Add(NotHomeRetry);
                    Reschedule(task, at, "Esperando", $"El héroe no está en la aldea. Reviso de nuevo a las {at:HH:mm}.");
                    return Result.Ok();
                }

                var statsResult = await ReadHeroStats();
                if (statsResult.IsFailed) return Result.Fail(statsResult.Errors);
                var stats = statsResult.Value;
                SaveStats(accountId, stats);

                // Re-check home on the attributes page (the hero could have left meanwhile).
                var heroHome = await HeroParser.IsHeroHome(browser.CurrentPage);
                var adventureAvailable = heroHome && await AdventureParser.CanStartAdventure(browser.CurrentPage);
                var tasksClaimable = await QuestParser.IsQuestClaimable(browser.CurrentPage);

                var action = HeroLevelingRules.Decide(heroHome, stats.HpPercent, adventureAvailable, tasksClaimable);
                logger.Information("Hero leveling: HP {Hp}%, adventure {Adventure}, tasks {Tasks} -> {Action}", stats.HpPercent, adventureAvailable, tasksClaimable, action);

                switch (action)
                {
                    case HeroLevelingAction.WaitForReturn:
                        Reschedule(task, DateTime.Now.Add(NotHomeRetry), "Esperando", "El héroe no está en la aldea.");
                        return Result.Ok();

                    case HeroLevelingAction.UnknownHealth:
                        Reschedule(task, DateTime.Now.Add(UnknownRetry), "Esperando", "No pude leer la vida del héroe. No lo mando a ningún lado y reviso en 10 minutos.");
                        return Result.Ok();

                    case HeroLevelingAction.Adventure:
                        return await StartAdventure(task, stats, cancellationToken);

                    case HeroLevelingAction.CollectTasks:
                        {
                            var collected = await CollectTasksUntilHealthy(cancellationToken);
                            if (collected.IsFailed) return Result.Fail(collected.Errors);
                            if (collected.Value) continue; // HP is back: run the loop again.

                            WaitForHealing(task, stats, "Cobré las tareas pero el héroe sigue con poca vida.");
                            return Result.Ok();
                        }

                    case HeroLevelingAction.WaitForHealing:
                        WaitForHealing(task, stats, "El héroe tiene poca vida y no hay tareas para cobrar.");
                        return Result.Ok();

                    case HeroLevelingAction.HuntOasis:
                        return await Hunt(task, server, targets, stats, cancellationToken);
                }
            }

            Reschedule(task, DateTime.Now.Add(UnknownRetry), "Esperando", "Reviso de nuevo en 10 minutos.");
            return Result.Ok();
        }

        #region hero

        private async Task<Result<HeroStats>> ReadHeroStats()
        {
            var server = new Uri(browser.CurrentUrl);
            var result = await browser.Navigate($"{server.Scheme}://{server.Host}{HeroParser.AttributesPath}");
            if (result.IsFailed) return Result.Fail(result.Errors);

            result = await browser.Wait(HeroParser.GetAttributesBox(browser.CurrentPage));
            if (result.IsFailed) return Result.Fail(result.Errors);

            // The attributes page is rendered by React; give the values a moment.
            await System.Threading.Tasks.Task.Delay(800);
            return await HeroParser.ReadStats(browser.CurrentPage);
        }

        private async Task<Result> StartAdventure(Task task, HeroStats stats, CancellationToken cancellationToken)
        {
            if (!HeroLevelingRules.CanGoOnAdventure(stats.HpPercent)) return Result.Ok();

            var result = await openAdventurePanelCommand.HandleAsync(new(browser), cancellationToken);
            if (result.IsFailed) return result;

            result = await exploreAdeventureCommand.HandleAsync(new(browser), cancellationToken);
            if (result.IsFailed)
            {
                if (result.HasError<Skip>())
                {
                    Reschedule(task, DateTime.Now.AddMinutes(1), "Esperando", "La aventura ya no estaba disponible.");
                    return Result.Ok();
                }
                return result;
            }

            var duration = await AdventureParser.GetAdventureDuration(browser.CurrentPage);
            if (duration <= TimeSpan.Zero) duration = NotHomeRetry;
            var at = DateTime.Now.Add(duration * 2).AddMinutes(1);
            task.ExpectedReturnAt = at;
            Reschedule(task, at, "Aventura", $"El héroe salió de aventura con {stats.HpPercent}% de vida. Vuelve cerca de las {at:HH:mm}.");
            return Result.Ok();
        }

        private async Task<Result<bool>> CollectTasksUntilHealthy(CancellationToken cancellationToken)
        {
            for (var i = 0; i < MaxTaskCollects; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var collected = await CollectOneTask(cancellationToken);
                if (collected.IsFailed) return collected;
                if (!collected.Value) return false;

                var stats = await ReadHeroStats();
                if (stats.IsFailed) return Result.Fail(stats.Errors);
                SaveStats(browser.AccountId, stats.Value);
                logger.Information("Collected a task reward, hero HP is now {Hp}%", stats.Value.HpPercent);

                // Stop as soon as HP is back: the rest of the tasks stay as XP reserve.
                if (stats.Value.HpPercent is int hp && hp >= HeroLevelingRules.MinHpPercent) return true;
                if (!await QuestParser.IsQuestClaimable(browser.CurrentPage)) return false;
            }

            return false;
        }

        private async Task<Result<bool>> CollectOneTask(CancellationToken cancellationToken)
        {
            var result = await openVillageQuestPageCommand.HandleAsync(new(browser), cancellationToken);
            if (result.IsFailed) return Result.Fail(result.Errors);

            var button = QuestParser.GetQuestCollectButton(browser.CurrentPage);
            if (await button.CountAsync() == 0)
            {
                result = await switchTabCommand.HandleAsync(new(1), cancellationToken);
                if (result.IsFailed) return Result.Fail(result.Errors);
                button = QuestParser.GetQuestCollectButton(browser.CurrentPage);
            }

            if (await button.CountAsync() == 0) return false;

            result = await browser.Click(button.First);
            if (result.IsFailed) return Result.Fail(result.Errors);

            await System.Threading.Tasks.Task.Delay(1500, cancellationToken);
            return true;
        }

        private void WaitForHealing(Task task, HeroStats stats, string reason)
        {
            var at = DateTime.Now.AddMinutes(Random.Shared.Next(30, 61));
            Reschedule(task, at, "Curándose", $"{reason} Vida: {stats.HpPercent}%. Espero que se cure y reviso a las {at:HH:mm}.");
        }

        #endregion hero

        #region hunt

        private async Task<Result> Hunt(Task task, string server, List<OasisTarget> targets, HeroStats stats, CancellationToken cancellationToken)
        {
            if (!HeroLevelingRules.CanHunt(stats.HpPercent)) return Result.Ok();

            var heroVillageId = await HeroParser.GetHeroVillageId(browser.CurrentPage);
            var origin = heroVillageId is int id ? GetVillageCoordinates(task.AccountId, id) : null;
            if (heroVillageId is null || origin is null)
            {
                Reschedule(task, DateTime.Now.Add(UnknownRetry), "Esperando", "No pude saber la aldea del héroe. No lo mando y reviso en 10 minutos.");
                return Result.Ok();
            }

            var ordered = HeroLevelingRules.OrderCandidates(targets.Select(x => (x.Id, x.Url)), origin.Value.X, origin.Value.Y);
            // Continue where the last search stopped: the closer oases were already found too strong
            // (or empty) for a hero of this level and HP. A stronger hero starts again from the closest.
            var startId = GetSearchStart(task.AccountId, stats);
            var candidates = HeroLevelingRules.StartFrom(ordered, startId);
            if (startId is int start && candidates.Count > 0 && candidates[0].Id == start)
                logger.Information("Hero leveling continues the search at ({X}|{Y})", candidates[0].X, candidates[0].Y);

            var checkedCount = 0;
            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (checkedCount >= MaxOasisChecks)
                {
                    // Too many checks for one run: the next run continues with this oasis.
                    SaveSearch(task.AccountId, candidate.Id, stats);
                    var later = DateTime.Now.Add(UnknownRetry);
                    Reschedule(task, later, "Buscando", $"Revisé {MaxOasisChecks} oasis sin encontrar uno seguro. Sigo desde ({candidate.X}|{candidate.Y}) a las {later:HH:mm}.");
                    return Result.Ok();
                }
                checkedCount++;

                // Save the position first, so an error in the middle resumes at this oasis.
                SaveSearch(task.AccountId, candidate.Id, stats);

                var check = await CheckOasis(server, heroVillageId.Value, candidate, stats);
                if (check.IsFailed) return Result.Fail(check.Errors);
                var (verdict, kid) = check.Value;
                if (!verdict.Send || kid is null)
                {
                    logger.Information("Hero leveling skips ({X}|{Y}): {Reason}", candidate.X, candidate.Y, verdict.Reason);
                    continue;
                }

                logger.Information("Hero leveling: ({X}|{Y}) is safe: {Reason}", candidate.X, candidate.Y, verdict.Reason);

                // The simulator page we just used shows the animals of right now: send immediately.
                var sent = await SendHero(server, heroVillageId.Value, kid.Value, candidate);
                if (sent.IsFailed) return Result.Fail(sent.Errors);
                if (!sent.Value)
                {
                    logger.Information("Hero leveling did not send to ({X}|{Y}): the rally point did not look right", candidate.X, candidate.Y);
                    continue;
                }

                // This oasis is being cleared: next time start with the one after it.
                SaveSearch(task.AccountId, HeroLevelingRules.NextAfter(ordered, candidate.Id), stats);

                var roundTrip = HeroLevelingRules.RoundTrip(candidate.Distance, stats.SpeedFieldsPerHour);
                var back = roundTrip is TimeSpan trip ? DateTime.Now.Add(trip).AddMinutes(1) : DateTime.Now.Add(NotHomeRetry);
                task.ExpectedReturnAt = back;
                Reschedule(task, back, "Cazando", $"El héroe fue a cazar al oasis ({candidate.X}|{candidate.Y}). {verdict.Reason}. Vuelve cerca de las {back:HH:mm}.");
                return Result.Ok();
            }

            // A full lap without a safe oasis: animals respawn and the hero heals, so start from the closest again.
            SaveSearch(task.AccountId, null, stats);
            var at = DateTime.Now.Add(NoSafeOasisRetry);
            Reschedule(task, at, "Esperando", $"Ningún oasis de la lista es seguro ahora (o no tienen animales). Reviso a las {at:HH:mm}.");
            return Result.Ok();
        }

        private async Task<Result<(SimulationVerdict Verdict, int? Kid)>> CheckOasis(string server, int heroVillageId, OasisCandidate candidate, HeroStats stats)
        {
            static (SimulationVerdict, int?) Skip(string reason) => (new SimulationVerdict(false, reason), null);

            var result = await browser.Navigate(candidate.Url);
            if (result.IsFailed) return Result.Fail(result.Errors);

            var tile = await CowOasisParser.ReadTile(browser.CurrentPage);
            if (!tile.IsOasis) return Skip("No es un oasis");
            if (!tile.IsUnoccupied) return Skip("Oasis ocupado");

            var animals = await CowOasisParser.ReadAnimalCount(browser.CurrentPage);
            if (animals is null) return Skip("No pude leer los animales");
            if (animals.Value <= 0) return Skip("Sin animales");

            var link = CowOasisParser.GetSimulatorLink(browser.CurrentPage);
            var kid = await link.CountAsync() > 0 ? CombatSimulatorParser.ParseKid(await link.GetAttributeAsync("href")) : null;
            if (kid is null) return Skip("No encontré el simulador del oasis");

            var verdict = await Simulate(server, heroVillageId, kid.Value, stats, animals.Value);
            if (verdict.IsFailed) return Result.Fail(verdict.Errors);
            return (verdict.Value, kid);
        }

        private async Task<Result<SimulationVerdict>> Simulate(string server, int heroVillageId, int kid, HeroStats stats, int animals)
        {
            var page = browser.CurrentPage;
            var result = await browser.Navigate(CombatSimulatorParser.BuildUrl(server, heroVillageId, kid));
            if (result.IsFailed) return Result.Fail(result.Errors);

            result = await browser.Wait(CombatSimulatorParser.GetSimulateButton(page));
            if (result.IsFailed) return Result.Fail(result.Errors);

            var select = CombatSimulatorParser.GetVillageSelect(page);
            if (await select.CountAsync() != 1 || await select.InputValueAsync() != heroVillageId.ToString())
                return new SimulationVerdict(false, "El simulador no está en la aldea del héroe");

            var raid = CombatSimulatorParser.GetRaidRadio(page);
            if (await raid.CountAsync() != 1) return new SimulationVerdict(false, "No encontré la opción de atraco");
            if (!await raid.IsCheckedAsync())
            {
                result = await browser.Click(raid);
                if (result.IsFailed) return Result.Fail(result.Errors);
            }

            // Attacker = only the hero: the simulator prefills the village troops, clear them.
            var units = CombatSimulatorParser.GetAttackerUnitInputs(page);
            var unitCount = await units.CountAsync();
            if (unitCount != 10) return new SimulationVerdict(false, "El simulador no tiene las 10 tropas del atacante");
            for (var i = 0; i < unitCount; i++)
            {
                result = await browser.Input(units.Nth(i), "0");
                if (result.IsFailed) return Result.Fail(result.Errors);
            }

            var hero = CombatSimulatorParser.GetAttackerHeroCheckbox(page);
            if (await hero.CountAsync() != 1) return new SimulationVerdict(false, "No encontré al héroe en el simulador");
            if (!await hero.IsCheckedAsync())
            {
                result = await browser.Click(hero);
                if (result.IsFailed) return Result.Fail(result.Errors);
            }
            if (!await hero.IsCheckedAsync()) return new SimulationVerdict(false, "No pude marcar al héroe en el simulador");

            // Real current stats from /hero/attributes.
            if (stats.HpPercent is not int hp) return new SimulationVerdict(false, "Vida del héroe desconocida");
            result = await browser.Input(CombatSimulatorParser.GetHeroHealthInput(page), hp.ToString());
            if (result.IsFailed) return Result.Fail(result.Errors);

            var strengthOk = await EnsureHeroPoints(CombatSimulatorParser.GetHeroStrengthInput(page), stats.StrengthPoints);
            if (strengthOk.IsFailed) return Result.Fail(strengthOk.Errors);
            if (!strengthOk.Value) return new SimulationVerdict(false, "No pude cargar la fuerza del héroe en el simulador");

            var offOk = await EnsureHeroPoints(CombatSimulatorParser.GetHeroOffPointsInput(page), stats.OffBonusPoints);
            if (offOk.IsFailed) return Result.Fail(offOk.Errors);
            if (!offOk.Value) return new SimulationVerdict(false, "No pude cargar el bonus de ataque del héroe en el simulador");

            // The defender must be the animals we just saw on the tile.
            var defenders = await CombatSimulatorParser.ReadDefenderTotal(page);
            if (defenders != animals) return new SimulationVerdict(false, $"Los animales del simulador ({defenders?.ToString() ?? "?"}) no coinciden con el oasis ({animals})");

            result = await browser.Click(CombatSimulatorParser.GetSimulateButton(page));
            if (result.IsFailed) return Result.Fail(result.Errors);

            result = await browser.Wait(CombatSimulatorParser.GetAttackResult(page));
            if (result.IsFailed) return Result.Fail(result.Errors);
            await System.Threading.Tasks.Task.Delay(500);

            var numbers = await CombatSimulatorParser.ReadResult(page);
            if (numbers is not null && numbers.HeroHealthBefore is int before && before != hp)
                return new SimulationVerdict(false, $"El simulador usó {before}% de vida y el héroe tiene {hp}%");

            return HeroLevelingRules.Evaluate(numbers);
        }

        private async Task<Result<bool>> EnsureHeroPoints(ILocator input, int? points)
        {
            if (await input.CountAsync() != 1) return false;

            if (points is int value)
            {
                var result = await browser.Input(input, value.ToString());
                if (result.IsFailed) return Result.Fail(result.Errors);
                return true;
            }

            // Unknown on the attributes page: accept only a readable prefilled value.
            var current = HeroLevelingRules.ParseNumber(await input.InputValueAsync());
            return current is >= 0;
        }

        private async Task<Result<bool>> SendHero(string server, int heroVillageId, int kid, OasisCandidate candidate)
        {
            var page = browser.CurrentPage;
            var origin = new Uri(server);
            var result = await browser.Navigate($"{origin.Scheme}://{origin.Host}/build.php?newdid={heroVillageId}&id=39&tt=2&eventType=4&targetMapId={kid}");
            if (result.IsFailed) return Result.Fail(result.Errors);

            result = await browser.Wait(RallyPointHeroParser.GetSendButton(page));
            if (result.IsFailed) return Result.Fail(result.Errors);

            var form = await RallyPointHeroParser.CheckSendForm(page, candidate.X, candidate.Y);
            if (!form.Ok)
            {
                logger.Information("Rally point form refused: {Reason}", form.Reason);
                return false;
            }

            // Only the hero: clear every other enabled troop input, then type 1 for the hero.
            foreach (var input in await RallyPointHeroParser.GetOtherTroopInputs(page).AllAsync())
            {
                if (await input.IsDisabledAsync()) continue;
                result = await browser.Input(input, "");
                if (result.IsFailed) return Result.Fail(result.Errors);
            }

            result = await browser.Input(RallyPointHeroParser.GetHeroInput(page), "1");
            if (result.IsFailed) return Result.Fail(result.Errors);

            var raid = RallyPointHeroParser.GetRaidRadio(page);
            if (!await raid.IsCheckedAsync())
            {
                result = await browser.Click(raid);
                if (result.IsFailed) return Result.Fail(result.Errors);
            }

            var ready = await RallyPointHeroParser.CheckFilledForm(page, candidate.X, candidate.Y);
            if (!ready.Ok)
            {
                logger.Information("Rally point form refused after filling: {Reason}", ready.Reason);
                return false;
            }

            result = await browser.Click(RallyPointHeroParser.GetSendButton(page));
            if (result.IsFailed) return Result.Fail(result.Errors);

            var confirm = RallyPointHeroParser.GetConfirmButton(page);
            result = await browser.Wait(confirm);
            if (result.IsFailed) return Result.Fail(result.Errors);

            var confirmation = await RallyPointHeroParser.CheckConfirmation(page, candidate.X, candidate.Y);
            if (!confirmation.Ok)
            {
                // Leave the confirmation page without confirming.
                logger.Warning("Hero leveling did NOT confirm the raid: {Reason}", confirmation.Reason);
                return false;
            }

            result = await browser.Click(confirm);
            if (result.IsFailed) return Result.Fail(result.Errors);
            return true;
        }

        #endregion hunt

        #region data

        private bool IsEnabled(AccountId accountId)
        {
            using var context = contextFactory.CreateDbContext();
            return HeroLevelingStore.IsEnabled(context, accountId);
        }

        private string? GetServer(AccountId accountId)
        {
            using var context = contextFactory.CreateDbContext();
            return context.Accounts.Where(x => x.Id == accountId.Value).Select(x => x.Server).FirstOrDefault();
        }

        private List<OasisTarget>? GetOasisTargets(AccountId accountId, out string message)
        {
            using var context = contextFactory.CreateDbContext();
            var lists = context.CowLists
                .Where(x => x.AccountId == accountId.Value)
                .Select(x => new { x.Id, x.Name })
                .AsEnumerable()
                .Where(x => HeroLevelingRules.IsOasisList(x.Name))
                .Select(x => x.Id)
                .ToList();

            message = HeroLevelingStore.MissingListMessage;
            if (lists.Count == 0) return null;

            var targets = context.CowVillages
                .Where(x => lists.Contains(x.CowListId))
                .OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.Url })
                .AsEnumerable()
                .Select(x => new OasisTarget(x.Id, x.Url))
                .ToList();
            if (targets.Count == 0) return null;

            message = "";
            return targets;
        }

        private (int X, int Y)? GetVillageCoordinates(AccountId accountId, int villageId)
        {
            using var context = contextFactory.CreateDbContext();
            var village = context.Villages
                .Where(x => x.Id == villageId && x.AccountId == accountId.Value)
                .Select(x => new { x.X, x.Y })
                .FirstOrDefault();
            return village is null ? null : (village.X, village.Y);
        }

        private void SaveStats(AccountId accountId, HeroStats stats)
        {
            using var context = contextFactory.CreateDbContext();
            HeroLevelingStore.SaveStats(context, accountId, stats);
        }

        private int? GetSearchStart(AccountId accountId, HeroStats stats)
        {
            using var context = contextFactory.CreateDbContext();
            var row = HeroLevelingStore.Get(context, accountId);
            if (row?.NextOasisId is null) return null;
            if (HeroLevelingRules.ShouldRestartSearch(row.SearchLevel, row.SearchHp, stats.Level, stats.HpPercent))
            {
                logger.Information("Hero leveling: hero is stronger than when the search stopped, starting again from the closest oasis");
                return null;
            }
            return row.NextOasisId;
        }

        private void SaveSearch(AccountId accountId, int? nextOasisId, HeroStats stats)
        {
            using var context = contextFactory.CreateDbContext();
            HeroLevelingStore.SaveSearch(context, accountId, nextOasisId, stats.Level, stats.HpPercent);
        }

        private void Reschedule(Task task, DateTime at, string state, string message)
        {
            task.ExecuteAt = at;
            using var context = contextFactory.CreateDbContext();
            HeroLevelingStore.SaveState(context, task.AccountId, state, message, at);
            logger.Information("Hero leveling: {Message}", message);
        }

        private void Disable(AccountId accountId, string message)
        {
            using var context = contextFactory.CreateDbContext();
            HeroLevelingStore.SetEnabled(context, accountId, false, message);
        }

        #endregion data
    }
}
