using System.ComponentModel.DataAnnotations.Schema;

#nullable disable

namespace MainCore.Entities
{
    [Table("CowVillages")]
    public class CowVillage
    {
        public int Id { get; set; }

        public int CowListId { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
        public int TroopCount { get; set; }
        public string TroopPlan { get; set; }
        public DateTime? LastAttackedAt { get; set; }
        public string LastSkipReason { get; set; }

        public CowList CowList { get; set; }
    }
}
