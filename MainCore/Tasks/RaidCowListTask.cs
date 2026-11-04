using MainCore.Tasks.Base;

namespace MainCore.Tasks
{
    [Handler]
    public sealed partial class RaidCowListTask(
        IDbContextFactory<AppDbContext> contextFactory,
        IChromeBrowser browser,
        RxQueue rxQueue,
        ILogger logger)
    {
        public sealed class Task(AccountId accountId, int cowListId) : AccountTask(accountId)
        {
            public int CowListId { get; } = cowListId;
            public int NextIndex { get; set; }
            public bool KeepRepeating { get; set; } = true;
            public Dictionary<int, int> KnownTroops { get; } = [];
            protected override string TaskName => $"Raid cow list #{CowListId}";
        }

        private readonly record struct CowTarget(int Id, string Url, string? TroopPlan, int TroopCount, DateTime? LastAttackedAt, int MinAttackMinutes);

        private async ValueTask<Result> HandleAsync(Task task, CancellationToken cancellationToken)
        {
            var targets = GetTargets(task);
            if (targets.Count == 0) return Skip.Error.WithError("Cow list has no villages");

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
                    await System.Threading.Tasks.Task.Delay(NextAttackDelay(), cancellationToken);

                    var result = await RaidTarget(task, target, slots);
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
                return Result.Ok();
            }

            if (task.KeepRepeating) ScheduleNext(task);
            return Result.Ok();
        }

        private async Task<Result> RaidTarget(Task task, CowTarget target, IReadOnlyList<CowTroopSlot> slots)
        {
            var result = await browser.Navigate(target.Url);
            if (result.IsFailed)
            {
                MarkSkipped(target.Id, "Error");
                return result;
            }

            if (await LoginParser.IsLoginPage(browser.CurrentPage) || !await LoginParser.IsIngamePage(browser.CurrentPage))
            {
                MarkSkipped(target.Id, "Error");
                return Retry.Error.WithError($"Tile page did not load for {target.Url}");
            }

            var tile = await CowOasisParser.ReadTile(browser.CurrentPage);
            if (!tile.IsOasis)
            {
                logger.Information("Skipping {Url}: it is not an oasis", target.Url);
                MarkSkipped(target.Id, "No oasis");
                return Result.Ok();
            }

            if (!tile.IsUnoccupied)
            {
                logger.Information("Skipping {Url}: oasis is occupied", target.Url);
                MarkSkipped(target.Id, "Ocupado");
                return Result.Ok();
            }

            if (!tile.HasNoTroops)
            {
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

            result = await browser.Wait(CowOasisParser.GetTroopTable(browser.CurrentPage));
            if (result.IsFailed)
            {
                MarkSkipped(target.Id, "Error");
                return result;
            }

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

        private static TimeSpan NextAttackDelay()
        {
            const double minimum = 1.5;
            const double maximum = 2.5;
            const double mean = (minimum + maximum) / 2.0;
            const double stdDev = (maximum - minimum) / 6.0;

            var u1 = 1.0 - Random.Shared.NextDouble();
            var u2 = 1.0 - Random.Shared.NextDouble();
            var standardNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            var seconds = Math.Clamp(mean + (stdDev * standardNormal), minimum, maximum);
            return TimeSpan.FromSeconds(seconds);
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
            task.ExecuteAt = DateTime.Now.AddMinutes(repeat);
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
                .Select(x => new { x.Id, x.Url, x.TroopPlan, x.TroopCount, x.LastAttackedAt, x.CowList.MinAttackMinutes })
                .AsEnumerable()
                .Select(x => new CowTarget(x.Id, x.Url, x.TroopPlan, x.TroopCount, x.LastAttackedAt, x.MinAttackMinutes))
                .ToList();
        }
    }
}
