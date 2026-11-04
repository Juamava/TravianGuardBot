namespace MainCore.Infrasturecture.Extensions
{
    public static class GetSettingsExtension
    {
        public static readonly Func<AppDbContext, int, VillageSettingEnums, int> ByNameVillageSettingQuery =
           EF.CompileQuery((AppDbContext context, int villageId, VillageSettingEnums setting) =>
               context.VillagesSetting
                   .Where(x => x.VillageId == villageId)
                   .Where(x => x.Setting == setting)
                   .Select(x => x.Value)
                   .FirstOrDefault());

        public static readonly Func<AppDbContext, int, VillageSettingEnums, bool> ByNameVillageSettingBooleanQuery =
            EF.CompileQuery((AppDbContext context, int villageId, VillageSettingEnums setting) =>
                context.VillagesSetting
                    .Where(x => x.VillageId == villageId)
                    .Where(x => x.Setting == setting)
                    .Select(x => x.Value != 0)
                    .FirstOrDefault());

        public static readonly Func<AppDbContext, int, AccountSettingEnums, int> ByNameAccountSettingQuery =
            EF.CompileQuery((AppDbContext context, int accountId, AccountSettingEnums setting) =>
                context.AccountsSetting
                    .Where(x => x.AccountId == accountId)
                    .Where(x => x.Setting == setting)
                    .Select(x => x.Value)
                    .FirstOrDefault());

        public static readonly Func<AppDbContext, int, AccountSettingEnums, bool> ByNameAccountSettingBooleanQuery =
            EF.CompileQuery((AppDbContext context, int accountId, AccountSettingEnums setting) =>
                context.AccountsSetting
                    .Where(x => x.AccountId == accountId)
                    .Where(x => x.Setting == setting)
                    .Select(x => x.Value != 0)
                    .FirstOrDefault());

        public static int ByName(this AppDbContext context, VillageId villageId, VillageSettingEnums setting)
        {
            var settingValue = ByNameVillageSettingQuery(context, villageId.Value, setting);
            return settingValue;
        }

        public static int Normal(int min, int max)
        {
            if (max < min) (min, max) = (max, min);
            if (min == max) return min;

            var mean = (min + max) / 2.0;
            var stdDev = (max - min) / 6.0;
            var u1 = 1.0 - Random.Shared.NextDouble();
            var u2 = 1.0 - Random.Shared.NextDouble();
            var standardNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            var value = Math.Clamp(mean + (stdDev * standardNormal), min, max);
            return (int)Math.Round(value);
        }

        extension(AppDbContext context)
        {
            public int ByName(VillageId villageId, VillageSettingEnums settingMin, VillageSettingEnums settingMax, int multiplier = 1)
            {
                var settings = new List<VillageSettingEnums>
                {
                    settingMin,
                    settingMax,
                };

                var settingValues = context.VillagesSetting
                       .Where(x => x.VillageId == villageId.Value)
                       .Where(x => settings.Contains(x.Setting))
                       .Select(x => x.Value)
                       .ToList();

                if (settingValues.Count != 2) return 0;
                var min = settingValues.Min() * multiplier;
                var max = settingValues.Max() * multiplier;
                return GetSettingsExtension.Normal(min, max);
            }

            public int ByName(AccountId accountId, AccountSettingEnums setting)
            {
                var settingValue = ByNameAccountSettingQuery(context, accountId.Value, setting);
                return settingValue;
            }

            public int ByName(AccountId accountId, AccountSettingEnums settingMin, AccountSettingEnums settingMax, int multiplier = 1)
            {
                var settings = new List<AccountSettingEnums>
                {
                    settingMin,
                    settingMax,
                };

                var settingValues = context.AccountsSetting
                       .Where(x => x.AccountId == accountId.Value)
                       .Where(x => settings.Contains(x.Setting))
                       .Select(x => x.Value)
                       .ToList();

                if (settingValues.Count != 2) return 1000;
                var min = settingValues.Min() * multiplier;
                var max = settingValues.Max() * multiplier;
                return GetSettingsExtension.Normal(min, max);
            }

            public Dictionary<VillageSettingEnums, int> ByName(VillageId villageId, List<VillageSettingEnums> settings)
            {
                var settingValues = context.VillagesSetting
                       .Where(x => x.VillageId == villageId.Value)
                       .Where(x => settings.Contains(x.Setting))
                       .ToDictionary(x => x.Setting, x => x.Value);
                return settingValues;
            }

            public bool BooleanByName(VillageId villageId, VillageSettingEnums setting)
            {
                var settingValue = ByNameVillageSettingBooleanQuery(context, villageId.Value, setting);
                return settingValue;
            }

            public bool BooleanByName(AccountId accountId, AccountSettingEnums setting)
            {
                var settingValue = ByNameAccountSettingBooleanQuery(context, accountId.Value, setting);
                return settingValue;
            }
        }
    }
}