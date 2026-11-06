using System.Globalization;
using System.Text.RegularExpressions;

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
            var sendTroops = GetSendTroopsLink(page);

            try
            {
                await unoccupied.Or(occupied).Or(sendTroops).First.WaitForAsync(new() { Timeout = 8_000 });
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

        public static ILocator GetSendTroopsLink(IPage page)
        {
            return page.GetByRole(AriaRole.Link, new() { Name = "Send troops" })
                .Or(page.GetByRole(AriaRole.Link, new() { Name = "Enviar tropas" }))
                .First;
        }

        public readonly record struct TileDetails(
            string Title,
            string Kind,
            string Owner,
            string Tribe,
            string Alliance,
            int Population,
            double Distance,
            bool HasSendTroops);

        public static async Task<TileDetails> ReadDetails(IPage page)
        {
            var raw = await page.EvaluateAsync<TileRaw>(@"() => {
                const marker = /owner|propietario|dueño|population|población|unoccupied oasis|occupied oasis|send troops|enviar tropas|raid unoccupied/i;
                const dialog = [...document.querySelectorAll('#map_details, #tileDetails, form, .dialog')]
                    .find(node => marker.test(node.innerText || ''));
                const root = dialog || document.body;
                const fields = {};
                root.querySelectorAll('tr').forEach(row => {
                    const cells = [...row.querySelectorAll('th, td')]
                        .map(cell => (cell.innerText || '').replace(/\s+/g, ' ').trim())
                        .filter(Boolean);
                    if (cells.length === 0) return;
                    const label = cells[0].replace(/:$/, '');
                    const value = cells.length > 1 ? cells[cells.length - 1] : '';
                    if (!fields[label]) fields[label] = value;
                });
                const title = ((root.querySelector('h1') || {}).innerText || '').replace(/\s+/g, ' ').trim();
                const links = [...root.querySelectorAll('a')].map(link => (link.innerText || '').replace(/\s+/g, ' ').trim());
                const text = (root.innerText || '').toLowerCase();
                return { fields, title, links, text };
            }");

            return FromFields(raw?.Fields, raw?.Title ?? "", raw?.Links, raw?.Text ?? "");
        }

        public static TileDetails FromFields(IReadOnlyDictionary<string, string>? fields, string title, IReadOnlyList<string>? links, string pageText = "")
        {
            fields ??= new Dictionary<string, string>();
            links ??= [];
            var owner = Field(fields, "Owner", "Propietario", "Dueño");
            var tribe = Field(fields, "Tribe", "Tribu");
            var alliance = Field(fields, "Alliance", "Alianza");
            var population = ParsePopulation(Field(fields, "Population", "Población"));
            var distance = ParseDistance(Field(fields, "Distance", "Distancia"));
            var text = pageText ?? "";
            var linkText = string.Join(" ", links);
            var hasSend = links.Any(link => link.Contains("Send troops", StringComparison.OrdinalIgnoreCase) || link.Contains("Enviar tropas", StringComparison.OrdinalIgnoreCase));
            var isOasis = text.Contains("unoccupied oasis", StringComparison.OrdinalIgnoreCase)
                || text.Contains("occupied oasis", StringComparison.OrdinalIgnoreCase)
                || linkText.Contains("oasis", StringComparison.OrdinalIgnoreCase);
            var kind = isOasis ? "oasis" : hasSend || owner.Length > 0 || population >= 0 ? "aldea" : "";
            return new TileDetails(title.Trim(), kind, owner, tribe, alliance, population, distance, hasSend);
        }

        public static bool TryReadCoordinates(string url, out int x, out int y)
        {
            x = 0;
            y = 0;
            if (string.IsNullOrWhiteSpace(url)) return false;
            var matchX = Regex.Match(url, @"[?&]x=(-?\d+)", RegexOptions.IgnoreCase);
            var matchY = Regex.Match(url, @"[?&]y=(-?\d+)", RegexOptions.IgnoreCase);
            if (!matchX.Success || !matchY.Success) return false;
            return int.TryParse(matchX.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out x)
                && int.TryParse(matchY.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out y);
        }

        public static double TravelDistance(int fromX, int fromY, int toX, int toY)
        {
            var dx = fromX - toX;
            var dy = fromY - toY;
            return Math.Round(Math.Sqrt((dx * dx) + (dy * dy)), 2);
        }

        public static bool CopyOnto(CowVillage row, TileDetails details, string url, int? originX, int? originY, bool replaceName)
        {
            var changed = false;
            var title = string.IsNullOrWhiteSpace(details.Title) ? "" : details.Title.Trim();
            if (title.Length > 0 && title != MissingName && (replaceName || string.IsNullOrWhiteSpace(row.Name) || row.Name == MissingName) && row.Name != title)
            {
                row.Name = title;
                changed = true;
            }

            changed |= SetText(value => row.Kind = value, row.Kind, details.Kind);
            changed |= SetText(value => row.Owner = value, row.Owner, details.Owner);
            changed |= SetText(value => row.Tribe = value, row.Tribe, details.Tribe);
            changed |= SetText(value => row.Alliance = value, row.Alliance, details.Alliance);
            if (details.Population >= 0 && row.Population != details.Population)
            {
                row.Population = details.Population;
                changed = true;
            }

            int? x = null;
            int? y = null;
            if (TryReadCoordinates(url, out var parsedX, out var parsedY))
            {
                x = parsedX;
                y = parsedY;
            }

            var distance = details.Distance;
            if (distance < 0 && row.Distance < 0 && x is int targetX && y is int targetY && originX is int fromX && originY is int fromY)
                distance = TravelDistance(fromX, fromY, targetX, targetY);
            if (distance >= 0 && Math.Abs(row.Distance - distance) > 0.001)
            {
                row.Distance = distance;
                changed = true;
            }

            if (x is int coordX && row.CoordX != coordX)
            {
                row.CoordX = coordX;
                changed = true;
            }

            if (y is int coordY && row.CoordY != coordY)
            {
                row.CoordY = coordY;
                changed = true;
            }

            return changed;
        }

        private static bool SetText(Action<string> assign, string current, string next)
        {
            if (string.IsNullOrWhiteSpace(next) || current == next) return false;
            assign(next);
            return true;
        }

        private static string Field(IReadOnlyDictionary<string, string> fields, params string[] names)
        {
            foreach (var pair in fields)
            {
                foreach (var name in names)
                {
                    if (pair.Key.Equals(name, StringComparison.OrdinalIgnoreCase)) return (pair.Value ?? "").Trim();
                }
            }

            return "";
        }

        private static int ParsePopulation(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return -1;
            var digits = new string(text.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var population) ? population : -1;
        }

        private static double ParseDistance(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return -1;
            var match = Regex.Match(text, @"\d+(?:[.,]\d+)?");
            if (!match.Success) return -1;
            return double.TryParse(match.Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var distance) ? distance : -1;
        }

        private sealed class TileRaw
        {
            public Dictionary<string, string>? Fields { get; set; }
            public string? Title { get; set; }
            public List<string>? Links { get; set; }
            public string? Text { get; set; }
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
