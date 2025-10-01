#if UNITY_SERVER
using SQLite;

namespace Server.Database
{
    [Table("RoomPlayers")]
    public class RoomPlayer
    {
        [NotNull]
        public string RoomId { get; set; }
        
        [NotNull]
        public string UserId { get; set; }
        
        public int Slot { get; set; }
        
        public int IsReady { get; set; }
        
        public long JoinedAt { get; set; }
        
        [Ignore]
        public ulong ClientId { get; set; }
    }
}
#endif