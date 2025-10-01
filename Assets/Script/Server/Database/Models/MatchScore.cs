#if UNITY_SERVER
using SQLite;

namespace Server.Database
{
    [Table("MatchScores")]
    public class MatchScore
    {
        [NotNull]
        public string MatchId { get; set; }
        
        [NotNull]
        public string UserId { get; set; }
        
        public int Score { get; set; }
        
        public int Rank { get; set; }
    }
}
#endif