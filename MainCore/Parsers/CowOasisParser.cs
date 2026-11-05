namespace MainCore.Parsers
{
    public static class CowOasisParser
    {
        public readonly record struct TileInfo(bool IsOasis, bool IsUnoccupied, bool HasNoTroops)
        {
            public bool CanRaid => IsOasis && IsUnoccupied && HasNoTroops;
        }

        public const string TitleXPath = "/html/body/div[2]/div/div/div/div/form/div[5]/div/h1";
        public const string MissingName = "Aldea sin nombre";
        public const string TroopCellXPath = "/html/body/div[2]/div/div/div/div/form/div[5]/div/div[2]/table[2]/tbody/tr[1]/td";
        public const string ConfirmButtonXPath = "/html/body/div[4]/div[3]/div[3]/div[2]/div/div[3]/div/form/div[1]/button[3]";

        public static async Task<TileInfo> ReadTile(IPage page)
        {
            var unoccupied = page.GetByText("Unoccupied oasis");
            var occupied = page.GetByText("Occupied oasis");

            try
            {
                await unoccupied.Or(occupied).First.WaitForAsync(new() { Timeout = 8_000 });
            }
            catch (TimeoutException)
            {
            }

            var isUnoccupied = await unoccupied.CountAsync() > 0;
            // "Unoccupied oasis" also contains the words "occupied oasis".
            var isOccupied = !isUnoccupied && await occupied.CountAsync() > 0;
            return new TileInfo(
                IsOasis: isUnoccupied || isOccupied,
                IsUnoccupied: isUnoccupied,
                HasNoTroops: await CellSaysNone(page));
        }

        public static async Task<string> ReadTitle(IPage page)
        {
            try
            {
                var text = (await page.Locator($"xpath={TitleXPath}").InnerTextAsync(new() { Timeout = 8_000 })).Trim();
                return string.IsNullOrWhiteSpace(text) ? MissingName : text;
            }
            catch (TimeoutException)
            {
                return MissingName;
            }
        }

        public static ILocator GetTroopCell(IPage page)
        {
            return page.Locator($"xpath={TroopCellXPath}");
        }

        private static async Task<bool> CellSaysNone(IPage page)
        {
            var cell = GetTroopCell(page);
            if (await cell.CountAsync() == 0) return false;

            var cellText = (await cell.First.InnerTextAsync()).Trim();
            return string.Equals(cellText, "none", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Total animals in the oasis troop table ("Examples HTML/Unoccupied oasis with animals.html").
        /// 0 when the table says none, null when the table is not on the page.
        /// </summary>
        public static async Task<int?> ReadAnimalCount(IPage page)
        {
            // The reports tab reuses the id: table#troop_info.rep.
            var tables = page.Locator("#map_details table#troop_info:not(.rep)");
            if (await tables.CountAsync() == 0) return null;

            // Without unit rows the table only says "none": nothing to kill, which callers treat as skip.
            var values = await tables.First.Locator("tr:has(img.unit) td.val").AllInnerTextsAsync();
            if (values.Count == 0) return 0;

            var total = 0;
            foreach (var value in values)
            {
                var number = HeroLevelingRules.ParseNumber(value);
                if (number is null || number < 0) return null;
                total += number.Value;
            }
            return total;
        }

        /// <summary>"simulate raid" link on the tile: build.php?id=39&amp;tt=3&amp;screen=combatSimulator&amp;kid=...</summary>
        public static ILocator GetSimulatorLink(IPage page)
        {
            return page.Locator("a[href*='screen=combatSimulator'][href*='kid=']").First;
        }

        public static ILocator GetRaidLink(IPage page)
        {
            return page.GetByRole(AriaRole.Link, new() { Name = "Raid unoccupied oasis" }).First;
        }

        public static ILocator GetTroopTable(IPage page)
        {
            return page.Locator("table#troops");
        }

        public static ILocator GetTroopInput(IPage page, int troopIndex)
        {
            return page.Locator($"table#troops input[name=\"troop[t{troopIndex}]\"]");
        }

        public static async Task<int> ReadAvailable(ILocator input)
        {
            if (await input.CountAsync() == 0) return 0;

            var field = input.First;
            if (await field.IsDisabledAsync()) return 0;

            var link = field.Locator("xpath=..").Locator("a");
            if (await link.CountAsync() == 0) return 0;

            var digits = new string((await link.First.InnerTextAsync()).Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out var amount) ? amount : 0;
        }

        public static ILocator GetSendButton(IPage page)
        {
            return page.Locator("form:has(table#troops) button#ok");
        }

        public static ILocator GetConfirmButton(IPage page)
        {
            return page.Locator($"xpath={ConfirmButtonXPath}");
        }
    }
}
