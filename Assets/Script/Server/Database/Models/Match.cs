#if UNITY_SERVER
using SQLite;

namespace Server.Database
{
    [Table("Matches")]
    public class Match
    {
        [PrimaryKey]
        public string Id { get; set; }
        
        [NotNull]
        public string RoomId { get; set; }
        
        public string WinnerId { get; set; }
        
        public int Duration { get; set; }
        
        public long StartedAt { get; set; }
        
        public long EndedAt { get; set; }
    }
}
#endif