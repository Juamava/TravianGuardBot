namespace MainCore.Infrasturecture.Extensions
{
    /// <summary>
    /// Reads and writes the HeroLevelings row of an account: the hero status shown on the Hero page.
    /// The status is only informative: a failed save must never stop the cow list, so errors are swallowed
    /// and the row is detached (otherwise the next SaveChanges of the same context would fail too).
    /// </summary>
    public static class HeroLevelingStore
    {
        public static HeroLeveling? Get(AppDbContext context, AccountId accountId)
        {
            return context.HeroLevelings.AsNoTracking().FirstOrDefault(x => x.AccountId == accountId.Value);
        }

        public static void SaveAction(AppDbContext context, AccountId accountId, string state, string message)
        {
            Save(context, accountId, row =>
            {
                row.State = state;
                row.Message = message;
                row.UpdatedAt = DateTime.Now;
            });
        }

        public static void SaveStats(AppDbContext context, AccountId accountId, HeroStats stats)
        {
            Save(context, accountId, row =>
            {
                row.Level = stats.Level ?? row.Level;
                row.HpPercent = stats.HpPercent;
                row.Experience = stats.Experience ?? row.Experience;
                row.StatsAt = DateTime.Now;
            });
        }

        private static void Save(AppDbContext context, AccountId accountId, Action<HeroLeveling> change)
        {
            HeroLeveling? row = null;
            try
            {
                row = GetOrAdd(context, accountId);
                change(row);
                context.SaveChanges();
            }
            catch (Exception)
            {
                if (row is not null) context.Entry(row).State = EntityState.Detached;
            }
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
