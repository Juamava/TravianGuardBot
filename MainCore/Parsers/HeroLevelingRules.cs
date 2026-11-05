using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MainCore.Parsers
{
    public enum HeroLevelingAction
    {
        WaitForReturn,
        UnknownHealth,
        Adventure,
        CollectTasks,
        WaitForHealing,
        HuntOasis,
    }

    public readonly record struct HeroStats(int? Level, int? HpPercent, int? Experience, int? SpeedFieldsPerHour, int? StrengthPoints, int? OffBonusPoints);

    /// <summary>
    /// Raw numbers read from the combat simulator result tables.
    /// Values are null when a cell could not be read.
    /// </summary>
    public sealed record SimulationNumbers(
        IReadOnlyList<int?> AttackerInitial,
        IReadOnlyList<int?> AttackerLoss,
        IReadOnlyList<int?> DefenderInitial,
        IReadOnlyList<int?> DefenderLoss,
        int? HeroHealthBefore,
        int? HeroHealthAfter);

    public readonly record struct SimulationVerdict(bool Send, string Reason);

    public readonly record struct OasisCandidate(int Id, string Url, int X, int Y, double Distance);

    /// <summary>
    /// Pure decision logic for HeroLevelingTask. Everything here fails closed:
    /// when a value is missing or doubtful the answer is "do not send".
    /// </summary>
    public static partial class HeroLevelingRules
    {
        public const int MinHpPercent = 50;

        // Safety margin on top of "the hero survives": the simulator does not know about
        // animals that respawn while the hero walks, so we want health left over.
        public const int MinHealthLeftAfterFight = 25;

        public const string OasisListName = "Oasis";

        public const int HeroUnitIndex = 11;

        public static HeroLevelingAction Decide(bool heroHome, int? hpPercent, bool adventureAvailable, bool tasksClaimable)
        {
            if (!heroHome) return HeroLevelingAction.WaitForReturn;
            if (hpPercent is null) return HeroLevelingAction.UnknownHealth;

            if (hpPercent.Value >= MinHpPercent)
            {
                return adventureAvailable ? HeroLevelingAction.Adventure : HeroLevelingAction.HuntOasis;
            }

            return tasksClaimable ? HeroLevelingAction.CollectTasks : HeroLevelingAction.WaitForHealing;
        }

        public static bool CanGoOnAdventure(int? hpPercent) => hpPercent is int hp && hp >= MinHpPercent;

        public static bool CanHunt(int? hpPercent) => hpPercent is int hp && hp >= MinHpPercent;

        public static bool IsOasisList(string? name)
        {
            return string.Equals((name ?? "").Trim(), OasisListName, StringComparison.OrdinalIgnoreCase);
        }

        public static SimulationVerdict Evaluate(SimulationNumbers? result)
        {
            if (result is null) return new(false, "No se pudo leer la simulación");

            // Attacker side: exactly one hero (column 11), no other troops.
            if (result.AttackerInitial.Count < HeroUnitIndex || result.AttackerLoss.Count < HeroUnitIndex)
                return new(false, "La simulación no tiene la columna del héroe");
            if (result.AttackerInitial.Any(x => x is null) || result.AttackerLoss.Any(x => x is null))
                return new(false, "No se pudo leer el lado atacante");

            var heroInitial = result.AttackerInitial[HeroUnitIndex - 1]!.Value;
            if (heroInitial != 1) return new(false, "La simulación no incluye al héroe");

            for (var i = 0; i < HeroUnitIndex - 1; i++)
            {
                if (result.AttackerInitial[i]!.Value != 0) return new(false, "La simulación incluye otras tropas además del héroe");
            }

            var heroLoss = result.AttackerLoss[HeroUnitIndex - 1]!.Value;
            if (heroLoss != 0) return new(false, "El héroe muere en la simulación");
            if (result.AttackerLoss.Any(x => x!.Value != 0)) return new(false, "La simulación muestra pérdidas del atacante");

            // Defender side: animals present and at least one of them dies.
            if (result.DefenderInitial.Count == 0 || result.DefenderInitial.Any(x => x is null) || result.DefenderLoss.Any(x => x is null))
                return new(false, "No se pudo leer el lado defensor");
            if (result.DefenderInitial.Sum(x => x!.Value) <= 0) return new(false, "El oasis no tiene animales");

            var killed = result.DefenderLoss.Sum(x => Math.Abs(x!.Value));
            if (killed <= 0) return new(false, "El héroe no mata ningún animal");

            // The health line must be readable and leave a margin.
            if (result.HeroHealthAfter is null) return new(false, "No se pudo leer la vida del héroe después del combate");
            if (result.HeroHealthAfter.Value < MinHealthLeftAfterFight)
                return new(false, $"Al héroe le quedaría {result.HeroHealthAfter.Value}% de vida (mínimo {MinHealthLeftAfterFight}%)");

            return new(true, $"El héroe sobrevive con {result.HeroHealthAfter.Value}% y mata {killed} animales");
        }

        /// <summary>
        /// Parses numbers as Travian prints them: with bidi control marks, thousands separators
        /// and the Unicode minus sign (U+2212). Returns null when the text is not a single integer.
        /// </summary>
        public static int? ParseNumber(string? text)
        {
            if (text is null) return null;
            var builder = new StringBuilder();
            foreach (var ch in text)
            {
                if (char.IsWhiteSpace(ch) || IsBidiMark(ch) || ch is ',' or '.' or '%') continue;
                builder.Append(ch is '−' ? '-' : ch);
            }

            var value = builder.ToString();
            if (!SignedInteger().IsMatch(value)) return null;
            return int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) ? number : null;
        }

        private static bool IsBidiMark(char ch)
        {
            return ch is '‎' or '‏' or '‪' or '‫' or '‬' or '‭' or '‮' or '⁦' or '⁧' or '⁨' or '⁩';
        }

        [GeneratedRegex(@"\d+")]
        private static partial Regex Digits();

        [GeneratedRegex(@"^-?\d+$")]
        private static partial Regex SignedInteger();

        /// <summary>
        /// "Hero's health lowered from 96 to 95" -> (96, 95). Needs exactly two numbers.
        /// </summary>
        public static (int? Before, int? After) ParseHealthLine(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return (null, null);
            var matches = Digits().Matches(text);
            if (matches.Count != 2) return (null, null);
            var before = int.Parse(matches[0].Value, CultureInfo.InvariantCulture);
            var after = int.Parse(matches[1].Value, CultureInfo.InvariantCulture);
            if (before > 100 || after > 100 || after > before) return (null, null);
            return (before, after);
        }

        /// <summary>
        /// "Rufian - level 5" -> 5 (last number of the title).
        /// </summary>
        public static int? ParseLevel(string? title)
        {
            if (string.IsNullOrWhiteSpace(title)) return null;
            var matches = Digits().Matches(title);
            if (matches.Count == 0) return null;
            return int.Parse(matches[^1].Value, CultureInfo.InvariantCulture);
        }

        [GeneratedRegex(@"[?&]x=(-?\d+)&y=(-?\d+)", RegexOptions.IgnoreCase)]
        private static partial Regex Coordinates();

        public static bool TryParseCoordinates(string? url, out int x, out int y)
        {
            x = 0;
            y = 0;
            if (string.IsNullOrWhiteSpace(url)) return false;
            var match = Coordinates().Match(url);
            if (!match.Success) return false;
            x = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            y = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            return true;
        }

        public static List<OasisCandidate> OrderCandidates(IEnumerable<(int Id, string Url)> targets, int originX, int originY)
        {
            var list = new List<OasisCandidate>();
            foreach (var (id, url) in targets)
            {
                if (!TryParseCoordinates(url, out var x, out var y)) continue;
                var dx = x - originX;
                var dy = y - originY;
                list.Add(new OasisCandidate(id, url, x, y, Math.Sqrt((dx * dx) + (dy * dy))));
            }

            return [.. list.OrderBy(x => x.Distance).ThenBy(x => x.Id)];
        }

        /// <summary>HP points above the saved search HP that count as "clearly stronger".</summary>
        public const int SearchRestartHpGain = 10;

        /// <summary>
        /// The saved search position is only valid while the hero is not stronger than when it
        /// was saved: a level up, or clearly more HP, can make a closer oasis safe again.
        /// </summary>
        public static bool ShouldRestartSearch(int? savedLevel, int? savedHp, int? level, int? hp)
        {
            if (savedLevel is null || savedHp is null || level is null || hp is null) return true;
            if (level.Value > savedLevel.Value) return true;
            return hp.Value >= savedHp.Value + SearchRestartHpGain;
        }

        /// <summary>
        /// Candidates starting at <paramref name="startId"/> and wrapping around, so each one appears once.
        /// An unknown or missing start (the oasis was removed from the list) starts from the closest.
        /// </summary>
        public static List<OasisCandidate> StartFrom(IReadOnlyList<OasisCandidate> ordered, int? startId)
        {
            var index = startId is int id ? ordered.ToList().FindIndex(x => x.Id == id) : -1;
            if (index <= 0) return [.. ordered];
            return [.. ordered.Skip(index), .. ordered.Take(index)];
        }

        /// <summary>The oasis after <paramref name="id"/> in the closest-first order, wrapping to the closest.</summary>
        public static int? NextAfter(IReadOnlyList<OasisCandidate> ordered, int id)
        {
            if (ordered.Count == 0) return null;
            var index = ordered.ToList().FindIndex(x => x.Id == id);
            if (index < 0) return null;
            return ordered[(index + 1) % ordered.Count].Id;
        }

        /// <summary>
        /// Round trip estimate for the hero alone. Falls back to null when the speed is unknown.
        /// </summary>
        public static TimeSpan? RoundTrip(double distance, int? speedFieldsPerHour)
        {
            if (speedFieldsPerHour is not int speed || speed <= 0) return null;
            var oneWayHours = distance / speed;
            return TimeSpan.FromHours(oneWayHours * 2);
        }
    }
}
