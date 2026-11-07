using System.Text.Json;

namespace MainCore.Parsers
{
    /// <summary>
    /// Reads the rally point for the attack evasion. The overview (build.php?gid=16&amp;tt=1) draws one
    /// table.troop_details per movement: the class says what it is (inAttack, inRaid, outSupply, ...),
    /// the headline link has the coordinates of the other village and tbody.infos has the arrival timer
    /// (span.timer value = seconds left). See "Examples HTML/Rally point overview hero home.html".
    /// </summary>
    public static class EvadeParser
    {
        /// <summary>Filter 1 = incoming movements, 2 = outgoing, 4 = own troops in other villages.</summary>
        public static string OverviewUrl(string server, int villageId, int filter) =>
            $"{server.TrimEnd('/')}/build.php?newdid={villageId}&gid=16&tt=1&filter={filter}";

        public static string SendUrl(string server, int villageId) => RallyPointTroops.SendPageUrl(server, villageId);

        /// <summary>The mission radio of the send form: 5 = reinforcement.</summary>
        public static ILocator GetReinforcement(IPage page) => page.Locator("input[name=\"eventType\"][value=\"5\"]");

        public static ILocator GetX(IPage page) => page.Locator("#xCoordInput, input[name=\"x\"]").First;

        public static ILocator GetY(IPage page) => page.Locator("#yCoordInput, input[name=\"y\"]").First;

        public static ILocator GetOverview(IPage page) => page.Locator(".rallyPointOverviewContainer, #build.gid16").First;

        /// <summary>Attacks and raids coming to this village, with their arrival time.</summary>
        public static async Task<List<IncomingAttack>> ReadIncoming(IPage page, DateTime now)
        {
            var rows = await ReadMovements(page);
            return rows
                .Where(x => x.kind is "inAttack" or "inRaid")
                .Where(x => x.seconds >= 0)
                .Select(x => new IncomingAttack(now.AddSeconds(x.seconds), x.from ?? "", x.kind == "inRaid"))
                .OrderBy(x => x.Arrival)
                .ToList();
        }

        /// <summary>
        /// Clicks "cancel" on the outgoing reinforcement to (x|y) that arrives last, which is the one just sent.
        /// False when there is no such movement or it cannot be cancelled anymore.
        /// </summary>
        public static async Task<bool> ClickCancel(IPage page, int x, int y)
        {
            return await page.EvaluateAsync<bool>(Wrap("""
                ([x, y]) => {
                    const tables = movements().filter(row => row.kind === 'outSupply' && row.x === x && row.y === y && row.abort);
                    if (tables.length === 0) return false;
                    tables.sort((a, b) => b.seconds - a.seconds);
                    tables[0].abort.click();
                    return true;
                }
                """), new[] { x, y });
        }

        /// <summary>Opens "send back" for own troops staying in (x|y). False when they are not listed.</summary>
        public static async Task<bool> ClickSendBack(IPage page, int x, int y)
        {
            return await page.EvaluateAsync<bool>(Wrap("""
                ([x, y]) => {
                    const back = /send back|return troops|zur(ü|u)ck|devolver|retirar|regresar|enviar de vuelta|traer de vuelta/i;
                    // Only troops staying in that village: a movement (inReturn, outSupply, ...) is not them.
                    const rows = movements().filter(row => row.x === x && row.y === y && !/^(in|out)[A-Z]/.test(row.kind));
                    for (const row of rows) {
                        const nodes = [...row.table.querySelectorAll('tbody a[href], tbody button')];
                        const node = nodes.find(n => /[?&]w=\d+/.test(n.getAttribute('href') || '') || /[?&]w=\d+/.test(n.getAttribute('onclick') || ''))
                            || row.table.querySelector('.sback a, .sback button')
                            || nodes.find(n => back.test((n.innerText || n.value || n.title || '').trim()));
                        if (node) { node.click(); return true; }
                    }
                    return false;
                }
                """), new[] { x, y });
        }

        /// <summary>The "send back" form: fills every unit to the maximum and submits.</summary>
        public static async Task<bool> SubmitSendBack(IPage page)
        {
            return await page.EvaluateAsync<bool>("""
                () => {
                    const form = [...document.querySelectorAll('#build form, form')].find(f => f.querySelector('button[type="submit"], input[type="submit"]'));
                    if (!form) return false;
                    form.querySelectorAll('input[type="text"][name^="t"], input[name^="troop"]').forEach(input => {
                        const cell = input.closest('td');
                        const max = (cell?.querySelector('a, span.none')?.textContent || '').replace(/[^0-9]/g, '');
                        if (max !== '') input.value = max;
                    });
                    const button = form.querySelector('button[type="submit"]#btn_ok, button[type="submit"].green, button[type="submit"], input[type="submit"]');
                    if (!button) return false;
                    button.click();
                    return true;
                }
                """);
        }

