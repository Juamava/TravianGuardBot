using MainCore.Infrasturecture.Extensions;

namespace MainCore.Commands.Features.HeroHunt
{
    /// <summary>
    /// Opens /hero/attributes and reads level, HP, XP, speed and hero points
    /// ("Examples HTML/Hero attributes.html"). Saves them as the Hero page status.
    /// </summary>
    [Handler]
    public sealed partial class ReadHeroStatsCommand(
        IChromeBrowser browser,
        IDbContextFactory<AppDbContext> contextFactory)
    {
        public sealed record Command : ICommand;

        private async ValueTask<Result<HeroStats>> HandleAsync(Command command)
        {
            _ = command;
            var server = new Uri(browser.CurrentUrl);
            var result = await browser.Navigate($"{server.Scheme}://{server.Host}{HeroParser.AttributesPath}");
            if (result.IsFailed) return Result.Fail(result.Errors);

            result = await browser.Wait(HeroParser.GetAttributesBox(browser.CurrentPage));
            if (result.IsFailed) return Result.Fail(result.Errors);

            // The attributes page is rendered by React; give the values a moment.
            await Task.Delay(800);
            var stats = await HeroParser.ReadStats(browser.CurrentPage);

            if (browser.AccountId != AccountId.Empty)
            {
                using var context = contextFactory.CreateDbContext();
                HeroLevelingStore.SaveStats(context, browser.AccountId, stats);
            }

            return stats;
        }
    }
}
