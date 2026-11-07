using MainCore.Infrasturecture.Extensions;
using MainCore.Tasks.Base;

namespace MainCore.Tasks
{
    /// <summary>
    /// "Evadir ataques" of the Troops screen. Looks at the attacks coming to the village every few
    /// seconds. When the first attack of a wave is about to land, every troop at home leaves as
    /// reinforcement to the ally the user saved, and the reinforcement is cancelled so the troops come
    /// back right after the last attack of the wave. Attacks that land close together (a train) are one
    /// wave. When the wave is too long to cancel in time, the troops stay with the ally and are sent
    /// back once the attacks stop.
    /// Never fails the account: any problem is logged and the village is looked at again a bit later.
    /// </summary>
    [Handler]
    public sealed partial class EvadeAttackTask(
        IDbContextFactory<AppDbContext> contextFactory,
        IChromeBrowser browser,
        EvadeStateStore store,
        ILogger logger)
    {
        public sealed class Task(AccountId accountId, VillageId villageId, string villageName = "") : AccountTask(accountId)
        {
            public VillageId VillageId { get; } = villageId;

            /// <summary>One evasion task per village, next to the other tasks of the account.</summary>
            public override string Key => $"{AccountId}-evade-{VillageId}";

            protected override string TaskName => string.IsNullOrWhiteSpace(villageName) ? $"Evade attacks #{VillageId}" : $"Evade attacks in {villageName}";

            public override bool CanStart(AppDbContext context) => context.BooleanByName(VillageId, VillageSettingEnums.EvadeEnable);
        }

        /// <summary>
        /// Starts the evasion of a village when it is turned on, and takes it out of the queue when it is
        /// turned off. A task already waiting keeps its time, unless the settings just changed: then it looks now.
        /// </summary>
        public static void Schedule(AppDbContext context, TaskManager taskManager, AccountId accountId, VillageId villageId)
        {
            var existing = taskManager.GetTaskList(accountId).OfType<Task>().FirstOrDefault(x => x.VillageId == villageId);
            var enabled = context.BooleanByName(villageId, VillageSettingEnums.EvadeEnable);
            if (!enabled)
            {
                if (existing is not null && existing.Stage != StageEnums.Executing) taskManager.Remove(accountId, existing);
                return;
            }
            if (existing is not null)
            {
                if (existing.Stage != StageEnums.Executing && existing.ExecuteAt > DateTime.Now) existing.ExecuteAt = DateTime.Now;
                return;
            }
            var name = context.Villages.Where(x => x.Id == villageId.Value).Select(x => x.Name).FirstOrDefault() ?? "";
            taskManager.Add(new Task(accountId, villageId, name));
        }

        private sealed record Settings(bool Enable, int X, int Y, int Lead, int Gap, int Safety, int Check, bool Hero);

        private async ValueTask<Result> HandleAsync(Task task, CancellationToken cancellationToken)
        {
            var village = task.VillageId;
            var settings = ReadSettings(village);
            if (!settings.Enable)
            {
                store.Clear(village);
                return Result.Ok();
            }

            var server = ReadServer(task.AccountId);
            if (string.IsNullOrWhiteSpace(server)) return Again(task, settings.Check, "No se encontró el servidor de la cuenta");

            try
            {
                var state = store.Get(village);
                if (state.Stationed) return await HandleStationed(task, settings, server, state, cancellationToken);

                var attacks = await ReadIncoming(server, village);
                if (attacks is null) return Again(task, 30, "No se pudo leer el punto de reunión");

                var waves = EvadeRules.GroupWaves(attacks, settings.Gap);
                Remember(state, attacks, waves);
                if (waves.Count == 0) return Again(task, settings.Check, "Sin ataques a la vista");

                var wave = waves[0];
                if (settings.X == 0 && settings.Y == 0)
                    return Again(task, settings.Check, $"Viene{(attacks.Count == 1 ? "" : "n")} {attacks.Count} ataque{(attacks.Count == 1 ? "" : "s")}, pero faltan las coordenadas del aliado", history: true);

                var now = DateTime.Now;
                var sendAt = EvadeRules.SendAt(wave, settings.Lead);
                if (state.HomeAt is not null && state.HomeAt > sendAt)
                {
                    // The troops of the last wave are still coming back: they cannot leave again in time.
                    store.Say(village, $"Las tropas vuelven a las {state.HomeAt:HH:mm:ss}, después del próximo ataque", history: true);
                }

                if (sendAt - now > TimeSpan.FromSeconds(EvadeRules.PrepareSeconds))
                {
                    var wake = Min(now.AddSeconds(settings.Check), sendAt.AddSeconds(-EvadeRules.PrepareSeconds));
                    task.ExecuteAt = wake;
                    store.Say(village, Describe(wave) + $". Las tropas salen a las {sendAt:HH:mm:ss}");
                    return Result.Ok();
                }

                if (wave.First <= now.AddSeconds(1))
                    return Again(task, 5, "El ataque ya está llegando: no hay tiempo de sacar las tropas", history: true);

                return await Evade(task, settings, server, wave, sendAt, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Evade attacks failed: {Message}", ex.Message);
                return Again(task, 30, "Error: " + ex.Message, history: true);
            }
        }

        /// <summary>Sends every troop at home to the ally at the right second, then cancels when it is safe.</summary>
        private async Task<Result> Evade(Task task, Settings settings, string server, AttackWave wave, DateTime sendAt, CancellationToken cancellationToken)
        {
            var village = task.VillageId;
            var page = browser.CurrentPage;

            var result = await browser.Navigate(EvadeParser.SendUrl(server, village.Value));
            if (result.IsFailed) return Again(task, 10, "No se abrió el punto de reunión");
            result = await browser.Wait(CowOasisParser.GetTroopTable(browser.CurrentPage));
            if (result.IsFailed) return Again(task, 10, "No apareció el formulario de tropas");
            page = browser.CurrentPage;

            var total = await EvadeParser.FillAllTroops(page, settings.Hero);
            if (total == 0)
                return Next(task, wave.Last.AddSeconds(settings.Safety + 2), Describe(wave) + ". No hay tropas en casa para sacar", history: true);

            result = await browser.Input(EvadeParser.GetX(page), settings.X.ToString());
            if (result.IsFailed) return Again(task, 10, "No se pudo escribir la X del aliado");
            result = await browser.Input(EvadeParser.GetY(page), settings.Y.ToString());
            if (result.IsFailed) return Again(task, 10, "No se pudo escribir la Y del aliado");

            var reinforcement = EvadeParser.GetReinforcement(page);
            if (await reinforcement.CountAsync() > 0 && await reinforcement.First.IsEnabledAsync())
                await reinforcement.First.CheckAsync(new() { Timeout = 3_000 });

            result = await browser.Click(CowOasisParser.GetSendButton(page));
            if (result.IsFailed) return Again(task, 10, "No se pudo apretar Enviar");

            var confirm = ConfirmButton(browser.CurrentPage);
            result = await browser.Wait(confirm);
            if (result.IsFailed)
                return Again(task, settings.Check, $"Travian no aceptó el envío a ({settings.X}|{settings.Y}). Revisá las coordenadas del aliado", history: true);

            var travel = await EvadeParser.ReadTravelSeconds(browser.CurrentPage);

            // Wait on the confirmation page until the second the troops have to leave.
            var wait = sendAt - DateTime.Now;
            if (wait > TimeSpan.Zero) await System.Threading.Tasks.Task.Delay(wait, cancellationToken);

            var sentAt = DateTime.Now;
            result = await browser.Click(confirm);
            if (result.IsFailed) return Again(task, 5, "No se pudo confirmar el envío de las tropas", history: true);

            var state = store.Get(village);
            state.SentAt = sentAt;
            state.HomeAt = null;
            logger.Information("Evade: {Total} troops sent to ({X}|{Y}) at {Time}, {Wave}", total, settings.X, settings.Y, sentAt.ToString("HH:mm:ss"), Describe(wave));
            store.Say(village, $"{total} tropas salieron a ({settings.X}|{settings.Y}) a las {sentAt:HH:mm:ss}. {Describe(wave)}", history: true);

            var cancelAt = EvadeRules.CancelAt(wave, sentAt, settings.Safety, travel);
            if (cancelAt is not null)
            {
                // Shortly before cancelling, look again: more attacks of the train may have shown up.
                var look = cancelAt.Value.AddSeconds(-20);
                if (look > DateTime.Now) await System.Threading.Tasks.Task.Delay(look - DateTime.Now, cancellationToken);
                var attacks = await ReadIncoming(server, village);
                if (attacks is not null)
                {
                    wave = EvadeRules.Extend(wave, attacks, settings.Gap);
                    Remember(state, attacks, EvadeRules.GroupWaves(attacks, settings.Gap));
                    cancelAt = EvadeRules.CancelAt(wave, sentAt, settings.Safety, travel);
                }
            }

            if (cancelAt is not null)
            {
                var cancelled = await Cancel(server, village, settings, cancelAt.Value, cancellationToken);
                if (cancelled is not null)
                {
                    var home = EvadeRules.HomeAt(sentAt, cancelled.Value);
                    state.HomeAt = home;
                    return Next(task, home.AddSeconds(3), $"Refuerzo cancelado a las {cancelled:HH:mm:ss}. Las tropas vuelven a las {home:HH:mm:ss}", history: true);
                }
                store.Say(village, "No se pudo cancelar el refuerzo: las tropas se quedan con el aliado", history: true);
            }

            // Long train: the troops arrive at the ally and come back when the attacks stop.
            state.Stationed = true;
            state.TravelSeconds = Math.Max(0, travel);
            state.WaveLast = wave.Last;
            var arrive = travel > 0 ? sentAt.AddSeconds(travel) : DateTime.Now.AddSeconds(settings.Check);
            var recallFrom = wave.Last.AddSeconds(settings.Safety - Math.Max(0, travel));
            return Next(task, Max(Max(arrive.AddSeconds(5), recallFrom), DateTime.Now.AddSeconds(30)),
                $"{Describe(wave)}: es largo, las tropas quedan con el aliado hasta que paren los ataques", history: true);
        }

        /// <summary>Cancels the reinforcement at <paramref name="cancelAt"/>. Returns when it was cancelled, or null.</summary>
        private async Task<DateTime?> Cancel(string server, VillageId village, Settings settings, DateTime cancelAt, CancellationToken cancellationToken)
        {
            // Open the outgoing list a few seconds early, so the click happens on time.
            var open = cancelAt.AddSeconds(-6);
            if (open > DateTime.Now) await System.Threading.Tasks.Task.Delay(open - DateTime.Now, cancellationToken);

            var result = await browser.Navigate(EvadeParser.OverviewUrl(server, village.Value, 2));
            if (result.IsFailed) return null;
            var page = browser.CurrentPage;
            try
            {
                await EvadeParser.GetOverview(page).WaitForAsync(new() { Timeout = 8_000 });
            }
            catch (TimeoutException)
            {
            }

            var wait = cancelAt - DateTime.Now;
            if (wait > TimeSpan.Zero) await System.Threading.Tasks.Task.Delay(wait, cancellationToken);

            // Some servers ask "are you sure?" before cancelling.
            void Accept(object? sender, IDialog dialog) => _ = dialog.AcceptAsync();
            page.Dialog += Accept;
            try
            {
                var at = DateTime.Now;
                if (!await EvadeParser.ClickCancel(page, settings.X, settings.Y)) return null;
                try
                {
                    await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new() { Timeout = 8_000 });
                }
                catch (TimeoutException)
                {
                }
                logger.Information("Evade: reinforcement to ({X}|{Y}) cancelled at {Time}", settings.X, settings.Y, at.ToString("HH:mm:ss"));
                return at;
            }
            finally
            {
                page.Dialog -= Accept;
            }
        }

        /// <summary>The troops wait with the ally: bring them back when the train is over.</summary>
        private async Task<Result> HandleStationed(Task task, Settings settings, string server, EvadeStateStore.State state, CancellationToken cancellationToken)
        {
            var village = task.VillageId;
            var attacks = await ReadIncoming(server, village);
            if (attacks is null) return Again(task, 30, "No se pudo leer el punto de reunión");
            var waves = EvadeRules.GroupWaves(attacks, settings.Gap);
            Remember(state, attacks, waves);

            // A train that keeps going moves the end of the wave.
            if (waves.Count > 0 && waves[0].First - state.WaveLast <= TimeSpan.FromSeconds(settings.Gap))
                state.WaveLast = Max(state.WaveLast, waves[0].Last);

            var now = DateTime.Now;
            if (!EvadeRules.CanRecall(now, state.TravelSeconds, state.WaveLast, attacks, settings.Lead, settings.Safety))
            {
                var from = state.WaveLast.AddSeconds(settings.Safety - state.TravelSeconds);
                var wake = Max(now.AddSeconds(30), Min(from, now.AddSeconds(settings.Check)));
                task.ExecuteAt = wake;
                store.Say(village, waves.Count > 0
                    ? $"Las tropas siguen con el aliado: {Describe(waves[0]).ToLowerInvariant()}"
                    : $"Las tropas siguen con el aliado hasta las {from:HH:mm:ss}");
                return Result.Ok();
            }

            cancellationToken.ThrowIfCancellationRequested();
            var result = await browser.Navigate($"{server.TrimEnd('/')}/build.php?newdid={village.Value}&gid=16&tt=1");
            if (result.IsFailed) return Again(task, 30, "No se abrió el punto de reunión");
            var page = browser.CurrentPage;
            try
            {
                await EvadeParser.GetOverview(page).WaitForAsync(new() { Timeout = 8_000 });
            }
            catch (TimeoutException)
            {
            }

            var opened = await EvadeParser.ClickSendBack(page, settings.X, settings.Y);
            if (!opened)
            {
                result = await browser.Navigate(EvadeParser.OverviewUrl(server, village.Value, 4));
                if (result.IsSuccess) opened = await EvadeParser.ClickSendBack(browser.CurrentPage, settings.X, settings.Y);
            }
            if (!opened)
            {
                store.Clear(village);
                return Again(task, settings.Check, $"No encontré las tropas en ({settings.X}|{settings.Y}) para traerlas: revisalo en el punto de reunión", history: true);
            }

            try
            {
                await browser.CurrentPage.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new() { Timeout = 8_000 });
            }
            catch (TimeoutException)
            {
            }

            if (!await EvadeParser.SubmitSendBack(browser.CurrentPage))
            {
                store.Clear(village);
                return Again(task, settings.Check, "Se abrió \"devolver tropas\" pero no se pudo confirmar: revisalo en el punto de reunión", history: true);
            }

            var home = DateTime.Now.AddSeconds(state.TravelSeconds);
            store.Clear(village);
            var fresh = store.Get(village);
            fresh.HomeAt = home;
            logger.Information("Evade: troops called back from ({X}|{Y}), home at {Time}", settings.X, settings.Y, home.ToString("HH:mm:ss"));
            return Next(task, home.AddSeconds(3), $"Los ataques pararon: las tropas vuelven del aliado y llegan a las {home:HH:mm:ss}", history: true);
        }

