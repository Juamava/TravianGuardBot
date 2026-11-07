using System.Globalization;
using System.Text.Json;

namespace MainCore.Parsers
{
    /// <summary>
    /// Reads /report/scouting, one scouting report and a player profile (/profile/{id}).
    /// The report list opens each report with a script click, so rows are clicked, not followed by URL.
    /// </summary>
    public static class ScoutParser
    {
        public const string ReportsPath = "/report/scouting";

        public sealed record ReportRow(int Index, string Subject, bool Unread);

        public static ILocator GetReportRows(IPage page) => page.Locator("#overview tbody tr:has(td.sub a)");

        public static ILocator GetReport(IPage page) => page.Locator("#reportWrapper .role.defender");

        public static async Task<List<ReportRow>> ReadReportRows(IPage page)
        {
            var json = await page.EvaluateAsync<string>("""
                () => JSON.stringify([...document.querySelectorAll('#overview tbody tr')]
                    .filter(tr => tr.querySelector('td.sub a'))
                    .map(tr => ({
                        subject: (tr.querySelector('td.sub')?.innerText || '').trim(),
                        unread: !!tr.querySelector('.messageStatusUnread') || tr.querySelector('td.sub')?.classList.contains('newMessage') === true,
                    })))
                """);
            var rows = JsonSerializer.Deserialize<List<RowJson>>(json ?? "[]") ?? [];
            return rows.Select((row, index) => new ReportRow(index, row.subject ?? "", row.unread)).ToList();
        }

        public static ILocator GetReportLink(IPage page, int index) => GetReportRows(page).Nth(index).Locator("td.sub a").First;

        public static async Task<ScoutReport?> ReadReport(IPage page, int worldSize = 200)
        {
            var json = await page.EvaluateAsync<string>("""
                () => {
                    const wrapper = document.querySelector('#reportWrapper');
                    if (!wrapper) return '';
                    const number = text => {
                        const clean = (text || '').replace(/[^0-9\-]/g, '');
                        return clean === '' ? null : parseInt(clean, 10);
                    };
                    const defenderRoles = [...wrapper.querySelectorAll('.role.defender')];
                    const first = defenderRoles[0];
                    const village = first?.querySelector('a.village');
                    const player = first?.querySelector('a.player');
                    let unknown = false;
                    let defenders = 0;
                    for (const role of defenderRoles) {
                        for (const row of role.querySelectorAll('tbody.units tr')) {
                            if (!row.querySelector('i.troopCount_small')) continue;
                            for (const cell of row.querySelectorAll('td.unit')) {
                                const text = cell.textContent.trim();
                                if (text === '?') { unknown = true; continue; }
                                defenders += number(text) || 0;
                            }
                        }
                    }
                    const attacker = wrapper.querySelector('.role.attacker');
                    const sent = [...(attacker?.querySelectorAll('tbody.units tr') || [])]
                        .filter(row => row.querySelector('i.troopCount_small'))
                        .flatMap(row => [...row.querySelectorAll('td.unit')].map(cell => number(cell.textContent) || 0))
                        .reduce((a, b) => a + b, 0);
                    const dead = [...(attacker?.querySelectorAll('tbody.units tr') || [])]
                        .filter(row => row.querySelector('i.troopDead_small'))
                        .flatMap(row => [...row.querySelectorAll('td.unit')].map(cell => number(cell.textContent) || 0))
                        .reduce((a, b) => a + b, 0);
                    const resource = name => {
                        const icon = attacker?.querySelector('table.additionalInformation i.' + name);
                        const value = icon?.parentElement?.querySelector('span.value');
                        const parsed = number(value?.textContent);
                        return parsed == null ? -1 : parsed;
                    };
                    const crannyIcon = attacker?.querySelector('table.additionalInformation i.building_small.type23');
                    const cranny = number(crannyIcon?.parentElement?.querySelector('span.value')?.textContent);
                    return JSON.stringify({
                        villageHref: village?.getAttribute('href') || '',
                        villageName: (village?.textContent || '').trim(),
                        playerHref: player?.getAttribute('href') || '',
                        playerName: (player?.textContent || '').trim(),
                        wood: resource('lumber'),
                        clay: resource('clay'),
                        iron: resource('iron'),
                        crop: resource('crop'),
                        cranny: cranny == null ? -1 : cranny,
                        defenders: unknown ? -1 : defenders,
                        unknown,
                        died: sent > 0 && dead >= sent,
                        time: (wrapper.querySelector('.header .time .text')?.textContent || '').trim(),
                    });
                }
                """);
            if (string.IsNullOrWhiteSpace(json)) return null;
            var data = JsonSerializer.Deserialize<ReportJson>(json);
            if (data is null) return null;

            int? x = null;
            int? y = null;
            if (TryReadMapId(data.villageHref, out var mapId))
            {
                var (cx, cy) = ScoutRules.FromMapId(mapId, worldSize);
                x = cx;
                y = cy;
            }
            else if (CowOasisParser.TryReadCoordinates(data.villageHref ?? "", out var px, out var py))
            {
                x = px;
                y = py;
            }

            var died = data.died || data.unknown;
            return new ScoutReport(
                x, y,
                data.villageName ?? "",
                data.playerName ?? "",
                data.playerHref ?? "",
                data.wood, data.clay, data.iron, data.crop, data.cranny,
                died ? -1 : data.defenders,
                died,
                data.time ?? "");
        }

