using MainCore.Commands.Features.HeroHunt;
using MainCore.Infrasturecture.Extensions;
using MainCore.Tasks.Base;

namespace MainCore.Tasks
{
    [Handler]
    public sealed partial class RaidCowListTask(
        IDbContextFactory<AppDbContext> contextFactory,
        IChromeBrowser browser,
        RxQueue rxQueue,
        ILogger logger,
        HuntAnimalOasisCommand.Handler huntAnimalOasisCommand)
    {
        public sealed class Task(AccountId accountId, int cowListId) : AccountTask(accountId)
        {
            public int CowListId { get; } = cowListId;
            public int NextIndex { get; set; }
            public bool KeepRepeating { get; set; } = true;
            public Dictionary<int, int> KnownTroops { get; } = [];

            /// <summary>Hero state for this pass over the list ("Levear héroe"). Reset with NextIndex.</summary>
            public HeroRunState Hero { get; } = new();
            protected override string TaskName => $"Raid cow list #{CowListId}";
        }

        /// <summary>List options that only matter for "Levear héroe".</summary>
        private readonly record struct HeroOptions(bool LevelHero, int ListVillageId, string Server);

        private readonly record struct CowTarget(int Id, string Url, string Name, string? TroopPlan, int TroopCount, DateTime? LastAttackedAt, int MinAttackMinutes);

        private async ValueTask<Result> HandleAsync(Task task, CancellationToken cancellationToken)
        {
            var targets = GetTargets(task);
            if (targets.Count == 0) return Skip.Error.WithError("Cow list has no villages");
            var hero = GetHeroOptions(task);

            try
            {
                for (var index = task.NextIndex; index < targets.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var target = targets[index];
                    var minMinutes = GetMinAttackMinutes(task, target.MinAttackMinutes);
                    if (IsOnCooldown(target, minMinutes, out var elapsedMinutes))
                    {
                        logger.Information("Skipping {Url}: attacked {Elapsed} min ago, minimum is {Minimum} min", target.Url, elapsedMinutes, minMinutes);
                        continue;
                    }

                    // When troops cannot go the tile is not opened, not even for the hero:
                    // the hero only hunts animals found on oases the normal raid opens.
                    var slots = CowTroopPlan.Resolve(target.TroopPlan, target.TroopCount);
                    if (slots.Count == 0)
                    {
                        logger.Information("Skipping {Url}: no troops configured", target.Url);
                        MarkSkipped(target.Id, "Sin plan");
                        continue;
                    }

                    if (CowTroopPlan.TryBlocked(task.KnownTroops, slots, out var shortage))
                    {
                        logger.Information("Skipping {Url}: {Shortage}", target.Url, shortage);
                        MarkSkipped(target.Id, "Sin tropas");
                        continue;
                    }

                    logger.Information("Raiding {Url} with {Troops} ({Index}/{Total})", target.Url, CowTroopPlan.Format(slots), index + 1, targets.Count);
                    MarkPhase(target.Id, "Analizando");
                    await System.Threading.Tasks.Task.Delay(NextAttackDelay(task.AccountId), cancellationToken);

                    Result result;
                    try
                    {
                        result = await RaidTarget(task, target, slots, hero, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        logger.Warning(ex, "Skipping {Url}: {Message}", target.Url, ex.Message);
                        MarkSkipped(target.Id, "Error");
                        continue;
                    }

                    if (result.IsFailed)
                    {
                        task.NextIndex = index;
                        return result;
                    }

                    task.NextIndex = index + 1;
                }
            }
            catch (OperationCanceledException)
            {
                logger.Information("Raid cow list #{Id} stopped", task.CowListId);
                ClearLiveStatus(task.CowListId);
                return Result.Ok();
            }

            if (task.KeepRepeating) ScheduleNext(task);
            return Result.Ok();
        }

        private async Task<Result> RaidTarget(Task task, CowTarget target, IReadOnlyList<CowTroopSlot> slots, HeroOptions hero, CancellationToken cancellationToken)
        {
            var result = await browser.Navigate(target.Url);
            if (result.IsFailed)
            {
                logger.Warning("Page did not open for {Url}. Retrying once. {Error}", target.Url, string.Join(" ", result.Reasons.Select(reason => reason.Message)));
                result = await browser.Navigate(target.Url);
            }

            if (result.IsFailed)
            {
                logger.Warning("Skipping {Url}: page did not open. {Error}", target.Url, string.Join(" ", result.Reasons.Select(reason => reason.Message)));
                MarkSkipped(target.Id, "Error");
                return Result.Ok();
            }

            if (await LoginParser.IsLoginPage(browser.CurrentPage) || !await LoginParser.IsIngamePage(browser.CurrentPage))
            {
                MarkSkipped(target.Id, "Error");
                return Retry.Error.WithError($"Tile page did not load for {target.Url}");
            }

            var tile = await CowOasisParser.ReadTile(browser.CurrentPage);
            await CaptureDetails(task, target);
            if (!tile.IsOasis)
            {
                var troopsLink = CowOasisParser.GetSendTroopsLink(browser.CurrentPage);
                if (await troopsLink.CountAsync() == 0)
                {
                    logger.Information("Skipping {Url}: it is not an oasis and has no Send troops", target.Url);
                    MarkSkipped(target.Id, "Sin ataque");
                    return Result.Ok();
                }

                result = await browser.Click(troopsLink);
                if (result.IsFailed)
                {
                    MarkSkipped(target.Id, "Error");
                    return result;
                }

                return await SendPlannedTroops(task, target, slots);
            }

            if (!tile.IsUnoccupied)
            {
                logger.Information("Skipping {Url}: oasis is occupied", target.Url);
                MarkSkipped(target.Id, "Ocupado");
                return Result.Ok();
            }

            if (!tile.HasNoTroops)
            {
                if (hero.LevelHero) return await HuntWithHero(task, target, hero, cancellationToken);

                logger.Information("Skipping {Url}: oasis troop cell does not say none", target.Url);
                MarkSkipped(target.Id, "Hay tropas");
                return Result.Ok();
            }

            result = await browser.Click(CowOasisParser.GetRaidLink(browser.CurrentPage));
            if (result.IsFailed)
            {
                MarkSkipped(target.Id, "Error");
                return result;
            }

            return await SendPlannedTroops(task, target, slots);
        }

        private async Task<Result> SendPlannedTroops(Task task, CowTarget target, IReadOnlyList<CowTroopSlot> slots)
        {
            var result = await browser.Wait(CowOasisParser.GetTroopTable(browser.CurrentPage));
            if (result.IsFailed)
            {
                MarkSkipped(target.Id, "Error");
                return result;
            }

            MarkPhase(target.Id, "Atacando");

            var missing = new List<string>();
            foreach (var slot in slots)
            {
                var input = CowOasisParser.GetTroopInput(browser.CurrentPage, slot.TroopIndex);
                if (await input.CountAsync() == 0)
                {
                    MarkSkipped(target.Id, "Error");
                    return Retry.Error.WithError($"Troop {slot.Letter} input was not on the rally point");
                }

                var available = await CowOasisParser.ReadAvailable(input);
                task.KnownTroops[slot.TroopIndex] = available;
                if (available < slot.Amount)
                    missing.Add($"{slot.Letter} has {available}, need {slot.Amount}");
            }

            if (missing.Count > 0)
            {
                logger.Information("Skipping {Url}: {Shortage}", target.Url, string.Join(", ", missing));
                MarkSkipped(target.Id, "Sin tropas");
                return Result.Ok();
            }

            foreach (var slot in slots)
            {
                result = await browser.Input(CowOasisParser.GetTroopInput(browser.CurrentPage, slot.TroopIndex), slot.Amount.ToString());
                if (result.IsFailed)
                {
                    MarkSkipped(target.Id, "Error");
                    return result;
                }
            }

            result = await browser.Click(CowOasisParser.GetSendButton(browser.CurrentPage));
            if (result.IsFailed)
            {
                MarkSkipped(target.Id, "Error");
                return result;
            }

            var confirmButton = CowOasisParser.GetConfirmButton(browser.CurrentPage);
            result = await browser.Wait(confirmButton);
            if (result.IsFailed)
            {
                MarkSkipped(target.Id, "Error");
                return result;
            }

            result = await browser.Click(confirmButton);
            if (result.IsFailed)
            {
                MarkSkipped(target.Id, "Error");
                return result;
            }

            foreach (var slot in slots)
                task.KnownTroops[slot.TroopIndex] -= slot.Amount;

            MarkAttacked(target.Id);
            return Result.Ok();
        }

        /// <summary>
        /// Oasis with animals on a list with "Levear héroe": the hero hunt decides (simulate, send alone
        /// only when safe). Hero problems never stop the list: they become the oasis status.
        /// LastAttackedAt is not touched, so the troop cooldown stays as it was.
        /// </summary>
        private async Task<Result> HuntWithHero(Task task, CowTarget target, HeroOptions hero, CancellationToken cancellationToken)
        {
            var animals = await CowOasisParser.ReadAnimalCount(browser.CurrentPage);
            if (animals is null or <= 0)
            {
                logger.Information("Skipping {Url}: oasis has troops but the animals could not be counted", target.Url);
                MarkSkipped(target.Id, HeroLevelingRules.StatusTroops);
                return Result.Ok();
            }

            var status = await huntAnimalOasisCommand.HandleAsync(new(hero.Server, hero.ListVillageId, target.Url, animals.Value, task.Hero), cancellationToken);
            if (status.IsFailed)
            {
                if (status.HasError<Cancel>()) throw new OperationCanceledException();
                MarkSkipped(target.Id, "Error");
                return Result.Ok();
            }

            logger.Information("Hero at {Url}: {Status}", target.Url, status.Value);
            MarkSkipped(target.Id, status.Value);
            return Result.Ok();
        }

        private HeroOptions GetHeroOptions(Task task)
        {
            using var context = contextFactory.CreateDbContext();
            var list = context.CowLists
                .Where(x => x.Id == task.CowListId && x.AccountId == task.AccountId.Value)
                .Select(x => new { x.LevelHero, x.VillageId })
                .FirstOrDefault();
            var server = context.Accounts.Where(x => x.Id == task.AccountId.Value).Select(x => x.Server).FirstOrDefault();
            if (list is null || string.IsNullOrWhiteSpace(server)) return new HeroOptions(false, 0, "");
            return new HeroOptions(list.LevelHero, list.VillageId, server);
        }

        private TimeSpan NextAttackDelay(AccountId accountId)
        {
            using var context = contextFactory.CreateDbContext();
            var milliseconds = context.ByName(accountId, AccountSettingEnums.TaskDelayMin, AccountSettingEnums.TaskDelayMax);
            if (milliseconds < 0) milliseconds = 0;
            return TimeSpan.FromMilliseconds(milliseconds);
        }

        private static bool IsOnCooldown(CowTarget target, int minAttackMinutes, out int elapsedMinutes)
        {
            elapsedMinutes = 0;
            if (minAttackMinutes <= 0 || target.LastAttackedAt is null) return false;

            elapsedMinutes = (int)Math.Floor((DateTime.Now - target.LastAttackedAt.Value).TotalMinutes);
            if (elapsedMinutes < 0) elapsedMinutes = 0;
            return elapsedMinutes < minAttackMinutes;
        }

        private int GetMinAttackMinutes(Task task, int fallback)
        {
            using var context = contextFactory.CreateDbContext();
            var minutes = context.CowLists
                .Where(x => x.Id == task.CowListId && x.AccountId == task.AccountId.Value)
                .Select(x => (int?)x.MinAttackMinutes)
                .FirstOrDefault();
            return minutes ?? fallback;
        }

        private void ScheduleNext(Task task)
        {
            using var context = contextFactory.CreateDbContext();
            var repeat = context.CowLists
                .Where(x => x.Id == task.CowListId && x.AccountId == task.AccountId.Value)
                .Select(x => (int?)x.RepeatMinutes)
                .FirstOrDefault() ?? 0;
            if (repeat <= 0 || !task.KeepRepeating) return;

            task.NextIndex = 0;
            task.KnownTroops.Clear();
            task.Hero.Reset();
            task.ExecuteAt = DateTime.Now.AddMinutes(repeat);
        }

        private async System.Threading.Tasks.Task CaptureDetails(Task task, CowTarget target)
        {
            CowOasisParser.TileDetails details;
            try
            {
                details = await CowOasisParser.ReadDetails(browser.CurrentPage);
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Could not read tile details for {Url}", target.Url);
                return;
            }

            if (string.IsNullOrWhiteSpace(details.Title) || details.Title == CowOasisParser.MissingName)
            {
                var title = await CowOasisParser.ReadTitle(browser.CurrentPage);
                if (!string.IsNullOrWhiteSpace(title) && title != CowOasisParser.MissingName)
                    details = details with { Title = title };
            }

            using var context = contextFactory.CreateDbContext();
            var row = context.CowVillages.FirstOrDefault(x => x.Id == target.Id);
            if (row is null) return;

            var listVillageId = context.CowLists.Where(x => x.Id == task.CowListId).Select(x => x.VillageId).FirstOrDefault();
            var origin = context.Villages.Where(x => x.Id == listVillageId).Select(x => new { x.X, x.Y }).FirstOrDefault();
            if (!CowOasisParser.CopyOnto(row, details, target.Url, origin?.X, origin?.Y, replaceName: false)) return;

            context.SaveChanges();
            if (browser.AccountId == AccountId.Empty) return;
            rxQueue.Enqueue(new CowVillagesModified(browser.AccountId));
        }

        private void MarkPhase(int cowVillageId, string phase)
        {
            using var context = contextFactory.CreateDbContext();
            var updated = context.CowVillages
                .Where(x => x.Id == cowVillageId)
                .ExecuteUpdate(x => x.SetProperty(village => village.LastSkipReason, phase));
            if (updated == 0 || browser.AccountId == AccountId.Empty) return;

            rxQueue.Enqueue(new CowVillagesModified(browser.AccountId));
        }

        private void ClearLiveStatus(int cowListId)
        {
            using var context = contextFactory.CreateDbContext();
            context.CowVillages
                .Where(x => x.CowListId == cowListId)
                .Where(x => x.LastSkipReason == "Analizando" || x.LastSkipReason == "Atacando")
                .ExecuteUpdate(x => x.SetProperty(village => village.LastSkipReason, ""));
            if (browser.AccountId == AccountId.Empty) return;

            rxQueue.Enqueue(new CowVillagesModified(browser.AccountId));
        }

        private void MarkAttacked(int cowVillageId)
        {
            using var context = contextFactory.CreateDbContext();
            var attackedAt = DateTime.Now;
            var updated = context.CowVillages
                .Where(x => x.Id == cowVillageId)
                .ExecuteUpdate(x => x
                    .SetProperty(village => village.LastAttackedAt, attackedAt)
                    .SetProperty(village => village.LastSkipReason, ""));
            if (updated == 0 || browser.AccountId == AccountId.Empty) return;

            rxQueue.Enqueue(new CowVillagesModified(browser.AccountId));
        }

        private void MarkSkipped(int cowVillageId, string reason)
        {
            using var context = contextFactory.CreateDbContext();
            var updated = context.CowVillages
                .Where(x => x.Id == cowVillageId)
                .ExecuteUpdate(x => x.SetProperty(village => village.LastSkipReason, reason));
            if (updated == 0 || browser.AccountId == AccountId.Empty) return;

            rxQueue.Enqueue(new CowVillagesModified(browser.AccountId));
        }

        private List<CowTarget> GetTargets(Task task)
        {
            using var context = contextFactory.CreateDbContext();
            return context.CowVillages
                .Where(x => x.CowListId == task.CowListId)
                .Where(x => x.CowList.AccountId == task.AccountId.Value)
                .OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.Url, x.Name, x.TroopPlan, x.TroopCount, x.LastAttackedAt, x.CowList.MinAttackMinutes })
                .AsEnumerable()
                .Select(x => new CowTarget(x.Id, x.Url, x.Name, x.TroopPlan, x.TroopCount, x.LastAttackedAt, x.MinAttackMinutes))
                .ToList();
        }
    }
}