        /// <summary>The travel time the confirmation page shows ("In 0:04:47 hours"). -1 when it is not there.</summary>
        public static async Task<int> ReadTravelSeconds(IPage page)
        {
            var node = page.Locator("#troopSendForm #in, #troopSendForm .in").First;
            if (await node.CountAsync() == 0) return -1;
            return EvadeRules.ParseDuration(await node.InnerTextAsync());
        }

        /// <summary>Puts the amount at home in every troop input (t1-t10, and t11 for the hero when asked).</summary>
        public static async Task<int> FillAllTroops(IPage page, bool hero)
        {
            return await page.EvaluateAsync<int>("""
                (hero) => {
                    let total = 0;
                    document.querySelectorAll('table#troops input[name^="troop[t"]').forEach(input => {
                        const match = /troop\[t(\d+)\]/.exec(input.getAttribute('name') || '');
                        const index = match ? parseInt(match[1], 10) : 0;
                        if (index < 1 || index > 11 || (index === 11 && !hero)) return;
                        const cell = input.closest('td');
                        const digits = (cell?.querySelector('a, span.none')?.textContent || '').replace(/[^0-9]/g, '');
                        const amount = input.disabled || digits === '' ? 0 : parseInt(digits, 10);
                        if (amount <= 0) return;
                        input.value = String(amount);
                        input.dispatchEvent(new Event('input', { bubbles: true }));
                        input.dispatchEvent(new Event('change', { bubbles: true }));
                        total += amount;
                    });
                    return total;
                }
                """, hero);
        }

        /// <summary>Runs <paramref name="function"/> with the movement helpers in scope.</summary>
        private static string Wrap(string function) => "(arg) => {\n" + Script + "\nreturn (" + function.Trim() + ")(arg);\n}";

        private const string Script = """
            const coordinate = node => {
                if (!node) return null;
                const text = (node.textContent || '').replace(/[−–]/g, '-').replace(/[^0-9-]/g, '');
                const value = parseInt(text, 10);
                return Number.isNaN(value) ? null : value;
            };
            const movements = () => [...document.querySelectorAll('table.troop_details')].map(table => {
                const kinds = ['inAttack', 'inRaid', 'inSupply', 'inReturn', 'outAttack', 'outRaid', 'outSupply', 'outSpy', 'inSpy'];
                const kind = kinds.find(k => table.classList.contains(k)) || [...table.classList].find(c => c !== 'troop_details') || '';
                const headline = table.querySelector('thead .troopHeadline') || table.querySelector('thead');
                const timer = table.querySelector('tbody.infos .in .timer, tbody.infos .timer[counting="down"], .in .timer');
                const raw = timer ? (timer.getAttribute('value') ?? timer.getAttribute('data-value')) : null;
                const seconds = raw !== null && raw !== '' && !Number.isNaN(parseInt(raw, 10)) ? parseInt(raw, 10) : -1;
                const place = { x: coordinate(headline?.querySelector('.coordinateX')), y: coordinate(headline?.querySelector('.coordinateY')) };
                if (place.x === null || place.y === null) {
                    // Some headlines only link the village: karte.php?x=..&y=.. or karte.php?d=<map id> on a 401 × 401 map.
                    const href = headline?.querySelector('a[href*="karte.php"]')?.getAttribute('href') || '';
                    const xy = /[?&]x=(-?\d+).*[?&]y=(-?\d+)/.exec(href);
                    const d = /[?&]d=(\d+)/.exec(href);
                    if (xy) { place.x = parseInt(xy[1], 10); place.y = parseInt(xy[2], 10); }
                    else if (d) { const id = parseInt(d[1], 10) - 1; place.x = (id % 401) - 200; place.y = 200 - Math.floor(id / 401); }
                }
                const abort = table.querySelector('.abort button, .abort a, a[href*="abort="], button[onclick*="abort"]');
                return {
                    table,
                    kind,
                    from: ((table.querySelector('thead .role') || {}).innerText || '').replace(/\s+/g, ' ').trim(),
                    x: place.x,
                    y: place.y,
                    seconds,
                    abort,
                };
            });
            """;

        private static async Task<List<MovementJson>> ReadMovements(IPage page)
        {
            var json = await page.EvaluateAsync<string>(Wrap("""
                () => JSON.stringify(movements().map(row => ({ kind: row.kind, from: row.from, x: row.x, y: row.y, seconds: row.seconds, canAbort: !!row.abort })))
                """));
            return JsonSerializer.Deserialize<List<MovementJson>>(json ?? "[]") ?? [];
        }

#pragma warning disable IDE1006 // JSON names from the page script
        private sealed class MovementJson
        {
            public string? kind { get; set; }
            public string? from { get; set; }
            public int? x { get; set; }
            public int? y { get; set; }
            public int seconds { get; set; }
            public bool canAbort { get; set; }
        }
#pragma warning restore IDE1006
    }
}
