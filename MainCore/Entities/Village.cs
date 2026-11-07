using StronglyTypedIds;

#nullable disable

namespace MainCore.Entities
{
    public class Village
    {
        public int Id { get; set; }

        public string Name { get; set; }
        public int X { get; set; }
        public int Y { get; set; }

        public bool IsActive { get; set; }
        public bool IsUnderAttack { get; set; }
        public int AccountId { get; set; }

        /// <summary>Last time TrainTroopTask ran here and when it is due again. Kept in the database so a
        /// restart does not train again right away. Columns added by AppDbContext.EnsureVillageColumns.</summary>
        public DateTime? LastTrainAt { get; set; }
        public DateTime? NextTrainAt { get; set; }

        public ICollection<Building> Buildings { get; set; }
        public ICollection<QueueBuilding> QueueBuildings { get; set; }
        public ICollection<Job> Jobs { get; set; }
        public Storage Storage { get; set; }
        public ICollection<VillageSetting> VillageSetting { get; set; }
    }

    [StronglyTypedId]
    public partial struct VillageId
    { }
}