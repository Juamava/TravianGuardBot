namespace MainCore.Commands.Features.UseHeroItem
{
    [Handler]
    public sealed partial class UseHeroResourceCommand(IChromeBrowser browser)
    {
        public sealed record Command(BuildingEnums Building, long[] Resource);

        private async ValueTask<Result> HandleAsync(Command command)
        {
            var (building, resource) = command;

            Result result;
            var fillUpButton = await UpgradeParser.GetFillUpButton(browser.CurrentPage, building);
            if (await fillUpButton.CountAsync() == 0)
                return Retry.Error.WithError("The page has no control to use hero resources");

            result = await browser.Click(fillUpButton);
            if (result.IsFailed) return result;

            // Short wait: when the dialog does not open, the caller falls back to "wait for resources".
            try
            {
                await InventoryParser.GetResourceTransferDialog(browser.CurrentPage).WaitForAsync(new() { Timeout = DialogTimeoutMs });
            }
            catch (TimeoutException)
            {
                return Retry.Error.WithError("The hero resource dialog did not open");
            }

            result = await IsEnoughResource(resource);
            if (result.IsFailed)
            {
                await browser.Refresh();
                return result;
            }

            for (var i = 0; i < resource.Length && i < _itemInputName.Count; i++)
            {
                if (resource[i] <= 0) continue;
                result = await browser.Input(InventoryParser.GetAmountBox(browser.CurrentPage, _itemInputName[i]), resource[i].ToString());
                if (result.IsFailed) return result;
            }

            var buttons = InventoryParser.GetResourceConfirmButton(browser.CurrentPage);
            var buttonCount = await buttons.CountAsync();
            var confirm = buttonCount >= 2 ? buttons.Nth(1) : buttons.First;
            result = await browser.Click(confirm);
            if (result.IsFailed) return result;

            result = await browser.WaitPageChanged("&reload=auto");
            if (result.IsFailed) return result;

            result = await browser.WaitPageChanged(@"^(?!.*reload=auto).*");
            if (result.IsFailed) return result;

            return Result.Ok();
        }

        private const float DialogTimeoutMs = 5_000;

        private static readonly List<string> _itemInputName =
        [
            "lumber",
            "clay",
            "iron",
            "crop",
        ];

        private async Task<Result> IsEnoughResource(long[] requiredResources)
        {
            var errors = new List<Error>();
            var resources = await InventoryParser.GetInventoryResources(browser.CurrentPage);
            for (var i = 0; i < 4; i++)
            {
                if (resources[i] < requiredResources[i])
                {
                    errors.Add(MissingResource.Error($"{_itemInputName[i]}", resources[i], requiredResources[i]));
                }
            }
            return Result.FailIfNotEmpty(errors);
        }
    }
}