namespace MainCore.Parsers
{
    /// <summary>An attack or raid coming to the village, as the rally point lists it.</summary>
    public sealed record IncomingAttack(DateTime Arrival, string From, bool Raid);

    /// <summary>
    /// Attacks that land close together. A "train" is many attacks sent so that they land every few
    /// seconds: they are one wave, and the troops stay away from the first to the last.
    /// </summary>
    public sealed record AttackWave(DateTime First, DateTime Last, int Count);

    /// <summary>
    /// The timing rules of the evasion, with no browser: group the attacks into waves and decide when
    /// the troops leave and when the reinforcement is cancelled so they come back after the last hit.
    /// </summary>
    public static class EvadeRules
    {
        /// <summary>Travian lets a movement be cancelled during its first 90 seconds. A few seconds are kept for the page to load.</summary>
        public const int CancelWindowSeconds = 80;

        /// <summary>The task opens the send form this long before the troops have to leave.</summary>
        public const int PrepareSeconds = 75;

        /// <summary>
        /// Splits the attacks into waves. An attack belongs to the current wave when it lands at most
        /// <paramref name="gapSeconds"/> after the previous one.
        /// </summary>
        public static List<AttackWave> GroupWaves(IEnumerable<IncomingAttack> attacks, int gapSeconds)
        {
            var gap = TimeSpan.FromSeconds(Math.Max(0, gapSeconds));
            var waves = new List<AttackWave>();
            DateTime? first = null;
            DateTime last = default;
            var count = 0;
            foreach (var arrival in attacks.Select(x => x.Arrival).OrderBy(x => x))
            {
                if (first is not null && arrival - last <= gap)
                {
                    last = arrival;
                    count++;
                    continue;
                }
                if (first is not null) waves.Add(new AttackWave(first.Value, last, count));
                first = arrival;
                last = arrival;
                count = 1;
            }
            if (first is not null) waves.Add(new AttackWave(first.Value, last, count));
            return waves;
        }

        /// <summary>
        /// Adds to the wave the attacks seen later that keep the train going: each one lands at most
        /// <paramref name="gapSeconds"/> after the previous one.
        /// </summary>
        public static AttackWave Extend(AttackWave wave, IEnumerable<IncomingAttack> attacks, int gapSeconds)
        {
            var gap = TimeSpan.FromSeconds(Math.Max(0, gapSeconds));
            var last = wave.Last;
            var count = wave.Count;
            foreach (var arrival in attacks.Select(x => x.Arrival).Where(x => x > wave.Last.AddSeconds(2)).OrderBy(x => x))
            {
                if (arrival - last > gap) break;
                last = arrival;
                count++;
            }
            return wave with { Last = last, Count = count };
        }

        /// <summary>When the troops have to leave for this wave.</summary>
        public static DateTime SendAt(AttackWave wave, int leadSeconds) => wave.First.AddSeconds(-Math.Max(1, leadSeconds));

        /// <summary>
        /// When to cancel a reinforcement that left at <paramref name="sentAt"/> so the troops get home
        /// <paramref name="safetySeconds"/> after the last attack of the wave. A cancelled movement goes back
        /// in the time it already travelled, so the troops are home at 2 × cancel − sent.
        /// Null when that moment is past the cancel window or the troops reach the ally first: then they stay
        /// with the ally and are sent back later.
        /// </summary>
        public static DateTime? CancelAt(AttackWave wave, DateTime sentAt, int safetySeconds, int travelSeconds)
        {
            var homeAt = wave.Last.AddSeconds(Math.Max(0, safetySeconds));
            var needed = sentAt.AddTicks((homeAt - sentAt).Ticks / 2);
            if (needed < sentAt) needed = sentAt;
            if (needed > sentAt.AddSeconds(CancelWindowSeconds)) return null;
            // A movement that already arrived cannot be cancelled. Keep 3 seconds for the click.
            if (travelSeconds > 0 && needed > sentAt.AddSeconds(travelSeconds - 3)) return null;
            return needed;
        }

        /// <summary>When troops cancelled at <paramref name="cancelAt"/> are home again.</summary>
        public static DateTime HomeAt(DateTime sentAt, DateTime cancelAt) => cancelAt + (cancelAt - sentAt);

        /// <summary>
        /// Troops left with the ally can come back when they get home after the wave and before the next
        /// attack, with time to leave again.
        /// </summary>
        public static bool CanRecall(DateTime now, int travelSeconds, DateTime waveLast, IEnumerable<IncomingAttack> attacks, int leadSeconds, int safetySeconds)
        {
            var home = now.AddSeconds(Math.Max(0, travelSeconds));
            if (home < waveLast.AddSeconds(Math.Max(0, safetySeconds))) return false;
            var next = attacks.Select(x => x.Arrival).Where(x => x > now).DefaultIfEmpty(DateTime.MaxValue).Min();
            if (next == DateTime.MaxValue) return true;
            return home.AddSeconds(Math.Max(1, leadSeconds) + PrepareSeconds) <= next;
        }

        /// <summary>Reads "0:04:47" or "In 0:04:47 hours" as seconds. -1 when there is no time.</summary>
        public static int ParseDuration(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return -1;
            var match = System.Text.RegularExpressions.Regex.Match(text, @"(\d+):(\d{1,2}):(\d{1,2})");
            if (!match.Success) return -1;
            return int.Parse(match.Groups[1].Value) * 3600 + int.Parse(match.Groups[2].Value) * 60 + int.Parse(match.Groups[3].Value);
        }
    }
}
