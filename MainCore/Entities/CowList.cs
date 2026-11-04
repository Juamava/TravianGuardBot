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

        public ICollection<CowVillage> Villages { get; set; }
    }
}
