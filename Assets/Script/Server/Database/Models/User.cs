#if UNITY_SERVER
using SQLite;

namespace Server.Database
{
    [Table("Users")]
    public class User
    {
        [PrimaryKey]
        public string Id { get; set; }
        
        [Indexed]
        public string Username { get; set; }
        
        public string PasswordHash { get; set; }
        
        [NotNull]
        public string DisplayName { get; set; }
        
        public long CreatedAt { get; set; }
    }
}
#endif