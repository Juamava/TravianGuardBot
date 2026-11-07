using System.Text.Json;

namespace MainCore.Parsers
{
    /// <summary>A troop slot of the rally point send form: letter, name and how many are at home.</summary>
    public sealed record HomeTroop(char Letter, int Index, string Name, int Available);

    /// <summary>
    /// Reads table#troops of the rally point "send troops" form (build.php?gid=16&amp;tt=2): each cell has
    /// the unit image (alt = name), the input troop[tN] and the available amount next to it.
    /// </summary>
    public static class RallyPointTroops
    {
        public static string SendPageUrl(string server, int villageId) => $"{server.TrimEnd('/')}/build.php?gid=16&tt=2&newdid={villageId}";

        public static async Task<List<HomeTroop>> Read(IPage page)
        {
            var json = await page.EvaluateAsync<string>("""
                () => JSON.stringify([...document.querySelectorAll('table#troops input[name^="troop[t"]')].map(input => {
                    const cell = input.closest('td');
                    const match = /troop\[t(\d+)\]/.exec(input.getAttribute('name') || '');
                    const amountNode = cell?.querySelector('a, span.none');
                    const digits = (amountNode?.textContent || '').replace(/[^0-9]/g, '');
                    return {
                        index: match ? parseInt(match[1], 10) : 0,
                        name: (cell?.querySelector('img.unit')?.getAttribute('alt') || '').trim(),
                        available: input.disabled || digits === '' ? 0 : parseInt(digits, 10),
                    };
                }))
                """);
            var rows = JsonSerializer.Deserialize<List<TroopJson>>(json ?? "[]") ?? [];
            return rows
                // Troop plans only know A-H (t1-t8); chiefs, settlers and the hero are not sent from here.
                .Where(x => x.index >= 1 && x.index <= 8)
                .Select(x => new HomeTroop(Letter(x.index), x.index, x.name ?? "", x.available))
                .OrderBy(x => x.Index)
                .ToList();
        }

        /// <summary>Rally point letters: A-H for t1-t8, the same as <see cref="CowTroopPlan"/>.</summary>
        public static char Letter(int index) => (char)('A' + index - 1);

        /// <summary>The mission radio of the send form: 3 = normal attack, 4 = raid.</summary>
        public static ILocator GetMission(IPage page, bool raid) => page.Locator($"input[name=\"eventType\"][value=\"{(raid ? 4 : 3)}\"]");

#pragma warning disable IDE1006 // JSON names from the page script
        private sealed class TroopJson
        {
            public int index { get; set; }
            public string? name { get; set; }
            public int available { get; set; }
        }
#pragma warning restore IDE1006
    }
}
