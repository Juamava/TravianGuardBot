namespace MainCore.Parsers
{
    public readonly record struct FormCheck(bool Ok, string Reason)
    {
        public static FormCheck Pass => new(true, "");

        public static FormCheck Fail(string reason) => new(false, reason);
    }

    /// <summary>
    /// Rally point "send troops" form with the hero
    /// (build.php?newdid=..&amp;id=39&amp;tt=2&amp;eventType=4&amp;targetMapId=..).
    /// Samples: "Examples HTML/Rally point raid form with hero.html" and
    /// "Examples HTML/Rally point confirmation hero raid.html".
    /// </summary>
    public static class RallyPointHeroParser
    {
        public static ILocator GetHeroInput(IPage page) => page.Locator("table#troops input[name='troop[t11]']");

        public static ILocator GetOtherTroopInputs(IPage page) => page.Locator("table#troops input[name^='troop[t']:not([name='troop[t11]'])");

        public static ILocator GetRaidRadio(IPage page) => page.Locator("form:has(table#troops) input[name='eventType'][value='4']");

        public static ILocator GetXInput(IPage page) => page.Locator("form:has(table#troops) input#xCoordInput");

        public static ILocator GetYInput(IPage page) => page.Locator("form:has(table#troops) input#yCoordInput");

        public static ILocator GetSendButton(IPage page) => CowOasisParser.GetSendButton(page);

        public static ILocator GetConfirmButton(IPage page) => page.Locator("form#troopSendForm button#confirmSendTroops");

        public static async Task<FormCheck> CheckSendForm(IPage page, int x, int y)
        {
            var hero = GetHeroInput(page);
            if (await hero.CountAsync() != 1) return FormCheck.Fail("no está el casillero del héroe");
            if (await hero.IsDisabledAsync()) return FormCheck.Fail("el héroe no está disponible");

            var available = await CowOasisParser.ReadAvailable(hero);
            if (available != 1) return FormCheck.Fail($"el héroe disponible es {available}, no 1");

            var raid = GetRaidRadio(page);
            if (await raid.CountAsync() != 1 || await raid.IsDisabledAsync()) return FormCheck.Fail("no se puede elegir atraco");

            return await CheckTarget(page, x, y);
        }

        public static async Task<FormCheck> CheckFilledForm(IPage page, int x, int y)
        {
            var hero = GetHeroInput(page);
            if (await hero.CountAsync() != 1 || (await hero.InputValueAsync()).Trim() != "1") return FormCheck.Fail("el héroe no quedó cargado con 1");

            foreach (var input in await GetOtherTroopInputs(page).AllAsync())
            {
                var value = (await input.InputValueAsync()).Trim();
                if (value is not ("" or "0")) return FormCheck.Fail("hay otras tropas cargadas además del héroe");
            }

            if (!await GetRaidRadio(page).IsCheckedAsync()) return FormCheck.Fail("no quedó elegido atraco");

            return await CheckTarget(page, x, y);
        }

        private static async Task<FormCheck> CheckTarget(IPage page, int x, int y)
        {
            var xInput = GetXInput(page);
            var yInput = GetYInput(page);
            if (await xInput.CountAsync() != 1 || await yInput.CountAsync() != 1) return FormCheck.Fail("no están las coordenadas");

            var formX = HeroLevelingRules.ParseNumber(await xInput.InputValueAsync());
            var formY = HeroLevelingRules.ParseNumber(await yInput.InputValueAsync());
            if (formX != x || formY != y) return FormCheck.Fail($"el destino del formulario ({formX}|{formY}) no es el oasis ({x}|{y})");

            return FormCheck.Pass;
        }

        /// <summary>
        /// Confirmation step after the first "Send" ("Examples HTML/Rally point confirmation hero raid.html"):
        /// form#troopSendForm with hidden eventType/x/y/troops[0][tN] plus the visible troop_details table.
        /// Both must say: raid (4), the oasis coordinates, hero = 1 and every other troop = 0.
        /// </summary>
        public static async Task<FormCheck> CheckConfirmation(IPage page, int x, int y)
        {
            var form = page.Locator("form#troopSendForm");
            if (await form.CountAsync() != 1) return FormCheck.Fail("no está el formulario de confirmación");

            async Task<int?> Hidden(string name)
            {
                var input = form.Locator($"input[type='hidden'][name='{name}']");
                if (await input.CountAsync() != 1) return null;
                return HeroLevelingRules.ParseNumber(await input.InputValueAsync());
            }

            if (await Hidden("eventType") != 4) return FormCheck.Fail("la confirmación no es un atraco");
            if (await Hidden("x") != x || await Hidden("y") != y) return FormCheck.Fail("el destino de la confirmación no es el oasis");

            for (var i = 1; i <= 10; i++)
            {
                if (await Hidden($"troops[0][t{i}]") != 0) return FormCheck.Fail($"la confirmación incluye la tropa t{i}");
            }
            if (await Hidden("troops[0][t11]") != 1) return FormCheck.Fail("la confirmación no manda al héroe");
            if (await form.Locator("input[type='hidden'][name^='troops[1]']").CountAsync() > 0) return FormCheck.Fail("la confirmación tiene más de un envío");

            // Visible table: the counts row is the tbody.units without unit icons.
            var counts = await form.Locator("table.troop_details tbody.units:not(:has(img.unit)) td.unit").AllInnerTextsAsync();
            if (counts.Count != 11) return FormCheck.Fail("no pude leer las tropas de la confirmación");
            for (var i = 0; i < 10; i++)
            {
                if (HeroLevelingRules.ParseNumber(counts[i]) != 0) return FormCheck.Fail("la confirmación muestra otras tropas");
            }
            if (HeroLevelingRules.ParseNumber(counts[10]) != 1) return FormCheck.Fail("la confirmación no muestra al héroe");

            if (await GetConfirmButton(page).CountAsync() != 1) return FormCheck.Fail("no está el botón de confirmar");
            return FormCheck.Pass;
        }
    }
}
