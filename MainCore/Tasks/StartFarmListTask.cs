using MainCore.Commands.Navigate;
using MainCore.Infrasturecture.Extensions;
using MainCore.Tasks.Base;

namespace MainCore.Tasks
{
    [Handler]
    public sealed partial class StartFarmListTask(
        IDbContextFactory<AppDbContext> contextFactory,
        IChromeBrowser browser,
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

            var result = await toFarmListPageCommand.HandleAsync(new(task.AccountId), cancellationToken);
            if (result.IsFailed) return result;

            result = await browser.Click(FarmListParser.GetStartButton(browser.CurrentPage, new FarmId(task.FarmId)));
            if (result.IsFailed) return result;

            task.ExecuteAt = DateTime.Now.AddSeconds(NextSeconds(farm.Min, farm.Max));
            return Result.Ok();
        }

        private FarmPace? LoadFarm(AccountId accountId, int farmId)
        {
            using var context = contextFactory.CreateDbContext();
            var row = context.FarmLists
                .Where(x => x.Id == farmId && x.AccountId == accountId.Value)
                .Select(x => new { x.IsActive, x.IntervalMin, x.IntervalMax })
                .FirstOrDefault();
            return row is null ? null : new FarmPace(row.IsActive, row.IntervalMin, row.IntervalMax);
        }

        private static int NextSeconds(int min, int max)
        {
            if (min < 1) min = 540;
            if (max < min) max = min;
            return GetSettingsExtension.Normal(min, max);
        }

        private sealed record FarmPace(bool Active, int Min, int Max);
    }
}
