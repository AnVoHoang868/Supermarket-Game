#if UNITY_SERVER
using SQLite;
using System;
using System.Linq;
using System.Threading.Tasks;
using Shared.Networking;

namespace Server.Database
{
    public class UserRepository : IUserRepository
    {
        private SQLiteConnection _db;

        public UserRepository(SQLiteConnection db)
        {
            _db = db;
        }

        public IUser CreateGuestUser(string displayName)
        {
            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = displayName,
                CreatedAt = DateTimeOffset.Now.ToUnixTimeSeconds(),
                IsGuest = true
            };

            _db.Insert(user);
            return user;
        }

        public IUser GetById(string id)
        {
            return _db.Table<User>().FirstOrDefault(u => u.Id == id);
        }

        public async Task<IUser> GetByIdAsync(string id)
        {
            return await Task.Run(() => GetById(id));
        }

        public IUser GetByUsername(string username)
        {
            return _db.Table<User>().FirstOrDefault(u => u.Username == username);
        }

        public bool ValidateCredentials(string username, string password)
        {
            var user = _db.Table<User>().FirstOrDefault(u => u.Username == username);
            if (user == null) return false;
            
            // In a real implementation, you would hash the password and compare
            // For now, we'll do a simple comparison (NOT SECURE - for demo only)
            return user.Password == password;
        }

        // Additional methods for internal server use
        public User CreateUser(string username, string password, string displayName)
        {
            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                Username = username,
                Password = password, // In production, hash this!
                DisplayName = displayName,
                CreatedAt = DateTimeOffset.Now.ToUnixTimeSeconds(),
                IsGuest = false
            };

            _db.Insert(user);
            return user;
        }

        public User GetUserById(string id)
        {
            return _db.Table<User>().FirstOrDefault(u => u.Id == id);
        }
    }
}
#endif