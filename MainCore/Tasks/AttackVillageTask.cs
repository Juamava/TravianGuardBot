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
            MarkStatus(task, $"Ataque enviado {task.TroopPlan}");
            return Result.Ok();
        }

        private Result Stop(Task task, string reason)
        {
            logger.Warning("Attack to {Url} was not sent: {Reason}", task.Url, reason);
            MarkStatus(task, "Ataque no enviado: " + reason);
            return Skip.Error.WithError("Attack was not sent: " + reason);
        }

        private void MarkStatus(Task task, string status)
        {
            using var context = contextFactory.CreateDbContext();
            var updated = context.CowVillages
                .Where(x => x.Id == task.CowVillageId)
                .ExecuteUpdate(x => x.SetProperty(village => village.LastSkipReason, status));
            if (updated > 0) rxQueue.Enqueue(new CowVillagesModified(task.AccountId));
        }
    }
}
