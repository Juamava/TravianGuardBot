using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

#nullable disable

namespace MainCore.Entities
{
    // One row per account: hero status for the Hero page (last stats read and last hero action of the cow lists).
    // Created with raw SQL in AppDbContext.EnsureHeroLevelingTable (no migrations). Older databases also have
    // Enabled/NextCheckAt/NextOasisId/SearchLevel/SearchHp columns that are no longer used.
    [Table("HeroLevelings")]
    public class HeroLeveling
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int AccountId { get; set; }

        public string State { get; set; }
        public string Message { get; set; }
        public int? Level { get; set; }
        public int? HpPercent { get; set; }
        public int? Experience { get; set; }

        /// <summary>When State/Message changed.</summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>When Level/HpPercent/Experience were read.</summary>
        public DateTime? StatsAt { get; set; }
    }
}
