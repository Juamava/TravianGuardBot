namespace MainCore.Infrasturecture.Extensions
{
    /// <summary>
    /// Reads and writes the HeroLevelings row of an account: the hero status shown on the Hero page.
    /// </summary>
    public static class HeroLevelingStore
    {
        public static HeroLeveling? Get(AppDbContext context, AccountId accountId)
        {
            return context.HeroLevelings.AsNoTracking().FirstOrDefault(x => x.AccountId == accountId.Value);
        }

        public static void SaveAction(AppDbContext context, AccountId accountId, string state, string message)
        {
            var row = GetOrAdd(context, accountId);
            row.State = state;
            row.Message = message;
            row.UpdatedAt = DateTime.Now;
            context.SaveChanges();
        }

        public static void SaveStats(AppDbContext context, AccountId accountId, HeroStats stats)
        {
            var row = GetOrAdd(context, accountId);
            row.Level = stats.Level ?? row.Level;
            row.HpPercent = stats.HpPercent;
            row.Experience = stats.Experience ?? row.Experience;
            row.StatsAt = DateTime.Now;
            context.SaveChanges();
        }

        private static HeroLeveling GetOrAdd(AppDbContext context, AccountId accountId)
        {
            var row = context.HeroLevelings.FirstOrDefault(x => x.AccountId == accountId.Value);
            if (row is not null) return row;

            row = new HeroLeveling { AccountId = accountId.Value, State = "", Message = "" };
            context.HeroLevelings.Add(row);
            return row;
        }
    }
}
