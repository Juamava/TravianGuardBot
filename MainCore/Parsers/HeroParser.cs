using System.Globalization;
using System.Text.Json;

namespace MainCore.Parsers
{
    /// <summary>
    /// Hero state. Samples: "Examples HTML/Hero attributes.html" (/hero/attributes) and
    /// "Examples HTML/Village overview hero home.html" (top bar with div.heroStatus).
    /// </summary>
    public static class HeroParser
    {
        public const string AttributesPath = "/hero/attributes";

        /// <summary>
        /// True only when the top bar shows the "home" icon. Moving, adventuring, dead or
        /// an unreadable top bar all count as "not home".
        /// </summary>
        public static async Task<bool> IsHeroHome(IPage page)
        {
            var status = page.Locator("#topBarHero div.heroStatus > a");
            if (await status.CountAsync() == 0) return false;
            return await status.First.Locator("i.heroHome").CountAsync() > 0;
        }

        /// <summary>
        /// Village id (newdid) of the hero's home, read from the hero status link
        /// (/build.php?newdid=28876&amp;id=39&amp;&amp;tt=2).
        /// </summary>
        public static async Task<int?> GetHeroVillageId(IPage page)
        {
            var status = page.Locator("#topBarHero div.heroStatus > a");
            if (await status.CountAsync() == 0) return null;
            var href = await status.First.GetAttributeAsync("href");
            return ParseNewdid(href);
        }

        public static int? ParseNewdid(string? href)
        {
            if (string.IsNullOrEmpty(href)) return null;
            var index = href.IndexOf("newdid=", StringComparison.OrdinalIgnoreCase);
            if (index < 0) return null;
            var digits = new string(href[(index + 7)..].TakeWhile(char.IsDigit).ToArray());
            return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null;
        }

        public static ILocator GetAttributesBox(IPage page)
        {
            return page.Locator("#content.heroV2Attributes .attributeBox .stats").First;
        }

        private record struct RawStats(string? Title, string? Health, string? Experience, string? Speed, string? Power, string? OffBonus);

        public static async Task<HeroStats> ReadStats(IPage page)
        {
            var json = await page.EvaluateAsync<JsonElement>(@"() => {
                const valueAfter = (iconClass) => {
                    const icon = document.querySelector('#content.heroV2Attributes .attributeBox .stats i.' + iconClass);
                    if (!icon) return null;
                    let node = icon.closest('.name');
                    while (node && (node = node.nextElementSibling)) {
                        if (node.classList.contains('name')) return null;
                        if (node.classList.contains('value')) return (node.innerText || node.textContent || '').trim();
                    }
                    return null;
                };
                const input = (name) => {
                    const el = document.querySelector('#content.heroV2Attributes input[name=""' + name + '""]');
                    return el ? el.value : null;
                };
                const title = document.querySelector('#content.heroV2Attributes h1.titleInHeader, h1.titleInHeader');
                const speed = document.querySelector('#content.heroV2Attributes .attributeBox .stats .speedValue strong');
                return {
                    Title: title ? title.textContent.trim() : null,
                    Health: valueAfter('attributeHealth_medium'),
                    Experience: valueAfter('attributeExperience_medium'),
                    Speed: speed ? speed.textContent.trim() : null,
                    Power: input('power'),
                    OffBonus: input('offBonus'),
                };
            }");

            var raw = JsonSerializer.Deserialize<RawStats>(json.GetRawText());
            return new HeroStats(
                Level: HeroLevelingRules.ParseLevel(raw.Title),
                HpPercent: ValidPercent(HeroLevelingRules.ParseNumber(raw.Health)),
                Experience: HeroLevelingRules.ParseNumber(raw.Experience),
                SpeedFieldsPerHour: HeroLevelingRules.ParseNumber(raw.Speed),
                StrengthPoints: HeroLevelingRules.ParseNumber(raw.Power),
                OffBonusPoints: HeroLevelingRules.ParseNumber(raw.OffBonus));
        }

        private static int? ValidPercent(int? value)
        {
            return value is >= 0 and <= 100 ? value : null;
        }
    }
}
