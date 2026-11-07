using System.Globalization;
using System.Text.Json;

namespace MainCore.Parsers
{
    public sealed record AttackUnit(string Name, int Sent, int Dead)
    {
        public int Survived => Math.Max(0, Sent - Dead);
    }

    /// <summary>What an attack/raid report says (/report/offensive?id=...).</summary>
    public sealed record AttackReport(
        int? X,
        int? Y,
        string VillageName,
        DateTime? TimeUtc,
        bool AttackerWon,
        IReadOnlyList<AttackUnit> Attacker,
        IReadOnlyList<AttackUnit> Defender,
        int BountyWood,
        int BountyClay,
        int BountyIron,
        int BountyCrop,
        int Carried,
        int Capacity)
    {
        public int Sent => Attacker.Sum(x => x.Sent);
        public int Lost => Attacker.Sum(x => x.Dead);
        public int Bounty => Math.Max(0, BountyWood) + Math.Max(0, BountyClay) + Math.Max(0, BountyIron) + Math.Max(0, BountyCrop);
    }

    /// <summary>
    /// Reads an attack report. Units: the first tbody.units of each role has the unit images (alt = name),
    /// then a row with i.troopCount_small (sent) and one with i.troopDead_small (dead). The bounty is in
    /// table.additionalInformation (lumber/clay/iron/crop values and "carried/capacity").
    /// The report time is printed in server time; #servertime tells how far that is from UTC.
    /// </summary>
    public static class AttackReportParser
    {
        public const string ReportsPath = "/report/offensive";

        public static string ListPageUrl(string root, int page) => page <= 1 ? root + ReportsPath : $"{root}{ReportsPath}?page={page}";

        public static async Task<AttackReport?> Read(IPage page, int worldSize = 200)
        {
            var json = await page.EvaluateAsync<string>("""
                () => {
                    const wrapper = document.querySelector('#reportWrapper');
                    if (!wrapper) return '';
                    const number = text => {
                        const clean = (text || '').replace(/[^0-9]/g, '');
                        return clean === '' ? 0 : parseInt(clean, 10);
                    };
                    const units = role => {
                        if (!role) return [];
                        const names = [...role.querySelectorAll('tbody.units img.unit')].map(img => (img.getAttribute('alt') || '').trim());
                        const row = icon => [...role.querySelectorAll('tbody.units tr')].find(tr => tr.querySelector('i.' + icon));
                        const values = tr => tr ? [...tr.querySelectorAll('td.unit')].map(cell => number(cell.textContent)) : [];
                        const sent = values(row('troopCount_small'));
                        const dead = values(row('troopDead_small'));
                        return sent.map((value, index) => ({ name: names[index] || ('t' + (index + 1)), sent: value, dead: dead[index] || 0 }))
                            .filter(unit => unit.sent > 0 || unit.dead > 0);
                    };
                    const attacker = wrapper.querySelector('.role.attacker');
                    const defenders = [...wrapper.querySelectorAll('.role.defender')];
                    const village = defenders[0]?.querySelector('a.village');
                    const bountyValue = name => {
                        const icon = attacker?.querySelector('table.additionalInformation i.' + name);
                        return icon ? number(icon.parentElement?.querySelector('span.value')?.textContent) : 0;
                    };
                    const carry = (attacker?.querySelector('table.additionalInformation .carry')?.textContent || '').replace(/[^0-9/]/g, '').split('/');
                    const outcome = wrapper.querySelector('.victory img.attacker')?.className || '';
                    const server = document.querySelector('#servertime');
                    return JSON.stringify({
                        villageHref: village?.getAttribute('href') || '',
                        villageName: (village?.textContent || '').trim(),
                        time: (wrapper.querySelector('.header .time .text')?.textContent || '').trim(),
                        serverText: (server?.textContent || '').replace(/[^0-9:]/g, ' ').trim(),
                        serverUnix: number(server?.querySelector('.timer')?.getAttribute('value')),
                        won: /\bwon\b/.test(outcome),
                        attacker: units(attacker),
                        defender: defenders.flatMap(units),
                        wood: bountyValue('lumber'),
                        clay: bountyValue('clay'),
                        iron: bountyValue('iron'),
                        crop: bountyValue('crop'),
                        carried: carry.length === 2 ? number(carry[0]) : 0,
                        capacity: carry.length === 2 ? number(carry[1]) : 0,
                    });
                }
                """);
            if (string.IsNullOrWhiteSpace(json)) return null;
            var data = JsonSerializer.Deserialize<ReportJson>(json);
            if (data is null) return null;

            int? x = null;
            int? y = null;
            if (ScoutParser.TryReadMapId(data.villageHref, out var mapId))
            {
                var (cx, cy) = ScoutRules.FromMapId(mapId, worldSize);
                x = cx;
                y = cy;
            }

            var offset = ServerOffset(data.serverText ?? "", data.serverUnix);
            var time = ParseServerTime(data.time ?? "");
            DateTime? utc = time is null ? null : DateTime.SpecifyKind(time.Value - offset, DateTimeKind.Utc);

            return new AttackReport(
                x, y,
                data.villageName ?? "",
                utc,
                data.won,
                (data.attacker ?? []).Select(u => new AttackUnit(u.name ?? "", u.sent, u.dead)).ToList(),
                (data.defender ?? []).Select(u => new AttackUnit(u.name ?? "", u.sent, u.dead)).ToList(),
                data.wood, data.clay, data.iron, data.crop,
                data.carried, data.capacity);
        }