        private async Task<List<IncomingAttack>?> ReadIncoming(string server, VillageId village)
        {
            var result = await browser.Navigate(EvadeParser.OverviewUrl(server, village.Value, 1));
            if (result.IsFailed) return null;
            try
            {
                await EvadeParser.GetOverview(browser.CurrentPage).WaitForAsync(new() { Timeout = 8_000 });
            }
            catch (TimeoutException)
            {
                // A village without a rally point has no overview: nothing can be read or sent.
                return null;
            }
            return await EvadeParser.ReadIncoming(browser.CurrentPage, DateTime.Now);
        }

        private static ILocator ConfirmButton(IPage page) => page.Locator("#troopSendForm #confirmSendTroops, #troopSendForm button.rallyPointConfirm").First;

        private void Remember(EvadeStateStore.State state, List<IncomingAttack> attacks, List<AttackWave> waves)
        {
            state.CheckedAt = DateTime.Now;
            state.Incoming = attacks.Count;
            state.NextAttack = attacks.Count > 0 ? attacks[0].Arrival : null;
            state.WaveFirst = waves.Count > 0 ? waves[0].First : null;
            state.WaveEnd = waves.Count > 0 ? waves[0].Last : null;
            state.WaveCount = waves.Count > 0 ? waves[0].Count : 0;
        }

