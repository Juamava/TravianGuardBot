using MainCore.Commands.Features.StartAdventure;
using MainCore.Infrasturecture.Extensions;
using MainCore.Tasks.Base;

namespace MainCore.Tasks
{
    [Handler]
    public sealed partial class StartAdventureTask(
        IChromeBrowser browser,
        ILogger logger,
        OpenAdventurePanelCommand.Handler openAdventurePanelCommand,
        ExploreAdeventureCommand.Handler exploreAdeventureCommand)
    {
        private static readonly TimeSpan LowHealthRetry = TimeSpan.FromMinutes(30);

        public sealed class Task(AccountId accountId) : AccountTask(accountId)
        {
            protected override string TaskName => "Start adventure";

            public override bool CanStart(AppDbContext context)
            {
                var settingEnable = context.BooleanByName(AccountId, AccountSettingEnums.EnableAutoStartAdventure);
                if (!settingEnable) return false;

                return true;
            }
        }

        private async ValueTask<Result> HandleAsync(Task task, CancellationToken cancellationToken)
        {
            var canStartAdventure = await AdventureParser.CanStartAdventure(browser.CurrentPage);
            if (!canStartAdventure) return Skip.Error.WithError("No adventure available");

            // Adventures can hurt or kill the hero: only go with at least MinHpPercent health.
            // Fail closed: unreadable health counts as too low.
            var hpPercent = await ReadHeroHealth();
            if (!HeroLevelingRules.CanGoOnAdventure(hpPercent))
            {
                logger.Information("Not starting adventure: hero HP is {Hp}%, minimum is {Minimum}%", hpPercent?.ToString() ?? "unknown", HeroLevelingRules.MinHpPercent);
                // Keep the task queued with a later time so it is not re-added and re-checked after every task.
                task.ExecuteAt = DateTime.Now.Add(LowHealthRetry);
                return Result.Ok();
            }

            // The attributes page has the same top bar, but check again in case the hero just left.
            canStartAdventure = await AdventureParser.CanStartAdventure(browser.CurrentPage);
            if (!canStartAdventure) return Skip.Error.WithError("No adventure available");

            Result result;
            result = await openAdventurePanelCommand.HandleAsync(new(browser), cancellationToken);
            if (result.IsFailed) return result;

            result = await exploreAdeventureCommand.HandleAsync(new(browser), cancellationToken);
            if (result.IsFailed) return result;

            var adventureDuration = await AdventureParser.GetAdventureDuration(browser.CurrentPage);
            task.ExecuteAt = DateTime.Now.Add(adventureDuration * 2);

            return Result.Ok();
        }

        private async Task<int?> ReadHeroHealth()
        {
            var server = new Uri(browser.CurrentUrl);
            var result = await browser.Navigate($"{server.Scheme}://{server.Host}{HeroParser.AttributesPath}");
            if (result.IsFailed) return null;

            result = await browser.Wait(HeroParser.GetAttributesBox(browser.CurrentPage));
            if (result.IsFailed) return null;

            // The attributes page is rendered by React; give the values a moment.
            await System.Threading.Tasks.Task.Delay(800);
            var stats = await HeroParser.ReadStats(browser.CurrentPage);
            return stats.HpPercent;
        }
    }
}
