using System.ComponentModel.DataAnnotations.Schema;

#nullable disable

namespace MainCore.Entities
{
    [Table("CowLists")]
    public class CowList
    {
        public int Id { get; set; }

        public int AccountId { get; set; }
        public int VillageId { get; set; }
        public string Name { get; set; }
        public int MinAttackMinutes { get; set; }
        public int RepeatMinutes { get; set; }
        public bool AutoStart { get; set; }

        /// <summary>"Levear héroe": oases with animals go through the hero hunt instead of being skipped.</summary>
        public bool LevelHero { get; set; }

        public ICollection<CowVillage> Villages { get; set; }
    }
}