        private static string Describe(AttackWave wave)
        {
            if (wave.Count == 1) return $"Ataque llega a las {wave.First:HH:mm:ss}";
            return $"Tren de {wave.Count} ataques entre las {wave.First:HH:mm:ss} y las {wave.Last:HH:mm:ss}";
        }

        private Result Again(Task task, int seconds, string message, bool history = false) =>
            Next(task, DateTime.Now.AddSeconds(Math.Max(5, seconds)), message, history);

        private Result Next(Task task, DateTime at, string message, bool history = false)
        {
            task.ExecuteAt = at;
            store.Say(task.VillageId, message, history);
            if (history) logger.Information("Evade: {Message}", message);
            return Result.Ok();
        }

        private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

        private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;

        private Settings ReadSettings(VillageId village)
        {
            using var context = contextFactory.CreateDbContext();
            var keys = new List<VillageSettingEnums>
            {
                VillageSettingEnums.EvadeEnable,
                VillageSettingEnums.EvadeTargetX,
                VillageSettingEnums.EvadeTargetY,
                VillageSettingEnums.EvadeLeadSeconds,
                VillageSettingEnums.EvadeTrainGapSeconds,
                VillageSettingEnums.EvadeSafetySeconds,
                VillageSettingEnums.EvadeCheckSeconds,
                VillageSettingEnums.EvadeIncludeHero,
            };
            var saved = context.ByName(village, keys);
            int Value(VillageSettingEnums key) => saved.TryGetValue(key, out var value) ? value : AppDbContext.VillageDefaultSettings[key];
            return new Settings(
                Value(VillageSettingEnums.EvadeEnable) == 1,
                Value(VillageSettingEnums.EvadeTargetX),
                Value(VillageSettingEnums.EvadeTargetY),
                Math.Clamp(Value(VillageSettingEnums.EvadeLeadSeconds), 3, 60),
                Math.Clamp(Value(VillageSettingEnums.EvadeTrainGapSeconds), 0, 1800),
                Math.Clamp(Value(VillageSettingEnums.EvadeSafetySeconds), 0, 120),
                Math.Clamp(Value(VillageSettingEnums.EvadeCheckSeconds), 30, 1800),
                Value(VillageSettingEnums.EvadeIncludeHero) == 1);
        }

        private string? ReadServer(AccountId accountId)
        {
            using var context = contextFactory.CreateDbContext();
            return context.Accounts.Where(x => x.Id == accountId.Value).Select(x => x.Server).FirstOrDefault();
        }
    }
}