        public static async Task<PlayerProfile?> ReadProfile(IPage page)
        {
            var json = await page.EvaluateAsync<string>("""
                () => {
                    const box = document.querySelector('#playerProfile .playerDetails');
                    if (!box) return '';
                    const details = {};
                    const ranks = [];
                    for (const row of box.querySelectorAll('tr')) {
                        const th = row.querySelector('th:not(.header)');
                        if (!th) continue;
                        const label = th.textContent.trim().replace(/:$/, '').toLowerCase();
                        const value = row.querySelector('td.value');
                        if (value) { ranks.push({ label, value: value.textContent.trim(), rank: (row.querySelector('td.rank')?.textContent || '').trim() }); continue; }
                        const td = row.querySelector('td');
                        if (td) details[label] = td.textContent.trim();
                    }
                    return JSON.stringify({ details, ranks });
                }
                """);
            if (string.IsNullOrWhiteSpace(json)) return null;
            var data = JsonSerializer.Deserialize<ProfileJson>(json);
            if (data is null) return null;

            var details = data.details ?? [];
            var ranks = data.ranks ?? [];
            string Detail(params string[] names) => details.FirstOrDefault(x => names.Any(name => x.Key.StartsWith(name, StringComparison.OrdinalIgnoreCase))).Value ?? "";
            // Population and points are in the value column; the hero level is in the rank column.
            int Rank(int order, bool rankColumn, params string[] names)
            {
                var row = ranks.FirstOrDefault(x => names.Any(name => x.label.StartsWith(name, StringComparison.OrdinalIgnoreCase)));
                row ??= order < ranks.Count ? ranks[order] : null;
                return FirstNumber(rankColumn ? row?.rank : row?.value);
            }

            var alliance = Detail("alliance", "alianza");
            if (alliance.Trim() == "-") alliance = "";
            return new PlayerProfile(
                Detail("tribe", "tribu", "pueblo"),
                alliance.Trim(),
                FirstNumber(Detail("villages", "aldeas")),
                FirstNumber(Detail("days playing", "días jugando", "dias jugando")),
                Rank(0, false, "population", "población", "poblacion"),
                Rank(1, false, "attacker", "atacante"),
                Rank(2, false, "defender", "defensor"),
                Rank(3, true, "hero", "héroe", "heroe"));
        }

        public static bool TryReadMapId(string? href, out int mapId)
        {
            mapId = 0;
            if (string.IsNullOrWhiteSpace(href)) return false;
            var marker = href.IndexOf("d=", StringComparison.OrdinalIgnoreCase);
            if (marker < 0) return false;
            var start = marker + 2;
            var end = start;
            while (end < href.Length && char.IsDigit(href[end])) end++;
            return end > start && int.TryParse(href[start..end], NumberStyles.Integer, CultureInfo.InvariantCulture, out mapId) && mapId > 0;
        }

        public static int FirstNumber(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return -1;
            var digits = new string(text.SkipWhile(c => !char.IsDigit(c)).TakeWhile(c => char.IsDigit(c) || c == ',' || c == '.' || c == '‭' || c == '‬').Where(char.IsDigit).ToArray());
            return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : -1;
        }

#pragma warning disable IDE1006 // JSON names from the page script
        private sealed class RowJson
        {
            public string? subject { get; set; }
            public bool unread { get; set; }
        }

        private sealed class ReportJson
        {
            public string? villageHref { get; set; }
            public string? villageName { get; set; }
            public string? playerHref { get; set; }
            public string? playerName { get; set; }
            public int wood { get; set; }
            public int clay { get; set; }
            public int iron { get; set; }
            public int crop { get; set; }
            public int cranny { get; set; }
            public int defenders { get; set; }
            public bool unknown { get; set; }
            public bool died { get; set; }
            public string? time { get; set; }
        }

        private sealed class ProfileJson
        {
            public Dictionary<string, string>? details { get; set; }
            public List<RankJson>? ranks { get; set; }
        }

        private sealed class RankJson
        {
            public string label { get; set; } = "";
            public string value { get; set; } = "";
            public string rank { get; set; } = "";
        }
#pragma warning restore IDE1006
    }
}
