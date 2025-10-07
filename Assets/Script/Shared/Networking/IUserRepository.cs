using System;
using System.Threading.Tasks;

namespace Shared.Networking
{
    /// <summary>
    /// Interface for user repository operations that can be used across client and server
    /// Provides abstraction for user data access
    /// </summary>
    public interface IUserRepository
    {
        /// <summary>
        /// Gets a user by their unique identifier
        /// </summary>
        /// <param name="id">The user's unique ID</param>
        /// <returns>The user data if found, null otherwise</returns>
        IUser GetById(string id);
        
        /// <summary>
        /// Asynchronously gets a user by their unique identifier
        /// </summary>
        /// <param name="id">The user's unique ID</param>
        /// <returns>Task containing the user data if found, null otherwise</returns>
        Task<IUser> GetByIdAsync(string id);
        
        /// <summary>
        /// Gets a user by their username
        /// </summary>
        /// <param name="username">The username to search for</param>
        /// <returns>The user data if found, null otherwise</returns>
        IUser GetByUsername(string username);
        
        /// <summary>
        /// Creates a guest user with the specified display name
        /// </summary>
        /// <param name="displayName">The display name for the guest user</param>
        /// <returns>The created user data</returns>
        IUser CreateGuestUser(string displayName);
        
        /// <summary>
        /// Validates user credentials
        /// </summary>
        /// <param name="username">The username</param>
        /// <param name="password">The password</param>
        /// <returns>True if credentials are valid, false otherwise</returns>
        bool ValidateCredentials(string username, string password);
    }

    /// <summary>
    /// Interface representing user data that can be shared between client and server
    /// </summary>
    public interface IUser
    {
        /// <summary>
        /// Unique identifier for the user
        /// </summary>
        string Id { get; }
        
        /// <summary>
        /// Username for login (may be null for guest users)
        /// </summary>
        string Username { get; }
        
        /// <summary>
        /// Display name shown to other users
        /// </summary>
        string DisplayName { get; }
        
        /// <summary>
        /// Timestamp when the user was created
        /// </summary>
        long CreatedAt { get; }
        
        /// <summary>
        /// Whether this is a guest user (temporary account)
        /// </summary>
        bool IsGuest { get; }
    }
}

#if UNITY_SERVER
namespace Server.Database
{
    /// <summary>
    /// Server-side implementation of IUserRepository using SQLite database
    /// </summary>
    public partial class UserRepository : Shared.Networking.IUserRepository
    {
        // Existing SQLiteConnection _db field should already be defined
        
        /// <summary>
        /// Implementation of IUserRepository.GetById for server-side database access
        /// </summary>
        Shared.Networking.IUser Shared.Networking.IUserRepository.GetById(string id)
        {
            var user = GetById(id); // Calls existing GetById method
            return user != null ? new UserAdapter(user) : null;
        }
        
        /// <summary>
        /// Async implementation of GetById
        /// </summary>
        public async System.Threading.Tasks.Task<Shared.Networking.IUser> GetByIdAsync(string id)
        {
            // For now, just wrap the synchronous call
            // In production, you might want to implement true async database operations
            return await System.Threading.Tasks.Task.FromResult(((Shared.Networking.IUserRepository)this).GetById(id));
        }
        
        /// <summary>
        /// Implementation of IUserRepository.GetByUsername
        /// </summary>
        Shared.Networking.IUser Shared.Networking.IUserRepository.GetByUsername(string username)
        {
            var user = GetByUsername(username); // Calls existing GetByUsername method
            return user != null ? new UserAdapter(user) : null;
        }
        
        /// <summary>
        /// Implementation of IUserRepository.CreateGuestUser
        /// </summary>
        Shared.Networking.IUser Shared.Networking.IUserRepository.CreateGuestUser(string displayName)
        {
            var user = CreateGuestUser(displayName); // Calls existing CreateGuestUser method
            return user != null ? new UserAdapter(user) : null;
        }
        
        /// <summary>
        /// Implementation of IUserRepository.ValidateCredentials
        /// </summary>
        public bool ValidateCredentials(string username, string password)
        {
            var user = GetByUsername(username);
            if (user == null) return false;
            
            // TODO: Implement proper password hashing and verification
            // For now, this is a placeholder
            return !string.IsNullOrEmpty(user.PasswordHash);
        }
    }
    
    /// <summary>
    /// Adapter class to convert Server.Database.User to Shared.Networking.IUser
    /// </summary>
    public class UserAdapter : Shared.Networking.IUser
    {
        private readonly User _user;
        
        public UserAdapter(User user)
        {
            _user = user ?? throw new System.ArgumentNullException(nameof(user));
        }
        
        public string Id => _user.Id;
        public string Username => _user.Username;
        public string DisplayName => _user.DisplayName;
        public long CreatedAt => _user.CreatedAt;
        public bool IsGuest => string.IsNullOrEmpty(_user.Username);
    }
}
#endif