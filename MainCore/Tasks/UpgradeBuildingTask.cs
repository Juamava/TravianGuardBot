using MainCore.Commands.Features.UpgradeBuilding;
using MainCore.Commands.Update;
using MainCore.Tasks.Base;

namespace MainCore.Tasks
{
    [Handler]
    public sealed partial class UpgradeBuildingTask(
        ILogger logger,
        IChromeBrowser browser,
        GetBuildPlanCommand.Handler getBuildPlanCommand,
        ToBuildPageCommand.Handler toBuildPageCommand,
        HandleResourceCommand.Handler handleResourceCommand,
        HandleUpgradeCommand.Handler handleUpgradeCommand,
        UpdateBuildingCommand.Handler updateBuildingCommand)
    {
        public sealed class Task(AccountId accountId, VillageId villageId) : VillageTask(accountId, villageId)
        {
            protected override string TaskName => "Upgrade building";
        }

        private async ValueTask<Result> HandleAsync(Task task, CancellationToken cancellationToken)
        {
            Result result;

            while (true)
            {
                if (cancellationToken.IsCancellationRequested) return Cancel.Error;

                var (_, isFailed, plan, errors) = await getBuildPlanCommand.HandleAsync(new(task.AccountId, task.VillageId), cancellationToken);
                if (isFailed)
                {
                    var nextExecuteErrors = errors.OfType<NextExecuteError>().OrderBy(x => x.NextExecute).ToList();
                    if (nextExecuteErrors.Count > 0)
                    {
                        task.ExecuteAt = nextExecuteErrors.Min(x => x.NextExecute);
                    }

                    return Skip.Error.WithErrors(errors);
                }

                logger.Information("Build {Type} to level {Level} at location {Location}", plan.Type, plan.Level, plan.Location);

                result = await toBuildPageCommand.HandleAsync(new(task.VillageId, plan), cancellationToken);
                if (result.IsFailed) return result;

                result = await handleResourceCommand.HandleAsync(new(task.AccountId, task.VillageId, plan), cancellationToken);
                if (result.IsFailed)
                {
                    if (result.HasError<LackOfFreeCrop>())
                        continue;

                    if (result.HasError<StorageLimit>())
                    {
                        return Stop.Error.WithErrors(result.Errors);
                    }
                    if (result.HasError<MissingResource>())
                    {
                        var time = TimeSpan.Zero;
                        try
                        {
                            time = await UpgradeParser.GetTimeWhenEnoughResource(browser.CurrentPage, plan.Type);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            logger.Warning("Could not read when resources are enough: {Message}", ex.Message);
                        }

                        // Without a timer on the page, try again later instead of right away.
                        if (time <= TimeSpan.Zero) time = TimeSpan.FromMinutes(5);
                        logger.Information("Not enough resources for {Type}, next try at {Time}", plan.Type, DateTime.Now.Add(time).ToString("yyyy-MM-dd HH:mm:ss"));
                        task.ExecuteAt = DateTime.Now.Add(time);
                        return Skip.Error.WithErrors(result.Errors);
                    }

                    return result;
                }

                result = await handleUpgradeCommand.HandleAsync(new(task.VillageId, plan), cancellationToken);
                if (result.IsFailed) return result;

                logger.Information("Upgrade for {Type} at location {Location} completed successfully.", plan.Type, plan.Location);

                result = await updateBuildingCommand.HandleAsync(new(task.VillageId), cancellationToken);
                if (result.IsFailed) return result;
            }
        }
    }
}