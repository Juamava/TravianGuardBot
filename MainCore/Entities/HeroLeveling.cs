using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

#nullable disable

namespace MainCore.Entities
{
    // One row per account. Created with raw SQL in AppDbContext.EnsureHeroLevelingTable (no migrations).
    [Table("HeroLevelings")]
    public class HeroLeveling
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int AccountId { get; set; }

        public bool Enabled { get; set; }
        public string State { get; set; }
        public string Message { get; set; }
        public int? Level { get; set; }
        public int? HpPercent { get; set; }
        public int? Experience { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? NextCheckAt { get; set; }
    }
}
