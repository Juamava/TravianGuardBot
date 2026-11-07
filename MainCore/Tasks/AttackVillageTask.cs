using MainCore.Tasks.Base;

namespace MainCore.Tasks
{
    /// <summary>
    /// One attack asked from the Scouting screen: opens the target, fills the plan in the rally point,
    /// picks raid or normal attack, sends and confirms. Runs once, in its turn on the shared browser.
    /// Every failure is a Skip, never a retry, so an attack is never sent twice.
    /// </summary>
    [Handler]
    public sealed partial class AttackVillageTask(
        IDbContextFactory<AppDbContext> contextFactory,
        IChromeBrowser browser,
        RxQueue rxQueue,
        TaskManager taskManager,
        ILogger logger)
    {
        public sealed class Task(AccountId accountId, int cowVillageId, string url, string troopPlan, bool raid) : AccountTask(accountId)
        {
            public int CowVillageId { get; } = cowVillageId;
            public string Url { get; } = url;
            public string TroopPlan { get; } = troopPlan;
            public bool Raid { get; } = raid;

            protected override string TaskName => $"Attack {TroopPlan} {(Raid ? "raid" : "normal")}";
        }

        private async ValueTask<Result> HandleAsync(Task task, CancellationToken cancellationToken)
        {
            if (!CowTroopPlan.TryParse(task.TroopPlan, out var slots, out var planError)) return Stop(task, planError);
            SetState(task, ScoutResult.AttackSending, "Abriendo la aldea en el navegador");

            try
            {
                var result = await browser.Navigate(task.Url);
                if (result.IsFailed) return Stop(task, "no se abrió la aldea");

                var tile = await CowOasisParser.ReadTile(browser.CurrentPage);
                if (tile.IsOasis)
                {
                    if (!tile.IsUnoccupied || !tile.HasNoTroops) return Stop(task, "el oasis está ocupado o tiene animales");
                    result = await browser.Click(CowOasisParser.GetRaidLink(browser.CurrentPage));
                }
                else
                {
                    var troopsLink = CowOasisParser.GetSendTroopsLink(browser.CurrentPage);
                    if (await troopsLink.CountAsync() == 0) return Stop(task, "la aldea no tiene \"Enviar tropas\"");
                    try
                    {
                        await troopsLink.ScrollIntoViewIfNeededAsync(new() { Timeout = 2_000 });
                    }
                    catch (TimeoutException)
                    {
                    }
                    result = await browser.Click(troopsLink);
                }
                if (result.IsFailed) return Stop(task, "no se abrió el punto de reunión");

                result = await browser.Wait(CowOasisParser.GetTroopTable(browser.CurrentPage));
                if (result.IsFailed) return Stop(task, "no apareció el formulario de tropas");

                var missing = new List<string>();
                foreach (var slot in slots)
                {
                    var input = CowOasisParser.GetTroopInput(browser.CurrentPage, slot.TroopIndex);
                    var available = await CowOasisParser.ReadAvailable(input);
                    if (available < slot.Amount) missing.Add($"{slot.Letter}: hay {available}, pediste {slot.Amount}");
                }
                if (missing.Count > 0) return Stop(task, "faltan tropas (" + string.Join(", ", missing) + ")");

                foreach (var slot in slots)
                {
                    result = await browser.Input(CowOasisParser.GetTroopInput(browser.CurrentPage, slot.TroopIndex), slot.Amount.ToString());
                    if (result.IsFailed) return Stop(task, "no se pudieron cargar las tropas");
                }

                var mission = RallyPointTroops.GetMission(browser.CurrentPage, task.Raid);
                if (await mission.CountAsync() > 0 && await mission.First.IsEnabledAsync())
                    await mission.First.CheckAsync(new() { Timeout = 3_000 });

                result = await browser.Click(CowOasisParser.GetSendButton(browser.CurrentPage));
                if (result.IsFailed) return Stop(task, "no se pudo apretar Enviar");

                var confirm = CowOasisParser.GetConfirmButton(browser.CurrentPage);
                result = await browser.Wait(confirm);
                if (result.IsFailed) return Stop(task, "no apareció la confirmación");
                result = await browser.Click(confirm);
                if (result.IsFailed) return Stop(task, "no se pudo confirmar");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return Stop(task, ex.Message);
            }

            logger.Information("Attack sent to {Url} with {Plan} ({Mission})", task.Url, task.TroopPlan, task.Raid ? "raid" : "normal");
            SetState(task, ScoutResult.AttackSent, $"{(task.Raid ? "Asalto" : "Ataque normal")} con {task.TroopPlan} en camino. Se lee el reporte cuando llegue.");
            MarkSent(task);
            taskManager.Add(new CheckAttackReportTask.Task(task.AccountId, task.CowVillageId) { ExecuteAt = DateTime.Now.AddMinutes(1) });
            return Result.Ok();
        }

        private Result Stop(Task task, string reason)
        {
            logger.Warning("Attack to {Url} was not sent: {Reason}", task.Url, reason);
            SetState(task, ScoutResult.AttackFailed, "No se envió: " + reason);
            return Skip.Error.WithError("Attack was not sent: " + reason);
        }

        /// <summary>Saves where the attack is, for the Scouting screen.</summary>
        public static void SaveState(AppDbContext context, int cowVillageId, string state, string plan, bool raid, string message)
        {
            var row = context.ScoutResults.FirstOrDefault(x => x.CowVillageId == cowVillageId);
            if (row is null)
            {
                row = new ScoutResult { CowVillageId = cowVillageId };
                context.ScoutResults.Add(row);
            }

            row.AttackState = state;
            row.AttackPlan = plan;
            row.AttackRaid = raid;
            row.AttackAt = DateTime.Now;
            row.AttackMessage = message;
            context.SaveChanges();
        }

        private void MarkSent(Task task)
        {
            using var context = contextFactory.CreateDbContext();
            var now = DateTime.UtcNow;
            context.ScoutResults.Where(x => x.CowVillageId == task.CowVillageId)
                .ExecuteUpdate(x => x.SetProperty(row => row.AttackSentUtc, now));
        }

        private void SetState(Task task, string state, string message)
        {
            try
            {
                using var context = contextFactory.CreateDbContext();
                SaveState(context, task.CowVillageId, state, task.TroopPlan, task.Raid, message);
                rxQueue.Enqueue(new CowVillagesModified(task.AccountId));
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Could not save the attack status: {Message}", ex.Message);
            }
        }
    }
}
