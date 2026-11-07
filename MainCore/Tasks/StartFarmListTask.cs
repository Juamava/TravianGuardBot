using MainCore.Commands.Navigate;
using MainCore.Infrasturecture.Extensions;
using MainCore.Tasks.Base;

namespace MainCore.Tasks
{
    [Handler]
    public sealed partial class StartFarmListTask(
        IDbContextFactory<AppDbContext> contextFactory,
        IChromeBrowser browser,
        ILogger logger,
        ToFarmListPageCommand.Handler toFarmListPageCommand
        )
    {
        public sealed class Task(AccountId accountId, int farmId) : AccountTask(accountId)
        {
            public int FarmId { get; } = farmId;
            public override string Key => $"{AccountId}-farm-{FarmId}";
            protected override string TaskName => $"Start farm list #{FarmId}";
        }

        private async ValueTask<Result> HandleAsync(
            Task task,
            CancellationToken cancellationToken)
        {
            var farm = LoadFarm(task.AccountId, task.FarmId);
            if (farm is null || !farm.Active)
                return Skip.Error.WithError("Farm list is inactive");

            if (OnCooldown(farm, out var readyAt, out var elapsed))
            {
                task.ExecuteAt = readyAt;
                logger.Information("Skipping farm list {Id}: started {Elapsed} min ago, minimum is {Minimum} min", task.FarmId, elapsed, farm.MinAttackMinutes);
                return Result.Ok();
            }

            var result = await toFarmListPageCommand.HandleAsync(new(task.AccountId), cancellationToken);
            if (result.IsFailed) return result;

            result = await browser.Click(FarmListParser.GetStartButton(browser.CurrentPage, new FarmId(task.FarmId)));
            if (result.IsFailed) return result;

            var startedAt = DateTime.Now;
            RememberStart(task.AccountId, task.FarmId, startedAt);
            var next = startedAt.AddSeconds(NextSeconds(farm.Min, farm.Max));
            if (farm.MinAttackMinutes >= 1)
            {
                var earliest = startedAt.AddMinutes(farm.MinAttackMinutes);
                if (earliest > next) next = earliest;
            }
            task.ExecuteAt = next;
            return Result.Ok();
        }

        private static bool OnCooldown(FarmPace farm, out DateTime readyAt, out int elapsed)
        {
            readyAt = DateTime.Now;
            elapsed = 0;
            if (farm.MinAttackMinutes < 1 || farm.LastAttackedAt is null) return false;
            elapsed = (int)Math.Floor((DateTime.Now - farm.LastAttackedAt.Value).TotalMinutes);
            if (elapsed < 0) elapsed = 0;
            if (elapsed >= farm.MinAttackMinutes) return false;
            readyAt = farm.LastAttackedAt.Value.AddMinutes(farm.MinAttackMinutes);
            return true;
        }

        private void RememberStart(AccountId accountId, int farmId, DateTime startedAt)
        {
            using var context = contextFactory.CreateDbContext();
            context.FarmLists.Where(x => x.Id == farmId && x.AccountId == accountId.Value)
                .ExecuteUpdate(x => x.SetProperty(farm => farm.LastAttackedAt, startedAt));
        }

        private FarmPace? LoadFarm(AccountId accountId, int farmId)
        {
            using var context = contextFactory.CreateDbContext();
            var row = context.FarmLists
                .Where(x => x.Id == farmId && x.AccountId == accountId.Value)
                .Select(x => new { x.IsActive, x.IntervalMin, x.IntervalMax, x.MinAttackMinutes, x.LastAttackedAt })
                .FirstOrDefault();
            return row is null ? null : new FarmPace(row.IsActive, row.IntervalMin, row.IntervalMax, row.MinAttackMinutes, row.LastAttackedAt);
        }

        private static int NextSeconds(int min, int max)
        {
            if (min < 1) min = 9;
            if (max < min) max = min;
            return GetSettingsExtension.Normal(min, max) * 60;
        }

        private sealed record FarmPace(bool Active, int Min, int Max, int MinAttackMinutes, DateTime? LastAttackedAt);
    }
}