        /// <summary>"07.11.26, 02:16:11" (day.month.year) as a server clock time.</summary>
        public static DateTime? ParseServerTime(string text)
        {
            var clean = new string(text.Where(c => char.IsDigit(c) || c is '.' or ':' or ',' or ' ' or '/').ToArray()).Trim();
            string[] formats = ["dd.MM.yy, HH:mm:ss", "dd.MM.yy, H:mm:ss", "dd.MM.yyyy, HH:mm:ss", "dd/MM/yy, HH:mm:ss", "dd.MM.yy HH:mm:ss"];
            return DateTime.TryParseExact(clean, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? value : null;
        }

        /// <summary>
        /// Server clock minus UTC, from the top bar: its text is the server clock and its timer value is the
        /// UTC unix time. Rounded to 15 minutes; zero when either is missing.
        /// </summary>
        public static TimeSpan ServerOffset(string serverText, long serverUnix)
        {
            if (serverUnix <= 0) return TimeSpan.Zero;
            var parts = serverText.Split([' ', ':'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3
                || !int.TryParse(parts[0], out var h)
                || !int.TryParse(parts[1], out var m)
                || !int.TryParse(parts[2], out var s)) return TimeSpan.Zero;

            var shown = h * 3600 + m * 60 + s;
            var utc = (int)(serverUnix % 86400);
            var diff = shown - utc;
            if (diff > 43200) diff -= 86400;
            if (diff < -43200) diff += 86400;
            var quarters = (int)Math.Round(diff / 900.0);
            return TimeSpan.FromMinutes(quarters * 15);
        }

#pragma warning disable IDE1006 // JSON names from the page script
        private sealed class ReportJson
        {
            public string? villageHref { get; set; }
            public string? villageName { get; set; }
            public string? time { get; set; }
            public string? serverText { get; set; }
            public long serverUnix { get; set; }
            public bool won { get; set; }
            public List<UnitJson>? attacker { get; set; }
            public List<UnitJson>? defender { get; set; }
            public int wood { get; set; }
            public int clay { get; set; }
            public int iron { get; set; }
            public int crop { get; set; }
            public int carried { get; set; }
            public int capacity { get; set; }
        }

        private sealed class UnitJson
        {
            public string? name { get; set; }
            public int sent { get; set; }
            public int dead { get; set; }
        }
#pragma warning restore IDE1006
    }
}
