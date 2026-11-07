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
        public DbSet<ScoutResult> ScoutResults { get; set; }

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
            {AccountSettingEnums.Gold, 0 },
            {AccountSettingEnums.EnableAutoStartGameFarms, 0 },
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

            {VillageSettingEnums.EvadeEnable, 0 },
            {VillageSettingEnums.EvadeTargetX, 0 },
            {VillageSettingEnums.EvadeTargetY, 0 },
            {VillageSettingEnums.EvadeLeadSeconds, 10 },
            {VillageSettingEnums.EvadeTrainGapSeconds, 60 },
            {VillageSettingEnums.EvadeSafetySeconds, 5 },
            {VillageSettingEnums.EvadeCheckSeconds, 90 },
            {VillageSettingEnums.EvadeIncludeHero, 1 },
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

        /// <summary>
        /// Creates the tables and columns added after the first release. Safe to call on every start;
        /// the web admin calls it before serving, because it does not go through MainViewModel.Load.
        /// </summary>
        public void EnsureSchema()
        {
            EnsureCowListTables();
            EnsureHeroLevelingTable();
            EnsureVillageColumns();
            EnsureScoutTables();
            EnsureAccessColumns();
        }

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
            EnsureColumn("CowLists", "LevelHero", "INTEGER NOT NULL DEFAULT 0");
            EnsureColumn("CowLists", "AutoStart", "INTEGER NOT NULL DEFAULT 0");
            EnsureColumn("CowLists", "Kind", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn("CowLists", "FullThreshold", "INTEGER NOT NULL DEFAULT 0");
            EnsureColumn("CowLists", "KeepScouts", "INTEGER NOT NULL DEFAULT 0");
            EnsureFarmIntervals();

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
            EnsureColumn("CowVillages", "Kind", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn("CowVillages", "Owner", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn("CowVillages", "Tribe", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn("CowVillages", "Alliance", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn("CowVillages", "Population", "INTEGER NOT NULL DEFAULT -1");
            EnsureColumn("CowVillages", "Distance", "REAL NOT NULL DEFAULT -1");
            EnsureColumn("CowVillages", "CoordX", "INTEGER NULL");
            EnsureColumn("CowVillages", "CoordY", "INTEGER NULL");
        }

        public void EnsureScoutTables()
        {
            Database.ExecuteSqlRaw(
                """
                CREATE TABLE IF NOT EXISTS "ScoutResults" (
                    "CowVillageId" INTEGER NOT NULL CONSTRAINT "PK_ScoutResults" PRIMARY KEY,
                    "Pending" INTEGER NOT NULL DEFAULT 0,
                    "SentAt" TEXT NULL,
                    "ScoutedAt" TEXT NULL,
                    "ReportTime" TEXT NOT NULL DEFAULT '',
                    "Wood" INTEGER NOT NULL DEFAULT -1,
                    "Clay" INTEGER NOT NULL DEFAULT -1,
                    "Iron" INTEGER NOT NULL DEFAULT -1,
                    "Crop" INTEGER NOT NULL DEFAULT -1,
                    "Cranny" INTEGER NOT NULL DEFAULT -1,
                    "Defenders" INTEGER NOT NULL DEFAULT -1,
                    "ScoutsDied" INTEGER NOT NULL DEFAULT 0,
                    "Class" TEXT NOT NULL DEFAULT '',
                    "Danger" INTEGER NOT NULL DEFAULT 0,
                    "PlayerUrl" TEXT NOT NULL DEFAULT '',
                    "PlayerPopulation" INTEGER NOT NULL DEFAULT -1,
                    "Alliance" TEXT NOT NULL DEFAULT '',
                    "PlayerVillages" INTEGER NOT NULL DEFAULT -1,
                    "AttackPoints" INTEGER NOT NULL DEFAULT -1,
                    "DefensePoints" INTEGER NOT NULL DEFAULT -1,
                    "DaysPlaying" INTEGER NOT NULL DEFAULT -1,
                    "HeroLevel" INTEGER NOT NULL DEFAULT -1,
                    "ProfileAt" TEXT NULL,
                    "AttackState" TEXT NOT NULL DEFAULT '',
                    "AttackPlan" TEXT NOT NULL DEFAULT '',
                    "AttackRaid" INTEGER NOT NULL DEFAULT 0,
                    "AttackAt" TEXT NULL,
                    "AttackMessage" TEXT NOT NULL DEFAULT '',
                    "AttackSentUtc" TEXT NULL,
                    "AttackReportAt" TEXT NULL,
                    "AttackDetails" TEXT NOT NULL DEFAULT '',
                    CONSTRAINT "FK_ScoutResults_CowVillages_CowVillageId" FOREIGN KEY ("CowVillageId") REFERENCES "CowVillages" ("Id") ON DELETE CASCADE
                );
                """);

            EnsureColumn("ScoutResults", "AttackState", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn("ScoutResults", "AttackPlan", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn("ScoutResults", "AttackRaid", "INTEGER NOT NULL DEFAULT 0");
            EnsureColumn("ScoutResults", "AttackAt", "TEXT NULL");
            EnsureColumn("ScoutResults", "AttackMessage", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn("ScoutResults", "AttackSentUtc", "TEXT NULL");
            EnsureColumn("ScoutResults", "AttackReportAt", "TEXT NULL");
            EnsureColumn("ScoutResults", "AttackDetails", "TEXT NOT NULL DEFAULT ''");
        }

        public void EnsureVillageColumns()
        {
            EnsureColumn("Villages", "LastTrainAt", "TEXT NULL");
            EnsureColumn("Villages", "NextTrainAt", "TEXT NULL");
        }

        public void EnsureAccessColumns()
        {
            EnsureColumn("Accesses", "ProxyHost", "TEXT NULL");
            EnsureColumn("Accesses", "ProxyPort", "INTEGER NOT NULL DEFAULT 0");
            EnsureColumn("Accesses", "ProxyUsername", "TEXT NULL");
            EnsureColumn("Accesses", "ProxyPassword", "TEXT NULL");
            EnsureColumn("Accesses", "Useragent", "TEXT NULL");
            EnsureColumn("Accesses", "LastUsed", "TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'");
        }

        public void EnsureHeroLevelingTable()
        {
            Database.ExecuteSqlRaw(
                """
                CREATE TABLE IF NOT EXISTS "HeroLevelings" (
                    "AccountId" INTEGER NOT NULL CONSTRAINT "PK_HeroLevelings" PRIMARY KEY,
                    "State" TEXT NULL,
                    "Message" TEXT NULL,
                    "Level" INTEGER NULL,
                    "HpPercent" INTEGER NULL,
                    "Experience" INTEGER NULL,
                    "UpdatedAt" TEXT NULL,
                    "StatsAt" TEXT NULL,
                    CONSTRAINT "FK_HeroLevelings_Accounts_AccountId" FOREIGN KEY ("AccountId") REFERENCES "Accounts" ("Id") ON DELETE CASCADE
                );
                """);

            // Older builds created this table with more columns. "Enabled" may be NOT NULL without a
            // default (when EnsureCreated made it), which breaks every insert now that it is unmapped.
            foreach (var legacy in new[] { "Enabled", "NextCheckAt", "NextOasisId", "SearchLevel", "SearchHp" })
            {
                DropColumn("HeroLevelings", legacy);
            }

            EnsureColumn("HeroLevelings", "StatsAt", "TEXT NULL");
        }

        private void DropColumn(string table, string name)
        {
            var connection = Database.GetDbConnection();
            var openedHere = connection.State != System.Data.ConnectionState.Open;
            if (openedHere) connection.Open();

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = '{name}'";
                var count = Convert.ToInt32(command.ExecuteScalar());
                if (count == 0) return;

                using var alter = connection.CreateCommand();
                alter.CommandText = $"ALTER TABLE \"{table}\" DROP COLUMN \"{name}\"";
                alter.ExecuteNonQuery();
            }
            finally
            {
                if (openedHere) connection.Close();
            }
        }

        private void EnsureFarmIntervals()
        {
            var addedMin = EnsureColumn("FarmLists", "IntervalMin", "INTEGER NOT NULL DEFAULT 540");
            var addedMax = EnsureColumn("FarmLists", "IntervalMax", "INTEGER NOT NULL DEFAULT 660");
            if (addedMin || addedMax)
            {
                var accounts = FarmLists.Select(x => x.AccountId).Distinct().ToList();
                foreach (var accountId in accounts)
                {
                    var settings = AccountsSetting
                        .Where(x => x.AccountId == accountId && (x.Setting == AccountSettingEnums.FarmIntervalMin || x.Setting == AccountSettingEnums.FarmIntervalMax))
                        .ToList();
                    var min = settings.FirstOrDefault(x => x.Setting == AccountSettingEnums.FarmIntervalMin)?.Value ?? 540;
                    var max = settings.FirstOrDefault(x => x.Setting == AccountSettingEnums.FarmIntervalMax)?.Value ?? 660;
                    if (min < 1) min = 540;
                    if (max < min) max = min;
                    FarmLists.Where(x => x.AccountId == accountId)
                        .ExecuteUpdate(x => x
                            .SetProperty(farm => farm.IntervalMin, min)
                            .SetProperty(farm => farm.IntervalMax, max));
                }
            }

            // Existing rows keep seconds until this column appears. It is added once, then those seconds become minutes.
            if (!EnsureColumn("FarmLists", "IntervalsInMinutes", "INTEGER NOT NULL DEFAULT 0")) return;

            var farms = FarmLists.ToList();
            foreach (var farm in farms)
            {
                farm.IntervalMin = SecondsToMinutes(farm.IntervalMin);
                farm.IntervalMax = SecondsToMinutes(farm.IntervalMax);
                if (farm.IntervalMax < farm.IntervalMin) farm.IntervalMax = farm.IntervalMin;
                farm.IntervalsInMinutes = 1;
            }
            if (farms.Count > 0) SaveChanges();
        }

        private static int SecondsToMinutes(int seconds)
        {
            if (seconds < 1) return 9;
            var minutes = (int)Math.Round(seconds / 60.0, MidpointRounding.AwayFromZero);
            return minutes < 1 ? 1 : minutes;
        }

        private bool EnsureColumn(string table, string name, string definition)
        {
            var connection = Database.GetDbConnection();
            var openedHere = connection.State != System.Data.ConnectionState.Open;
            if (openedHere) connection.Open();

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = '{name}'";
                var count = Convert.ToInt32(command.ExecuteScalar());
                if (count > 0) return false;

                using var alter = connection.CreateCommand();
                alter.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{name}\" {definition}";
                alter.ExecuteNonQuery();
                return true;
            }
            finally
            {
                if (openedHere) connection.Close();
            }
        }
    }
}