namespace MainCore.Parsers
{
    /// <summary>What a scouting report says, already read from the page.</summary>
    public sealed record ScoutReport(
        int? X,
        int? Y,
        string VillageName,
        string PlayerName,
        string PlayerUrl,
        int Wood,
        int Clay,
        int Iron,
        int Crop,
        int Cranny,
        int Defenders,
        bool ScoutsDied,
        string Time);

    /// <summary>What a player profile page says.</summary>
    public sealed record PlayerProfile(
        string Tribe,
        string Alliance,
        int Villages,
        int DaysPlaying,
        int Population,
        int AttackPoints,
        int DefensePoints,
        int HeroLevel);

    /// <summary>Everything the danger level looks at. -1 means unknown.</summary>
    public sealed record DangerInput(
        int Defenders,
        bool ScoutsDied,
        int Population,
        double PopulationPercentile,
        int Villages,
        int AttackPoints,
        int DefensePoints,
        bool HasAlliance,
        int HeroLevel);

    /// <summary>
    /// Pure decisions for the Scouting lists, kept apart from the browser so they can be tested.
    /// </summary>
    public static class ScoutRules
    {
        public const string Fat = "gorda";
        public const string Thin = "flaca";
        public const string Danger = "peligro";

        /// <summary>
        /// A full warehouse caps wood, clay and iron at the same number, so three equal values mean "full".
        /// The threshold (when above 0) also counts as full, for villages whose warehouse is not known.
        /// </summary>
        public static bool IsFull(int wood, int clay, int iron, int crop, int threshold)
        {
            if (wood < 0 || clay < 0 || iron < 0) return false;
            var total = (long)wood + clay + iron + Math.Max(crop, 0);
            if (threshold > 0 && total >= threshold) return true;

            var max = Math.Max(wood, Math.Max(clay, iron));
            var min = Math.Min(wood, Math.Min(clay, iron));
            if (min < 700) return false;
            return max - min <= max * 0.01;
        }

        /// <summary>
        /// Fat cow: nothing defends it and the storage is full. Thin cow: nothing defends it, but someone
        /// is already taking the resources. Anything else is a danger.
        /// </summary>
        public static string Classify(ScoutReport report, int threshold)
        {
            if (report.ScoutsDied || report.Defenders != 0) return Danger;
            return IsFull(report.Wood, report.Clay, report.Iron, report.Crop, threshold) ? Fat : Thin;
        }

        /// <summary>
        /// 1 (harmless) to 10 (do not touch). Defense in the village weighs most, then how big the player is
        /// compared with the others in the list, then attack and defense points, alliance, villages and hero.
        /// </summary>
        public static int DangerLevel(DangerInput input) => DangerBreakdown(input).Level;

        /// <summary>One reason behind the danger level and the points it added (out of <see cref="MaxDangerScore"/>).</summary>
        public sealed record DangerPart(string Reason, double Points);

        public const double MaxDangerScore = 11.5;

        public static (int Level, IReadOnlyList<DangerPart> Parts) DangerBreakdown(DangerInput input)
        {
            var parts = new List<DangerPart>();

            // Troops at home: up to 3.5. Dead scouts mean there is something that kills scouts.
            if (input.ScoutsDied || input.Defenders < 0) parts.Add(new("Mató a los espías: no se vio qué defiende", 3.5));
            else if (input.Defenders > 0) parts.Add(new($"{input.Defenders} tropas en la aldea", Math.Min(3.5, 1 + Math.Log10(input.Defenders) * 1.2)));
            else parts.Add(new("Sin tropas en la aldea", 0));

            // Size against the other targets of the list: up to 2.
            if (input.PopulationPercentile >= 0)
                parts.Add(new($"Población {input.Population}: más grande que el {Math.Round(Math.Clamp(input.PopulationPercentile, 0, 1) * 100)}% de la lista", 2 * Math.Clamp(input.PopulationPercentile, 0, 1)));
            else if (input.Population > 0)
                parts.Add(new($"Población {input.Population}", Math.Min(2, input.Population / 500.0)));

            // Points from fights: attack up to 2, defense up to 1.5.
            if (input.AttackPoints > 0) parts.Add(new($"{input.AttackPoints:N0} puntos de ataque", Math.Min(2, Math.Log10(input.AttackPoints + 1) / 2.5)));
            else if (input.AttackPoints == 0) parts.Add(new("Nunca atacó", 0));
            if (input.DefensePoints > 0) parts.Add(new($"{input.DefensePoints:N0} puntos de defensa", Math.Min(1.5, Math.Log10(input.DefensePoints + 1) / 3)));

            if (input.HasAlliance) parts.Add(new("Está en una alianza", 0.75));
            if (input.Villages > 1) parts.Add(new($"{input.Villages} aldeas", Math.Min(1, (input.Villages - 1) * 0.35)));
            if (input.HeroLevel > 0) parts.Add(new($"Héroe nivel {input.HeroLevel}", Math.Min(0.75, input.HeroLevel / 40.0)));

            var score = parts.Sum(x => x.Points);
            var level = 1 + (int)Math.Round(score / MaxDangerScore * 9, MidpointRounding.AwayFromZero);
            return (Math.Clamp(level, 1, 10), parts);
        }

