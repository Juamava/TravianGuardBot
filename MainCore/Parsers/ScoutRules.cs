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
        public static int DangerLevel(DangerInput input)
        {
            double score = 0;

            // Troops at home: up to 3.5. Dead scouts mean there is something that kills scouts.
            if (input.ScoutsDied || input.Defenders < 0) score += 3.5;
            else if (input.Defenders > 0) score += Math.Min(3.5, 1 + Math.Log10(input.Defenders) * 1.2);

            // Size against the other targets of the list: up to 2.
            if (input.PopulationPercentile >= 0) score += 2 * Math.Clamp(input.PopulationPercentile, 0, 1);
            else if (input.Population > 0) score += Math.Min(2, input.Population / 500.0);

            // Points from fights: attack up to 2, defense up to 1.5.
            if (input.AttackPoints > 0) score += Math.Min(2, Math.Log10(input.AttackPoints + 1) / 2.5);
            if (input.DefensePoints > 0) score += Math.Min(1.5, Math.Log10(input.DefensePoints + 1) / 3);

            if (input.HasAlliance) score += 0.75;
            if (input.Villages > 1) score += Math.Min(1, (input.Villages - 1) * 0.35);
            if (input.HeroLevel > 0) score += Math.Min(0.75, input.HeroLevel / 40.0);

            const double max = 11.5;
            var level = 1 + (int)Math.Round(score / max * 9, MidpointRounding.AwayFromZero);
            return Math.Clamp(level, 1, 10);
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

        public static string Label(string kind, int danger) => kind switch
        {
            Fat => "Vaca gorda",
            Thin => "Vaca flaca",
            Danger => $"Peligro {danger}",
            _ => "",
        };
    }
}
