#if UNITY_SERVER
using SQLite;
using System;
using System.Linq;

namespace Server.Database
{
    public class UserRepository
    {
        private SQLiteConnection _db;

        public UserRepository(SQLiteConnection db)
        {
            _db = db;
        }

        public User CreateGuestUser(string displayName)
        {
            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = displayName,
                CreatedAt = DateTimeOffset.Now.ToUnixTimeSeconds()
            };

            _db.Insert(user);
            return user;
        }

        public User GetById(string id)
        {
            return _db.Table<User>().FirstOrDefault(u => u.Id == id);
        }

        public User GetByUsername(string username)
        {
            return _db.Table<User>().FirstOrDefault(u => u.Username == username);
        }
    }
}
#endif