        /// <summary>Text for the danger tooltip: each reason with the points it added.</summary>
        public static string Explain(int level, IReadOnlyList<DangerPart> parts)
        {
            var lines = parts.Select(part => part.Points > 0 ? $"+{part.Points:0.0}  {part.Reason}" : $"  0   {part.Reason}");
            var total = parts.Sum(x => x.Points);
            return $"Peligro {level} de 10 ({total:0.0} de {MaxDangerScore:0.#} puntos)\n" + string.Join("\n", lines);
        }

        /// <summary>Share of the other populations that are smaller than this one (0 to 1), or -1 without data.</summary>
        public static double Percentile(int population, IReadOnlyCollection<int> all)
        {
            var known = all.Where(x => x > 0).ToList();
            if (population <= 0 || known.Count < 2) return -1;
            var below = known.Count(x => x < population);
            var equal = known.Count(x => x == population);
            return (below + Math.Max(0, equal - 1) / 2.0) / (known.Count - 1);
        }

        /// <summary>karte.php?d= map id to coordinates, for a world that goes from -size to size.</summary>
        public static (int X, int Y) FromMapId(int mapId, int worldSize = 200)
        {
            var width = worldSize * 2 + 1;
            var index = mapId - 1;
            var x = index % width - worldSize;
            var y = worldSize - index / width;
            return (x, y);
        }

        /// <summary>
        /// Village name without the coordinates the map title appends ("Aldea (Capital) (−57|18)" becomes
        /// "Aldea (Capital)"), and without the direction marks Travian puts around numbers.
        /// </summary>
        public static string CleanVillageName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            var text = new string(name.Where(c => c is not ('\u202a' or '\u202b' or '\u202c' or '\u202d' or '\u202e' or '\u2066' or '\u2067' or '\u2068' or '\u2069' or '\u200e' or '\u200f')).ToArray())
                .Replace('\u2212', '-');
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s*\(\s*-?\d+\s*\|\s*-?\d+\s*\)", "");
            return text.Trim();
        }

        /// <summary>The scout unit of each tribe, as a rally point letter.</summary>
        public static string DefaultScoutPlan(TribeEnums tribe, int amount = 1)
        {
            var letter = tribe switch
            {
                TribeEnums.Gauls => 'C',
                TribeEnums.Huns => 'C',
                TribeEnums.Spartan => 'B',
                _ => 'D',
            };
            return $"{letter}:{Math.Max(1, amount)}";
        }

        /// <summary>Why a cow is fat or thin, for the tooltip of those tags.</summary>
        public static string ExplainCow(string kind, int wood, int clay, int iron, int crop, int threshold)
        {
            var total = Math.Max(0, wood) + Math.Max(0, clay) + Math.Max(0, iron) + Math.Max(0, crop);
            var resources = $"{wood:N0} / {clay:N0} / {iron:N0} / {crop:N0} (total {total:N0})";
            return kind switch
            {
                Fat => $"Sin tropas y con el almacén lleno: {resources}." + (threshold > 0 ? $" Cuenta como gorda desde {threshold:N0} de total." : ""),
                Thin => $"Sin tropas, pero no está llena: {resources}. Probablemente alguien más ya la está farmeando.",
                _ => "",
            };
        }

        public static string Label(string kind, int danger) => kind switch
        {
            Fat => "Vaca gorda",
            Thin => "Vaca flaca",
            Danger => $"Peligro {danger}",
            _ => "",
        };
    }
}
