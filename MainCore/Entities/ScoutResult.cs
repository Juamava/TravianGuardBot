using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

#nullable disable

namespace MainCore.Entities
{
    /// <summary>
    /// What the last scouting report said about one target of a Scouting list, plus the owner's profile.
    /// Created with raw SQL in AppDbContext.EnsureScoutTables (no migrations).
    /// </summary>
    [Table("ScoutResults")]
    public class ScoutResult
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int CowVillageId { get; set; }

        /// <summary>Scouts were sent and the report has not been read yet.</summary>
        public bool Pending { get; set; }

        public DateTime? SentAt { get; set; }
        public DateTime? ScoutedAt { get; set; }

        /// <summary>Time printed on the report (server time).</summary>
        public string ReportTime { get; set; } = "";

        public int Wood { get; set; } = -1;
        public int Clay { get; set; } = -1;
        public int Iron { get; set; } = -1;
        public int Crop { get; set; } = -1;
        public int Cranny { get; set; } = -1;

        /// <summary>Troops seen in the village. -1 when the scouts died and nothing was seen.</summary>
        public int Defenders { get; set; } = -1;

        public bool ScoutsDied { get; set; }

        /// <summary><see cref="Parsers.ScoutRules"/> class: gorda, flaca or peligro.</summary>
        public string Class { get; set; } = "";

        /// <summary>1 to 10.</summary>
        public int Danger { get; set; }

        public string PlayerUrl { get; set; } = "";
        public int PlayerPopulation { get; set; } = -1;
        public string Alliance { get; set; } = "";
        public int PlayerVillages { get; set; } = -1;
        public int AttackPoints { get; set; } = -1;
        public int DefensePoints { get; set; } = -1;
        public int DaysPlaying { get; set; } = -1;
        public int HeroLevel { get; set; } = -1;
        public DateTime? ProfileAt { get; set; }

        /// <summary>Last attack asked from the Scouting screen: cola, enviando, enviado or error.</summary>
        public string AttackState { get; set; } = "";

        public string AttackPlan { get; set; } = "";
        public bool AttackRaid { get; set; }

        /// <summary>When AttackState last changed.</summary>
        public DateTime? AttackAt { get; set; }

        /// <summary>Why it failed, or what was sent.</summary>
        public string AttackMessage { get; set; } = "";

        public const string AttackQueued = "cola";
        public const string AttackSending = "enviando";
        public const string AttackSent = "enviado";
        public const string AttackFailed = "error";
    }
}
