using System.Text.Json;

namespace MainCore.Parsers
{
    /// <summary>
    /// Rally point combat simulator (build.php?id=39&amp;tt=3&amp;screen=combatSimulator&amp;kid=...).
    /// Samples: "Examples HTML/Combat simulator form.html",
    /// "Examples HTML/Combat simulator result hero dies.html" and
    /// "Examples HTML/Combat simulator result hero survives.html".
    /// The form is prefilled with the village troops, the hero (checkbox + real health,
    /// strength points and off bonus points) and the animals of the oasis.
    /// </summary>
    public static class CombatSimulatorParser
    {
        public static string BuildUrl(string server, int villageId, int kid)
        {
            var origin = new Uri(server);
            return $"{origin.Scheme}://{origin.Host}/build.php?newdid={villageId}&id=39&tt=3&screen=combatSimulator&kid={kid}";
        }

        public static int? ParseKid(string? href)
        {
            if (string.IsNullOrEmpty(href)) return null;
            var index = href.IndexOf("kid=", StringComparison.OrdinalIgnoreCase);
            if (index < 0) return null;
            var digits = new string(href[(index + 4)..].TakeWhile(char.IsDigit).ToArray());
            return int.TryParse(digits, out var kid) ? kid : null;
        }

        public static ILocator GetAttackerRole(IPage page) => page.Locator("div.role.attacker");

        public static ILocator GetDefenderRole(IPage page) => page.Locator("div.role.defender");

        public static ILocator GetVillageSelect(IPage page) => GetAttackerRole(page).Locator("select[name='village']");

        public static ILocator GetRaidRadio(IPage page) => GetAttackerRole(page).Locator("input[name='movement'][value='raid']");

        /// <summary>Attacker troop count inputs t1..t10 (first row, they carry data-t).</summary>
        public static ILocator GetAttackerUnitInputs(IPage page) => GetAttackerRole(page).Locator("tr input[data-t]");

        public static ILocator GetAttackerHeroCheckbox(IPage page) => GetAttackerRole(page).Locator("input.hero[name='hero']");

        public static ILocator GetHeroHealthInput(IPage page) => GetAttackerRole(page).Locator(".heroAttributes input[name='health']");

        public static ILocator GetHeroStrengthInput(IPage page) => GetAttackerRole(page).Locator(".heroAttributes input[name='strength']");

        public static ILocator GetHeroOffPointsInput(IPage page) => GetAttackerRole(page).Locator(".heroAttributes input[name='offPoints']");

        public static ILocator GetSimulateButton(IPage page) => page.Locator("button#simulate");

        public static ILocator GetAttackResult(IPage page) => page.Locator("div.results div.result.attack");

        /// <summary>Sum of the defender count inputs (first row, the animals of the oasis).</summary>
        public static async Task<int?> ReadDefenderTotal(IPage page)
        {
            // Nature has 10 units and the defender side has a single row of unit1..unit10.
            var values = await GetDefenderRole(page).Locator("input[name^='unit']")
                .EvaluateAllAsync<string[]>("els => els.map(e => e.value)");
            if (values.Length != 10) return null;

            var total = 0;
            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value)) continue;
                var number = HeroLevelingRules.ParseNumber(value);
                if (number is null || number < 0) return null;
                total += number.Value;
            }
            return total;
        }

        private sealed record RawRows(string[]? AttackerInitial, string[]? AttackerLoss, string[]? DefenderInitial, string[]? DefenderLoss, string[]? HealthLines);

        public static async Task<SimulationNumbers?> ReadResult(IPage page)
        {
            if (await GetAttackResult(page).CountAsync() != 1) return null;

            var json = await page.EvaluateAsync<JsonElement>(@"() => {
                const row = (side, cls) => {
                    const tr = document.querySelector('div.results div.result.' + side + ' tr.' + cls);
                    if (!tr) return null;
                    return Array.from(tr.querySelectorAll('td')).map(td => (td.innerText || td.textContent || '').trim());
                };
                const lines = Array.from(document.querySelectorAll('div.results div.result.attack tr.combatReportLine'))
                    .filter(tr => tr.querySelector('i.hero_health'))
                    .map(tr => (tr.innerText || tr.textContent || '').trim());
                return {
                    AttackerInitial: row('attack', 'troopsInitial'),
                    AttackerLoss: row('attack', 'troopsLoss'),
                    DefenderInitial: row('defence', 'troopsInitial'),
                    DefenderLoss: row('defence', 'troopsLoss'),
                    HealthLines: lines,
                };
            }");

            var raw = JsonSerializer.Deserialize<RawRows>(json.GetRawText());
            if (raw is null) return null;
            return ToNumbers(raw.AttackerInitial, raw.AttackerLoss, raw.DefenderInitial, raw.DefenderLoss, raw.HealthLines);
        }

        public static SimulationNumbers? ToNumbers(string[]? attackerInitial, string[]? attackerLoss, string[]? defenderInitial, string[]? defenderLoss, string[]? healthLines)
        {
            if (attackerInitial is null || attackerLoss is null || defenderInitial is null || defenderLoss is null) return null;

            // Exactly one hero health line, otherwise we do not trust it.
            var (before, after) = healthLines is { Length: 1 } ? HeroLevelingRules.ParseHealthLine(healthLines[0]) : (null, null);

            return new SimulationNumbers(
                [.. attackerInitial.Select(HeroLevelingRules.ParseNumber)],
                [.. attackerLoss.Select(HeroLevelingRules.ParseNumber)],
                [.. defenderInitial.Select(HeroLevelingRules.ParseNumber)],
                [.. defenderLoss.Select(HeroLevelingRules.ParseNumber)],
                before,
                after);
        }
    }
}
