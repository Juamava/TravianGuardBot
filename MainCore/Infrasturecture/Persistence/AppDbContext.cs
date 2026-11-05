using System.Collections.Immutable;

using StronglyTypedIds;

[assembly: StronglyTypedIdDefaults(backingType: StronglyTypedIdBackingType.Int, converters: StronglyTypedIdConverter.None)]

namespace MainCore.Infrasturecture.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        #region table

        public DbSet<Account> Accounts { get; set; }
        public DbSet<AccountInfo> AccountsInfo { get; set; }
        public DbSet<Access> Accesses { get; set; }
        public DbSet<AccountSetting> AccountsSetting { get; set; }
        public DbSet<HeroItem> HeroItems { get; set; }
        public DbSet<Village> Villages { get; set; }
        public DbSet<Building> Buildings { get; set; }
        public DbSet<QueueBuilding> QueueBuildings { get; set; }
        public DbSet<Job> Jobs { get; set; }
        public DbSet<Storage> Storages { get; set; }
        public DbSet<VillageSetting> VillagesSetting { get; set; }
        public DbSet<Farm> FarmLists { get; set; }
        public DbSet<CowList> CowLists { get; set; }
        public DbSet<CowVillage> CowVillages { get; set; }
        public DbSet<HeroLeveling> HeroLevelings { get; set; }

        #endregion table

        #region account setting

        public static readonly ImmutableDictionary<AccountSettingEnums, int> AccountDefaultSettings = new Dictionary<AccountSettingEnums, int>
        {
            {AccountSettingEnums.ClickDelayMin, 500 },
            {AccountSettingEnums.ClickDelayMax, 900 },
            {AccountSettingEnums.TaskDelayMin, 1500 },
            {AccountSettingEnums.TaskDelayMax, 2500 },
            {AccountSettingEnums.EnableAutoLoadVillageBuilding, 1 },
            {AccountSettingEnums.UseStartAllButton, 0 },
            {AccountSettingEnums.FarmIntervalMin, 540 },
            {AccountSettingEnums.FarmIntervalMax, 660 },
            {AccountSettingEnums.Tribe, 0 },
            {AccountSettingEnums.SleepTimeMin, 480 },
            {AccountSettingEnums.SleepTimeMax, 600 },
            {AccountSettingEnums.WorkTimeMin, 600 },
            {AccountSettingEnums.WorkTimeMax, 720 },
            {AccountSettingEnums.HeadlessChrome, 0 },
            {AccountSettingEnums.EnableAutoStartAdventure, 0 },
            {AccountSettingEnums.EnableAutoClaimDailyQuest, 0 },
            {AccountSettingEnums.EnableAutoSetHeroPoint, 0 },
        }.ToImmutableDictionary();

        private List<AccountSettingEnums> GetMissingAccountSettings()
        {
            var defaultSettings = AccountDefaultSettings.Keys
                .AsEnumerable();
            var dbSettings = AccountsSetting
                .Select(x => x.Setting)
                .Distinct()
                .AsEnumerable();
            return defaultSettings.Except(dbSettings).ToList();
        }

        public void FillAccountSettings()
        {
            var missingSettings = GetMissingAccountSettings();
            if (missingSettings.Count == 0) return;

            var missingSettingsDict = missingSettings
                .Select(x => new
                {
                    Setting = x,
                    Value = AccountDefaultSettings[x],
                })
                .ToDictionary(x => x.Setting, x => x.Value);

            var ids = Accounts
                .Select(x => x.Id)
                .AsEnumerable();

            var settings = new List<AccountSetting>();

            foreach (var id in ids)
            {
                foreach (var (setting, value) in missingSettingsDict)
                {
                    settings.Add(new AccountSetting
                    {
                        AccountId = id,
                        Setting = setting,
                        Value = value,
                    });
                }
            }

            AddRange(settings);
            SaveChanges();
        }

        #endregion account setting

        #region village setting

        public static readonly ImmutableDictionary<VillageSettingEnums, int> VillageDefaultSettings = new Dictionary<VillageSettingEnums, int>
        {
            {VillageSettingEnums.UseHeroResourceForBuilding, 0 },
            {VillageSettingEnums.ApplyRomanQueueLogicWhenBuilding, 0 },
            {VillageSettingEnums.UseSpecialUpgrade, 0 },
            {VillageSettingEnums.CompleteImmediately, 0 },
            {VillageSettingEnums.Tribe, 0 },
            {VillageSettingEnums.TrainTroopEnable, 0 },
            {VillageSettingEnums.TrainTroopRepeatTimeMin, 120 },
            {VillageSettingEnums.TrainTroopRepeatTimeMax, 180 },
            {VillageSettingEnums.TrainWhenLowResource, 0 },
            {VillageSettingEnums.BarrackTroop, 0 },
            {VillageSettingEnums.BarrackAmountMin, 1 },
            {VillageSettingEnums.BarrackAmountMax, 10 },
            {VillageSettingEnums.StableTroop, 0 },
            {VillageSettingEnums.StableAmountMin, 1 },
            {VillageSettingEnums.StableAmountMax, 10 },
            {VillageSettingEnums.GreatBarrackTroop, 0 },
            {VillageSettingEnums.GreatBarrackAmountMin, 1 },
            {VillageSettingEnums.GreatBarrackAmountMax, 10 },
            {VillageSettingEnums.GreatStableTroop, 0 },
            {VillageSettingEnums.GreatStableAmountMin, 1 },
            {VillageSettingEnums.GreatStableAmountMax, 10 },
            {VillageSettingEnums.WorkshopTroop, 0 },
            {VillageSettingEnums.WorkshopAmountMin, 1 },
            {VillageSettingEnums.WorkshopAmountMax, 10 },

            {VillageSettingEnums.AutoNPCEnable, 0 },
            {VillageSettingEnums.AutoNPCOverflow, 0 },
            {VillageSettingEnums.AutoNPCGranaryPercent, 95 },
            {VillageSettingEnums.AutoNPCWood, 1 },
            {VillageSettingEnums.AutoNPCClay, 1 },
            {VillageSettingEnums.AutoNPCIron, 1 },
            {VillageSettingEnums.AutoNPCCrop, 0 },

            {VillageSettingEnums.AutoRefreshEnable, 0 },
            {VillageSettingEnums.AutoRefreshMin, 45 },
            {VillageSettingEnums.AutoRefreshMax, 75 },

            {VillageSettingEnums.AutoClaimQuestEnable, 0 },
            {VillageSettingEnums.CompleteImmediatelyTime, 20 },
        }.ToImmutableDictionary();

        private List<VillageSettingEnums> GetMissingVillageSettings()
        {
            var defaultSettings = VillageDefaultSettings.Keys
                .AsEnumerable();
            var dbSettings = VillagesSetting
                .Select(x => x.Setting)
                .Distinct()
                .AsEnumerable();
            return defaultSettings.Except(dbSettings).ToList();
        }

        public void FillVillageSettings()
        {
            var missingSettings = GetMissingVillageSettings();
            if (missingSettings.Count == 0) return;

            var missingSettingsDict = missingSettings
                .Select(x => new
                {
                    Setting = x,
                    Value = VillageDefaultSettings[x],
                })
                .ToDictionary(x => x.Setting, x => x.Value);

            var ids = Villages
                .Select(x => x.Id)
                .AsEnumerable();

            var settings = new List<VillageSetting>();

            foreach (var id in ids)
            {
                foreach (var (setting, value) in missingSettingsDict)
                {
                    settings.Add(new VillageSetting
                    {
                        VillageId = id,
                        Setting = setting,
                        Value = value,
                    });
                }
            }

            AddRange(settings);
            SaveChanges();
        }

        public void FillVillageSettings(AccountId accountId, VillageId villageId)
        {
            var settings = new List<VillageSetting>();

            foreach (var (setting, value) in VillageDefaultSettings)
            {
                if (setting == VillageSettingEnums.Tribe)
                {
                    var tribe = AccountsSetting
                        .Where(x => x.AccountId == accountId.Value)
                        .Where(x => x.Setting == AccountSettingEnums.Tribe)
                        .Select(x => x.Value)
                        .FirstOrDefault();

                    settings.Add(new VillageSetting
                    {
                        VillageId = villageId.Value,
                        Setting = setting,
                        Value = tribe,
                    });
                    continue;
                }
                settings.Add(new VillageSetting
                {
                    VillageId = villageId.Value,
                    Setting = setting,
                    Value = value,
                });
            }

            AddRange(settings);
        }

        #endregion village setting

        public void EnsureCowListTables()
        {
            Database.ExecuteSqlRaw(
                """
                CREATE TABLE IF NOT EXISTS "CowLists" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_CowLists" PRIMARY KEY AUTOINCREMENT,
                    "AccountId" INTEGER NOT NULL,
                    "VillageId" INTEGER NOT NULL DEFAULT 0,
                    "Name" TEXT NOT NULL,
                    "MinAttackMinutes" INTEGER NOT NULL DEFAULT 0
                );
                """);

            EnsureColumn("CowLists", "VillageId", "INTEGER NOT NULL DEFAULT 0");
            EnsureColumn("CowLists", "MinAttackMinutes", "INTEGER NOT NULL DEFAULT 0");
            EnsureColumn("CowLists", "RepeatMinutes", "INTEGER NOT NULL DEFAULT 0");

            Database.ExecuteSqlRaw(
                """
                CREATE TABLE IF NOT EXISTS "CowVillages" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_CowVillages" PRIMARY KEY AUTOINCREMENT,
                    "CowListId" INTEGER NOT NULL,
                    "Url" TEXT NOT NULL,
                    "TroopCount" INTEGER NOT NULL,
                    "LastAttackedAt" TEXT NULL,
                    CONSTRAINT "FK_CowVillages_CowLists_CowListId" FOREIGN KEY ("CowListId") REFERENCES "CowLists" ("Id") ON DELETE CASCADE
                );
                """);

            EnsureColumn("CowVillages", "LastAttackedAt", "TEXT NULL");
            EnsureColumn("CowVillages", "TroopPlan", "TEXT NULL");
            EnsureColumn("CowVillages", "LastSkipReason", "TEXT NULL");
            EnsureColumn("CowVillages", "Name", "TEXT NOT NULL DEFAULT 'Aldea sin nombre'");
        }

        public void EnsureHeroLevelingTable()
        {
            Database.ExecuteSqlRaw(
                """
                CREATE TABLE IF NOT EXISTS "HeroLevelings" (
                    "AccountId" INTEGER NOT NULL CONSTRAINT "PK_HeroLevelings" PRIMARY KEY,
                    "Enabled" INTEGER NOT NULL DEFAULT 0,
                    "State" TEXT NULL,
                    "Message" TEXT NULL,
                    "Level" INTEGER NULL,
                    "HpPercent" INTEGER NULL,
                    "Experience" INTEGER NULL,
                    "UpdatedAt" TEXT NULL,
                    "NextCheckAt" TEXT NULL,
                    CONSTRAINT "FK_HeroLevelings_Accounts_AccountId" FOREIGN KEY ("AccountId") REFERENCES "Accounts" ("Id") ON DELETE CASCADE
                );
                """);
        }

        private void EnsureColumn(string table, string name, string definition)
        {
            var connection = Database.GetDbConnection();
            var openedHere = connection.State != System.Data.ConnectionState.Open;
            if (openedHere) connection.Open();

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = '{name}'";
                var count = Convert.ToInt32(command.ExecuteScalar());
                if (count > 0) return;

                using var alter = connection.CreateCommand();
                alter.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{name}\" {definition}";
                alter.ExecuteNonQuery();
            }
            finally
            {
                if (openedHere) connection.Close();
            }
        }
    }
}