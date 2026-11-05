using MainCore.Commands.Features.ClaimVillageQuest;
using MainCore.Commands.Navigate;

namespace MainCore.Commands.Features.HeroHunt
{
    /// <summary>
    /// Collects Task overview rewards one at a time while the hero HP is below the minimum.
    /// The XP can level the hero up, which restores full HP. Stops as soon as the HP is back:
    /// the rest of the tasks stay as XP reserve. Returns the last stats read, or null when
    /// nothing was collected.
    /// </summary>
    [Handler]
    public sealed partial class CollectHeroTasksCommand(
        IChromeBrowser browser,
        ILogger logger,
        OpenVillageQuestPageCommand.Handler openVillageQuestPageCommand,
        SwitchTabCommand.Handler switchTabCommand,
        ReadHeroStatsCommand.Handler readHeroStatsCommand)
    {
        private const int MaxCollects = 15;

        public sealed record Command : ICommand;

        private async ValueTask<Result<HeroStats?>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            _ = command;
            HeroStats? last = null;

            for (var i = 0; i < MaxCollects; i++)
            {
                if (cancellationToken.IsCancellationRequested) return Cancel.Error;
                if (!await QuestParser.IsQuestClaimable(browser.CurrentPage)) break;

                var collected = await CollectOne(cancellationToken);
                if (collected.IsFailed) return Result.Fail(collected.Errors);
                if (!collected.Value) break;

                var stats = await readHeroStatsCommand.HandleAsync(new(), cancellationToken);
                if (stats.IsFailed) return Result.Fail(stats.Errors);
                last = stats.Value;
                logger.Information("Collected a task reward, hero HP is now {Hp}%", stats.Value.HpPercent);

                if (HeroLevelingRules.CanHunt(stats.Value.HpPercent)) break;
            }

            return last;
        }

        private async Task<Result<bool>> CollectOne(CancellationToken cancellationToken)
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

            await Task.Delay(1500, cancellationToken);
            return true;
        }
    }
}
