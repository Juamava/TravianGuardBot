namespace MainCore.Infrasturecture.Extensions
{
    public static class SaveVillageSettingExtension
    {
        extension(AppDbContext context)
        {
            public void SaveVillageSetting(VillageId villageId, Dictionary<VillageSettingEnums, int> settings)
            {
                if (settings.Count == 0) return;
                var added = false;
                foreach (var setting in settings)
                {
                    var updated = context.VillagesSetting
                        .Where(x => x.VillageId == villageId.Value)
                        .Where(x => x.Setting == setting.Key)
                        .ExecuteUpdate(x => x.SetProperty(x => x.Value, setting.Value));
                    if (updated > 0) continue;
                    // A setting added in a newer version has no row yet when the app did not fill the defaults.
                    context.VillagesSetting.Add(new VillageSetting { VillageId = villageId.Value, Setting = setting.Key, Value = setting.Value });
                    added = true;
                }
                if (added) context.SaveChanges();
            }
        }
    }
}