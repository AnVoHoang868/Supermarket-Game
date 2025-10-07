#if UNITY_SERVER
using SQLite;
using Shared.Networking;

namespace Server.Database
{
    [Table("Users")]
    public class User : IUser
    {
        [PrimaryKey]
        public string Id { get; set; }
        
        [Indexed]
        public string Username { get; set; }
        
        public string Password { get; set; }  // In production, this should be PasswordHash
        
        public string PasswordHash { get; set; }
        
        [NotNull]
        public string DisplayName { get; set; }
        
        public long CreatedAt { get; set; }
        
        public bool IsGuest { get; set; }
        
        public bool IsOnline { get; set; }
        
        public long LastLoginAt { get; set; }
    }
}
#endif