using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MainCore.Parsers
{
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

    public enum SimulationOutcome
    {
        /// <summary>Safe: the hero survives with margin and kills animals.</summary>
        Safe,
        /// <summary>Read fine, but the hero dies, keeps too little HP or kills nothing.</summary>
        TooStrong,
        /// <summary>Something could not be read or did not match: never send.</summary>
        Unreadable,
    }

    public readonly record struct SimulationVerdict(bool Send, string Reason, SimulationOutcome Outcome)
    {
        public static SimulationVerdict Safe(string reason) => new(true, reason, SimulationOutcome.Safe);

        public static SimulationVerdict TooStrong(string reason) => new(false, reason, SimulationOutcome.TooStrong);

        public static SimulationVerdict Unreadable(string reason) => new(false, reason, SimulationOutcome.Unreadable);
    }

    /// <summary>What RaidCowListTask does with an oasis that has animals.</summary>
    public enum AnimalOasisAction
    {
        /// <summary>"Levear héroe" is off: the normal "Hay tropas" skip.</summary>
        SkipTroops,
        /// <summary>The hero already left in this run: the normal skip.</summary>
        HeroAlreadySent,
        HeroAway,
        HeroOtherVillage,
        /// <summary>HP not read yet in this run: read /hero/attributes and decide again.</summary>
        NeedHeroStats,
        /// <summary>HP below the minimum and tasks not collected yet in this run: collect and decide again.</summary>
        CollectTasks,
        HeroLowHp,
        HeroUnknownHp,
        /// <summary>Simulate and, only if safe, send the hero alone.</summary>
        TryHero,
    }

    /// <summary>Hero state for one pass over a cow list. Reset when the list starts again.</summary>
    public sealed class HeroRunState
    {
        public bool Sent { get; set; }
        public bool StatsRead { get; set; }
        public int? HpPercent { get; set; }
        public HeroStats Stats { get; set; }
        public bool TasksCollected { get; set; }
        public bool SeenAway { get; set; }

        public void Reset()
        {
            Sent = false;
            StatsRead = false;
            HpPercent = null;
            Stats = default;
            TasksCollected = false;
            SeenAway = false;
        }
    }

    /// <summary>
    /// Pure decision logic for the hero hunting inside RaidCowListTask. Everything here fails closed:
    /// when a value is missing or doubtful the answer is "do not send".
    /// </summary>
    public static partial class HeroLevelingRules
    {
        public const int MinHpPercent = 50;

        // Safety margin on top of "the hero survives": the simulator does not know about
        // animals that respawn while the hero walks, so we want health left over.
        public const int MinHealthLeftAfterFight = 25;

        public const int HeroUnitIndex = 11;

        // Short per-oasis statuses (CowVillages.LastSkipReason).
        public const string StatusTroops = "Hay tropas";
        public const string StatusSent = "Héroe enviado";
        public const string StatusTooStrong = "Muy fuerte";
        public const string StatusAway = "Héroe fuera";
        public const string StatusLowHp = "Héroe con poca vida";
        public const string StatusOtherVillage = "Héroe en otra aldea";
        public const string StatusUnknown = "Héroe sin datos";
        public const string StatusNotSimulated = "Sin simular";
        public const string StatusNotSent = "Héroe no enviado";

        public static readonly IReadOnlySet<string> HeroStatuses = new HashSet<string>
        {
            StatusSent, StatusTooStrong, StatusAway, StatusLowHp, StatusOtherVillage, StatusUnknown, StatusNotSimulated, StatusNotSent,
        };

        public static bool CanGoOnAdventure(int? hpPercent) => hpPercent is int hp && hp >= MinHpPercent;

        public static bool CanHunt(int? hpPercent) => hpPercent is int hp && hp >= MinHpPercent;

        /// <param name="listVillageId">CowLists.VillageId (0 = no village saved: only the hero home is required).</param>
        public static AnimalOasisAction DecideAnimalOasis(bool levelHero, HeroRunState run, bool heroHome, int? heroVillageId, int listVillageId)
        {
            if (!levelHero) return AnimalOasisAction.SkipTroops;
            if (run.Sent) return AnimalOasisAction.HeroAlreadySent;
            if (!heroHome) return AnimalOasisAction.HeroAway;
            if (heroVillageId is null || (listVillageId > 0 && heroVillageId.Value != listVillageId)) return AnimalOasisAction.HeroOtherVillage;
            if (!run.StatsRead) return AnimalOasisAction.NeedHeroStats;
            if (run.HpPercent is null) return AnimalOasisAction.HeroUnknownHp;
            if (run.HpPercent.Value < MinHpPercent)
                return run.TasksCollected ? AnimalOasisAction.HeroLowHp : AnimalOasisAction.CollectTasks;
            return AnimalOasisAction.TryHero;
        }

        /// <summary>
        /// For lists with "Levear héroe": an oasis the troops would skip before opening it
        /// (no plan / not enough troops) is still worth opening while the hero may go.
        /// </summary>
        public static bool HeroMightGo(bool levelHero, HeroRunState run)
        {
            if (!levelHero || run.Sent || run.SeenAway) return false;
            if (run.StatsRead && run.HpPercent is null) return false;
            if (run.StatsRead && run.TasksCollected && run.HpPercent < MinHpPercent) return false;
            return true;
        }

        public static string StatusFor(AnimalOasisAction action)
        {
            return action switch
            {
                AnimalOasisAction.HeroAway => StatusAway,
                AnimalOasisAction.HeroOtherVillage => StatusOtherVillage,
                AnimalOasisAction.HeroLowHp => StatusLowHp,
                AnimalOasisAction.HeroUnknownHp => StatusUnknown,
                _ => StatusTroops,
            };
        }

        public static string StatusFor(SimulationVerdict verdict)
        {
            return verdict.Outcome switch
            {
                SimulationOutcome.Safe => StatusSent,
                SimulationOutcome.TooStrong => StatusTooStrong,
                _ => StatusNotSimulated,
            };
        }

        public static SimulationVerdict Evaluate(SimulationNumbers? result)
        {
            if (result is null) return SimulationVerdict.Unreadable("No se pudo leer la simulación");

            // Attacker side: exactly one hero (column 11), no other troops.
            if (result.AttackerInitial.Count < HeroUnitIndex || result.AttackerLoss.Count < HeroUnitIndex)
                return SimulationVerdict.Unreadable("La simulación no tiene la columna del héroe");
            if (result.AttackerInitial.Any(x => x is null) || result.AttackerLoss.Any(x => x is null))
                return SimulationVerdict.Unreadable("No se pudo leer el lado atacante");

            var heroInitial = result.AttackerInitial[HeroUnitIndex - 1]!.Value;
            if (heroInitial != 1) return SimulationVerdict.Unreadable("La simulación no incluye al héroe");

            for (var i = 0; i < HeroUnitIndex - 1; i++)
            {
                if (result.AttackerInitial[i]!.Value != 0) return SimulationVerdict.Unreadable("La simulación incluye otras tropas además del héroe");
            }

            var heroLoss = result.AttackerLoss[HeroUnitIndex - 1]!.Value;
            if (heroLoss != 0) return SimulationVerdict.TooStrong("El héroe muere en la simulación");
            if (result.AttackerLoss.Any(x => x!.Value != 0)) return SimulationVerdict.Unreadable("La simulación muestra pérdidas del atacante");

            // Defender side: animals present and at least one of them dies.
            if (result.DefenderInitial.Count == 0 || result.DefenderInitial.Any(x => x is null) || result.DefenderLoss.Any(x => x is null))
                return SimulationVerdict.Unreadable("No se pudo leer el lado defensor");
            if (result.DefenderInitial.Sum(x => x!.Value) <= 0) return SimulationVerdict.Unreadable("El oasis no tiene animales");

            var killed = result.DefenderLoss.Sum(x => Math.Abs(x!.Value));
            if (killed <= 0) return SimulationVerdict.TooStrong("El héroe no mata ningún animal");

            // The health line must be readable and leave a margin.
            if (result.HeroHealthAfter is null) return SimulationVerdict.Unreadable("No se pudo leer la vida del héroe después del combate");
            if (result.HeroHealthAfter.Value < MinHealthLeftAfterFight)
                return SimulationVerdict.TooStrong($"Al héroe le quedaría {result.HeroHealthAfter.Value}% de vida (mínimo {MinHealthLeftAfterFight}%)");

            return SimulationVerdict.Safe($"El héroe sobrevive con {result.HeroHealthAfter.Value}% y mata {killed} animales");
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
    }
}
