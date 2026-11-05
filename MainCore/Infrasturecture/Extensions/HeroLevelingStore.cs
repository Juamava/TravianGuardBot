namespace MainCore.Infrasturecture.Extensions
{
    /// <summary>
    /// Reads and writes the HeroLevelings row of an account (on/off flag, state and last message).
    /// </summary>
    public static class HeroLevelingStore
    {
        public const string MissingListMessage = "Primero creá en Farming una lista de vacas llamada \"Oasis\" y agregale oasis.";

        public static bool IsEnabled(AppDbContext context, AccountId accountId)
        {
            return context.HeroLevelings.Where(x => x.AccountId == accountId.Value).Select(x => x.Enabled).FirstOrDefault();
        }

        public static HeroLeveling? Get(AppDbContext context, AccountId accountId)
        {
            return context.HeroLevelings.AsNoTracking().FirstOrDefault(x => x.AccountId == accountId.Value);
        }

        public static void SetEnabled(AppDbContext context, AccountId accountId, bool enabled, string message)
        {
            var row = GetOrAdd(context, accountId);
            row.Enabled = enabled;
            row.State = enabled ? "Activo" : "Detenido";
            row.Message = message;
            row.UpdatedAt = DateTime.Now;
            if (!enabled) row.NextCheckAt = null;
            context.SaveChanges();
        }

        public static void SaveState(AppDbContext context, AccountId accountId, string state, string message, DateTime? nextCheckAt)
        {
            var row = GetOrAdd(context, accountId);
            if (!row.Enabled) return;
            row.State = state;
            row.Message = message;
            row.NextCheckAt = nextCheckAt;
            row.UpdatedAt = DateTime.Now;
            context.SaveChanges();
        }

        public static void SaveStats(AppDbContext context, AccountId accountId, HeroStats stats)
        {
            var row = GetOrAdd(context, accountId);
            row.Level = stats.Level ?? row.Level;
            row.HpPercent = stats.HpPercent;
            row.Experience = stats.Experience ?? row.Experience;
            row.UpdatedAt = DateTime.Now;
            context.SaveChanges();
        }

        private static HeroLeveling GetOrAdd(AppDbContext context, AccountId accountId)
        {
            var row = context.HeroLevelings.FirstOrDefault(x => x.AccountId == accountId.Value);
            if (row is not null) return row;

            row = new HeroLeveling { AccountId = accountId.Value, Enabled = false, State = "Detenido", Message = "" };
            context.HeroLevelings.Add(row);
            return row;
        }
    }
}
