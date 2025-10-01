#if UNITY_SERVER
using SQLite;
using System.Collections.Generic;

namespace Server.Database
{
    public enum RoomStatus
    {
        Waiting = 0,
        Playing = 1,
        Ended = 2
    }

    [Table("Rooms")]
    public class Room
    {
        [PrimaryKey]
        public string Id { get; set; }
        
        [NotNull]
        public string OwnerId { get; set; }
        
        public int Status { get; set; }
        
        public int PlayerCount { get; set; }
        
        public long CreatedAt { get; set; }
        
        [Ignore]
        public List<RoomPlayer> Players { get; set; } = new List<RoomPlayer>();
    }
}
#endif