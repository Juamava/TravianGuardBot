namespace MainCore.Commands.Features.HeroHunt
{
    public enum HeroSendOutcome
    {
        /// <summary>A check failed before Confirm: nothing was sent.</summary>
        NotSent,
        /// <summary>Confirm was pressed. Even if the click reported an error, treat the hero as gone.</summary>
        Sent,
    }

    /// <summary>
    /// Sends the hero ALONE as a raid from his village to the oasis
    /// ("Examples HTML/Rally point raid form with hero.html" and
    /// "Examples HTML/Rally point confirmation hero raid.html").
    /// Checks the form before and after filling it, and the confirmation screen
    /// (raid, target x|y, hero = 1, every other troop = 0) before pressing Confirm.
    /// </summary>
    [Handler]
    public sealed partial class SendHeroRaidCommand(IChromeBrowser browser, ILogger logger)
    {
        public sealed record Command(string Server, int HeroVillageId, int Kid, int X, int Y) : ICommand;

        private async ValueTask<Result<HeroSendOutcome>> HandleAsync(Command command)
        {
            var (server, heroVillageId, kid, x, y) = command;
            var page = browser.CurrentPage;
            var origin = new Uri(server);

            var result = await browser.Navigate($"{origin.Scheme}://{origin.Host}/build.php?newdid={heroVillageId}&id=39&tt=2&eventType=4&targetMapId={kid}");
            if (result.IsFailed) return Result.Fail(result.Errors);

            result = await browser.Wait(RallyPointHeroParser.GetSendButton(page));
            if (result.IsFailed) return Result.Fail(result.Errors);

            var form = await RallyPointHeroParser.CheckSendForm(page, x, y);
            if (!form.Ok)
            {
                logger.Information("Rally point form refused: {Reason}", form.Reason);
                return HeroSendOutcome.NotSent;
            }

            // Only the hero: clear every other enabled troop input, then type 1 for the hero.
            foreach (var input in await RallyPointHeroParser.GetOtherTroopInputs(page).AllAsync())
            {
                if (await input.IsDisabledAsync()) continue;
                result = await browser.Input(input, "");
                if (result.IsFailed) return Result.Fail(result.Errors);
            }

            result = await browser.Input(RallyPointHeroParser.GetHeroInput(page), "1");
            if (result.IsFailed) return Result.Fail(result.Errors);

            var raid = RallyPointHeroParser.GetRaidRadio(page);
            if (!await raid.IsCheckedAsync())
            {
                result = await browser.Click(raid);
                if (result.IsFailed) return Result.Fail(result.Errors);
            }

            var ready = await RallyPointHeroParser.CheckFilledForm(page, x, y);
            if (!ready.Ok)
            {
                logger.Information("Rally point form refused after filling: {Reason}", ready.Reason);
                return HeroSendOutcome.NotSent;
            }

            result = await browser.Click(RallyPointHeroParser.GetSendButton(page));
            if (result.IsFailed) return Result.Fail(result.Errors);

            var confirm = RallyPointHeroParser.GetConfirmButton(page);
            result = await browser.Wait(confirm);
            if (result.IsFailed) return Result.Fail(result.Errors);

            var confirmation = await RallyPointHeroParser.CheckConfirmation(page, x, y);
            if (!confirmation.Ok)
            {
                // Leave the confirmation page without confirming.
                logger.Warning("Hero raid NOT confirmed: {Reason}", confirmation.Reason);
                return HeroSendOutcome.NotSent;
            }

            result = await browser.Click(confirm);
            if (result.IsFailed)
            {
                // We cannot know whether the click went through: assume the hero left so he is not used again this run.
                logger.Warning("Confirm click reported an error; treating the hero as sent. {Error}", string.Join(" ", result.Reasons.Select(r => r.Message)));
            }
            return HeroSendOutcome.Sent;
        }
    }
}
