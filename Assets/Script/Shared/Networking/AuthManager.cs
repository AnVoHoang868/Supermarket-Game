using Unity.Netcode;
using UnityEngine;
using System;
using System.Collections.Generic;
#if UNITY_SERVER
using Server.Database;
#endif

namespace Shared.Networking
{
    /// <summary>
    /// Manages user authentication across client-server using Unity Netcode
    /// Handles login/logout requests and authentication state synchronization
    /// </summary>
    public class AuthManager : NetworkBehaviour
    {
        public static AuthManager Instance { get; private set; }
        
        [Header("Authentication Settings")]
        [SerializeField] private bool debugMode = true;
        
        // Events
        public event Action<string, string> OnUserAuthenticated; // userId, displayName
        public event Action<string> OnUserLoggedOut; // userId
        public event Action<string> OnAuthenticationFailed; // error message
        
        // Session tracking
        private Dictionary<ulong, AuthSession> _activeSessions = new Dictionary<ulong, AuthSession>();
        private AuthSession _localSession;
        
        public bool IsAuthenticated => _localSession != null && _localSession.IsValid;
        public string LocalUserId => _localSession?.UserId;
        public string LocalDisplayName => _localSession?.DisplayName;
        
        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public override void OnNetworkSpawn()
        {
            if (debugMode)
                Debug.Log($"[AuthManager] NetworkSpawn - IsServer: {IsServer}, IsClient: {IsClient}");
        }

        #region Public API
        
        /// <summary>
        /// Attempts to login with guest credentials
        /// </summary>
        public void LoginAsGuest(string displayName)
        {
            if (!IsClient)
            {
                Debug.LogError("[AuthManager] LoginAsGuest can only be called from client");
                return;
            }
            
            if (string.IsNullOrEmpty(displayName))
            {
                OnAuthenticationFailed?.Invoke("Display name cannot be empty");
                return;
            }
            
            if (debugMode)
                Debug.Log($"[AuthManager] Requesting guest login for: {displayName}");
                
            LoginAsGuestServerRpc(displayName);
        }
        
        /// <summary>
        /// Attempts to login with username/password
        /// </summary>
        public void Login(string username, string password)
        {
            if (!IsClient)
            {
                Debug.LogError("[AuthManager] Login can only be called from client");
                return;
            }
            
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                OnAuthenticationFailed?.Invoke("Username and password are required");
                return;
            }
            
            if (debugMode)
                Debug.Log($"[AuthManager] Requesting login for: {username}");
                
            LoginServerRpc(username, password);
        }
        
        /// <summary>
        /// Logs out the current user
        /// </summary>
        public void Logout()
        {
            if (!IsClient)
            {
                Debug.LogError("[AuthManager] Logout can only be called from client");
                return;
            }
            
            if (_localSession == null)
            {
                Debug.LogWarning("[AuthManager] No active session to logout");
                return;
            }
            
            if (debugMode)
                Debug.Log($"[AuthManager] Requesting logout for: {_localSession.UserId}");
                
            LogoutServerRpc();
        }
        
        /// <summary>
        /// Gets the auth session for a specific client (server only)
        /// </summary>
        public AuthSession GetClientSession(ulong clientId)
        {
            return _activeSessions.TryGetValue(clientId, out var session) ? session : null;
        }
        
        #endregion

        #region Server RPCs
        
        [ServerRpc(RequireOwnership = false)]
        private void LoginAsGuestServerRpc(string displayName, ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            
            if (debugMode)
                Debug.Log($"[AuthManager] Server processing guest login for client {clientId}: {displayName}");
            
            try
            {
                // Check if client already has a session
                if (_activeSessions.ContainsKey(clientId))
                {
                    SendAuthenticationFailedClientRpc("Already authenticated", 
                        new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
                    return;
                }
                
                // Create guest user
                #if UNITY_SERVER
                var dbService = DatabaseService.Instance;
                if (dbService?.Users == null)
                {
                    SendAuthenticationFailedClientRpc("Server database not available", 
                        new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
                    return;
                }
                
                var user = dbService.Users.CreateGuestUser(displayName);
                if (user == null)
                {
                    SendAuthenticationFailedClientRpc("Failed to create user", 
                        new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
                    return;
                }
                
                // Create session
                var session = new AuthSession
                {
                    UserId = user.Id,
                    DisplayName = user.DisplayName,
                    ClientId = clientId,
                    LoginTime = DateTime.UtcNow,
                    IsGuest = true
                };
                
                _activeSessions[clientId] = session;
                
                // Integrate with SessionManager
                var sessionManager = SessionManager.Instance;
                if (sessionManager != null)
                {
                    sessionManager.CreateSession(user.Id, user.DisplayName, true);
                    sessionManager.AssignSessionToClient(clientId, session.UserId);
                }
                
                if (debugMode)
                    Debug.Log($"[AuthManager] Guest login successful - UserId: {user.Id}, ClientId: {clientId}");
                
                // Notify client of successful authentication
                SendAuthenticationSuccessClientRpc(user.Id, user.DisplayName, true,
                    new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
                #else
                // Fallback for non-server builds
                var userId = Guid.NewGuid().ToString();
                var session = new AuthSession
                {
                    UserId = userId,
                    DisplayName = displayName,
                    ClientId = clientId,
                    LoginTime = DateTime.UtcNow,
                    IsGuest = true
                };
                
                _activeSessions[clientId] = session;
                
                // Integrate with SessionManager
                var sessionManager = SessionManager.Instance;
                if (sessionManager != null)
                {
                    sessionManager.CreateSession(userId, displayName, true);
                    sessionManager.AssignSessionToClient(clientId, session.UserId);
                }
                
                SendAuthenticationSuccessClientRpc(userId, displayName, true,
                    new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
                #endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AuthManager] Guest login failed: {ex.Message}");
                SendAuthenticationFailedClientRpc($"Login failed: {ex.Message}", 
                    new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
            }
        }
        
        [ServerRpc(RequireOwnership = false)]
        private void LoginServerRpc(string username, string password, ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            
            if (debugMode)
                Debug.Log($"[AuthManager] Server processing login for client {clientId}: {username}");
            
            try
            {
                // Check if client already has a session
                if (_activeSessions.ContainsKey(clientId))
                {
                    SendAuthenticationFailedClientRpc("Already authenticated", 
                        new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
                    return;
                }
                
                #if UNITY_SERVER
                var dbService = DatabaseService.Instance;
                if (dbService?.Users == null)
                {
                    SendAuthenticationFailedClientRpc("Server database not available", 
                        new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
                    return;
                }
                
                // Use UserRepository for authentication
                var userRepo = dbService.Users as IUserRepository;
                if (userRepo == null || !userRepo.ValidateCredentials(username, password))
                {
                    SendAuthenticationFailedClientRpc("Invalid username or password", 
                        new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
                    return;
                }
                
                var user = userRepo.GetByUsername(username);
                if (user == null)
                {
                    SendAuthenticationFailedClientRpc("User not found", 
                        new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
                    return;
                }
                
                // Create session
                var session = new AuthSession
                {
                    UserId = user.Id,
                    DisplayName = user.DisplayName,
                    ClientId = clientId,
                    LoginTime = DateTime.UtcNow,
                    IsGuest = false
                };
                
                _activeSessions[clientId] = session;
                
                // Integrate with SessionManager
                var sessionManager = SessionManager.Instance;
                if (sessionManager != null)
                {
                    sessionManager.CreateSession(user.Id, user.DisplayName, false);
                    sessionManager.AssignSessionToClient(clientId, session.UserId);
                }
                
                if (debugMode)
                    Debug.Log($"[AuthManager] Login successful - UserId: {user.Id}, ClientId: {clientId}");
                
                SendAuthenticationSuccessClientRpc(user.Id, user.DisplayName, false,
                    new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
                #else
                // Fallback for testing
                SendAuthenticationFailedClientRpc("Authentication not available in client build", 
                    new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
                #endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AuthManager] Login failed: {ex.Message}");
                SendAuthenticationFailedClientRpc($"Login failed: {ex.Message}", 
                    new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
            }
        }
        
        [ServerRpc(RequireOwnership = false)]
        private void LogoutServerRpc(ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            
            if (debugMode)
                Debug.Log($"[AuthManager] Server processing logout for client {clientId}");
            
            if (_activeSessions.TryGetValue(clientId, out var session))
            {
                _activeSessions.Remove(clientId);
                
                // Clean up session in SessionManager
                var sessionManager = SessionManager.Instance;
                if (sessionManager != null)
                {
                    var sessionData = sessionManager.GetClientSession(clientId);
                    if (sessionData != null)
                    {
                        sessionManager.DestroySession(sessionData.SessionId);
                    }
                }
                
                if (debugMode)
                    Debug.Log($"[AuthManager] Logout successful - UserId: {session.UserId}, ClientId: {clientId}");
                
                SendLogoutSuccessClientRpc(session.UserId,
                    new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
            }
            else
            {
                Debug.LogWarning($"[AuthManager] No active session found for client {clientId}");
            }
        }
        
        #endregion

        #region Client RPCs
        
        [ClientRpc]
        private void SendAuthenticationSuccessClientRpc(string userId, string displayName, bool isGuest, ClientRpcParams rpcParams = default)
        {
            if (debugMode)
                Debug.Log($"[AuthManager] Authentication successful - UserId: {userId}, DisplayName: {displayName}");
            
            _localSession = new AuthSession
            {
                UserId = userId,
                DisplayName = displayName,
                ClientId = NetworkManager.Singleton.LocalClientId,
                LoginTime = DateTime.UtcNow,
                IsGuest = isGuest
            };
            
            OnUserAuthenticated?.Invoke(userId, displayName);
        }
        
        [ClientRpc]
        private void SendAuthenticationFailedClientRpc(string errorMessage, ClientRpcParams rpcParams = default)
        {
            if (debugMode)
                Debug.LogWarning($"[AuthManager] Authentication failed: {errorMessage}");
            
            OnAuthenticationFailed?.Invoke(errorMessage);
        }
        
        [ClientRpc]
        private void SendLogoutSuccessClientRpc(string userId, ClientRpcParams rpcParams = default)
        {
            if (debugMode)
                Debug.Log($"[AuthManager] Logout successful for UserId: {userId}");
            
            _localSession = null;
            OnUserLoggedOut?.Invoke(userId);
        }
        
        #endregion

        #region Network Events
        
        public void OnClientDisconnected(ulong clientId)
        {
            if (IsServer && _activeSessions.ContainsKey(clientId))
            {
                var session = _activeSessions[clientId];
                _activeSessions.Remove(clientId);
                
                // Clean up session in SessionManager
                var sessionManager = SessionManager.Instance;
                if (sessionManager != null)
                {
                    var sessionData = sessionManager.GetClientSession(clientId);
                    if (sessionData != null)
                    {
                        sessionManager.DestroySession(sessionData.SessionId);
                    }
                }
                
                if (debugMode)
                    Debug.Log($"[AuthManager] Client {clientId} disconnected, removing session for user {session.UserId}");
            }
        }
        
        #endregion
    }

    /// <summary>
    /// Represents an authenticated user session
    /// </summary>
    [System.Serializable]
    public class AuthSession
    {
        public string UserId { get; set; }
        public string DisplayName { get; set; }
        public ulong ClientId { get; set; }
        public DateTime LoginTime { get; set; }
        public bool IsGuest { get; set; }
        
        public bool IsValid => !string.IsNullOrEmpty(UserId) && !string.IsNullOrEmpty(DisplayName);
        public TimeSpan SessionDuration => DateTime.UtcNow - LoginTime;
    }
}
//         /// Logs out the current user
//         /// </summary>
//         public void Logout()
//         {
//             if (!IsClient)
//             {
//                 Debug.LogError("[AuthManager] Logout can only be called from client");
//                 return;
//             }
            
//             if (_localSession == null)
//             {
//                 Debug.LogWarning("[AuthManager] No active session to logout");
//                 return;
//             }
            
//             if (debugMode)
//                 Debug.Log($"[AuthManager] Requesting logout for: {_localSession.UserId}");
                
//             LogoutServerRpc();
//         }
        
//         /// <summary>
//         /// Gets the auth session for a specific client (server only)
//         /// </summary>
//         public AuthSession GetClientSession(ulong clientId)
//         {
//             return _activeSessions.TryGetValue(clientId, out var session) ? session : null;
//         }
        
//         #endregion

//         #region Server RPCs
        
//         [ServerRpc(RequireOwnership = false)]
//         private void LoginAsGuestServerRpc(string displayName, ServerRpcParams rpcParams = default)
//         {
//             var clientId = rpcParams.Receive.SenderClientId;
            
//             if (debugMode)
//                 Debug.Log($"[AuthManager] Server processing guest login for client {clientId}: {displayName}");
            
//             try
//             {
//                 // Check if client already has a session
//                 if (_activeSessions.ContainsKey(clientId))
//                 {
//                     SendAuthenticationFailedClientRpc("Already authenticated", 
//                         new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
//                     return;
//                 }
                
//                 // Create guest user in database
//                 #if UNITY_SERVER
//                 var dbService = DatabaseService.Instance;
//                 if (dbService?.Users == null)
//                 {
//                     SendAuthenticationFailedClientRpc("Server database not available", 
//                         new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
//                     return;
//                 }
                
//                 var user = dbService.Users.CreateGuestUser(displayName);
//                 if (user == null)
//                 {
//                     SendAuthenticationFailedClientRpc("Failed to create user", 
//                         new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
//                     return;
//                 }
                
//                 // Create session
//                 var session = new AuthSession
//                 {
//                     UserId = user.Id,
//                     DisplayName = user.DisplayName,
//                     ClientId = clientId,
//                     LoginTime = DateTime.UtcNow,
//                     IsGuest = true
//                 };
                
//                 _activeSessions[clientId] = session;
                
//                 // Create session in SessionManager
//                 var sessionManager = SessionManager.Instance;
//                 if (sessionManager != null)
//                 {
//                     sessionManager.CreateSession(user.Id, user.DisplayName, true);
//                     sessionManager.AssignSessionToClient(clientId, session.UserId);
//                 }
                
//                 if (debugMode)
//                     Debug.Log($"[AuthManager] Guest login successful - UserId: {user.Id}, ClientId: {clientId}");
                
//                 // Notify client of successful authentication
//                 SendAuthenticationSuccessClientRpc(user.Id, user.DisplayName, true,
//                     new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
//                 #else
//                 // Fallback for non-server builds
//                 var userId = Guid.NewGuid().ToString();
//                 var session = new AuthSession
//                 {
//                     UserId = userId,
//                     DisplayName = displayName,
//                     ClientId = clientId,
//                     LoginTime = DateTime.UtcNow,
//                     IsGuest = true
//                 };
                
//                 _activeSessions[clientId] = session;
                
//                 // Create session in SessionManager
//                 var sessionManager = SessionManager.Instance;
//                 if (sessionManager != null)
//                 {
//                     sessionManager.CreateSession(userId, displayName, true);
//                     sessionManager.AssignSessionToClient(clientId, session.UserId);
//                 }
                
//                 SendAuthenticationSuccessClientRpc(userId, displayName, true,
//                     new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
//                 #endif
//             }
//             catch (Exception ex)
//             {
//                 Debug.LogError($"[AuthManager] Guest login failed: {ex.Message}");
//                 SendAuthenticationFailedClientRpc($"Login failed: {ex.Message}", 
//                     new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
//             }
//         }
        
//         [ServerRpc(RequireOwnership = false)]
//         private void LoginServerRpc(string username, string password, ServerRpcParams rpcParams = default)
//         {
//             var clientId = rpcParams.Receive.SenderClientId;
            
//             if (debugMode)
//                 Debug.Log($"[AuthManager] Server processing login for client {clientId}: {username}");
            
//             try
//             {
//                 // Check if client already has a session
//                 if (_activeSessions.ContainsKey(clientId))
//                 {
//                     SendAuthenticationFailedClientRpc("Already authenticated", 
//                         new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
//                     return;
//                 }
                
//                 #if UNITY_SERVER
//                 var dbService = DatabaseService.Instance;
//                 if (dbService?.Users == null)
//                 {
//                     SendAuthenticationFailedClientRpc("Server database not available", 
//                         new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
//                     return;
//                 }
                
//                 // TODO: Implement password verification
//                 var user = dbService.Users.GetByUsername(username);
//                 if (user == null)
//                 {
//                     SendAuthenticationFailedClientRpc("Invalid username or password", 
//                         new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
//                     return;
//                 }
                
//                 // Create session
//                 var session = new AuthSession
//                 {
//                     UserId = user.Id,
//                     DisplayName = user.DisplayName,
//                     ClientId = clientId,
//                     LoginTime = DateTime.UtcNow,
//                     IsGuest = false
//                 };
                
//                 _activeSessions[clientId] = session;
                
//                 // Create session in SessionManager
//                 var sessionManager = SessionManager.Instance;
//                 if (sessionManager != null)
//                 {
//                     sessionManager.CreateSession(user.Id, user.DisplayName, false);
//                     sessionManager.AssignSessionToClient(clientId, session.UserId);
//                 }
                
//                 if (debugMode)
//                     Debug.Log($"[AuthManager] Login successful - UserId: {user.Id}, ClientId: {clientId}");
                
//                 SendAuthenticationSuccessClientRpc(user.Id, user.DisplayName, false,
//                     new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
//                 #else
//                 // Fallback for testing
//                 SendAuthenticationFailedClientRpc("Authentication not available in client build", 
//                     new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
//                 #endif
//             }
//             catch (Exception ex)
//             {
//                 Debug.LogError($"[AuthManager] Login failed: {ex.Message}");
//                 SendAuthenticationFailedClientRpc($"Login failed: {ex.Message}", 
//                     new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
//             }
//         }
        
//         [ServerRpc(RequireOwnership = false)]
//         private void LogoutServerRpc(ServerRpcParams rpcParams = default)
//         {
//             var clientId = rpcParams.Receive.SenderClientId;
            
//             if (debugMode)
//                 Debug.Log($"[AuthManager] Server processing logout for client {clientId}");
            
//             if (_activeSessions.TryGetValue(clientId, out var session))
//             {
//                 _activeSessions.Remove(clientId);
                
//                 // Destroy session in SessionManager
//                 var sessionManager = SessionManager.Instance;
//                 if (sessionManager != null)
//                 {
//                     var sessionData = sessionManager.GetClientSession(clientId);
//                     if (sessionData != null)
//                     {
//                         sessionManager.DestroySession(sessionData.SessionId);
//                     }
//                 }
                
//                 if (debugMode)
//                     Debug.Log($"[AuthManager] Logout successful - UserId: {session.UserId}, ClientId: {clientId}");
                
//                 SendLogoutSuccessClientRpc(session.UserId,
//                     new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
//             }
//             else
//             {
//                 Debug.LogWarning($"[AuthManager] No active session found for client {clientId}");
//             }
//         }
        
//         #endregion

//         #region Client RPCs
        
//         [ClientRpc]
//         private void SendAuthenticationSuccessClientRpc(string userId, string displayName, bool isGuest, ClientRpcParams rpcParams = default)
//         {
//             if (debugMode)
//                 Debug.Log($"[AuthManager] Authentication successful - UserId: {userId}, DisplayName: {displayName}");
            
//             _localSession = new AuthSession
//             {
//                 UserId = userId,
//                 DisplayName = displayName,
//                 ClientId = NetworkManager.Singleton.LocalClientId,
//                 LoginTime = DateTime.UtcNow,
//                 IsGuest = isGuest
//             };
            
//             OnUserAuthenticated?.Invoke(userId, displayName);
//         }
        
//         [ClientRpc]
//         private void SendAuthenticationFailedClientRpc(string errorMessage, ClientRpcParams rpcParams = default)
//         {
//             if (debugMode)
//                 Debug.LogWarning($"[AuthManager] Authentication failed: {errorMessage}");
            
//             OnAuthenticationFailed?.Invoke(errorMessage);
//         }
        
//         [ClientRpc]
//         private void SendLogoutSuccessClientRpc(string userId, ClientRpcParams rpcParams = default)
//         {
//             if (debugMode)
//                 Debug.Log($"[AuthManager] Logout successful for UserId: {userId}");
            
//             _localSession = null;
//             OnUserLoggedOut?.Invoke(userId);
//         }
        
//         #endregion

//         #region Network Events
        
//         public void OnClientDisconnected(ulong clientId)
//         {
//             if (IsServer && _activeSessions.ContainsKey(clientId))
//             {
//                 var session = _activeSessions[clientId];
//                 _activeSessions.Remove(clientId);
                
//                 // Cleanup session in SessionManager
//                 var sessionManager = SessionManager.Instance;
//                 if (sessionManager != null)
//                 {
//                     var sessionData = sessionManager.GetClientSession(clientId);
//                     if (sessionData != null)
//                     {
//                         sessionManager.DestroySession(sessionData.SessionId);
//                     }
//                 }
                
//                 if (debugMode)
//                     Debug.Log($"[AuthManager] Client {clientId} disconnected, removing session for user {session.UserId}");
//             }
//         }
        
//         #endregion
//     }

//     /// <summary>
//     /// Represents an authenticated user session
//     /// </summary>
//     [System.Serializable]
//     public class AuthSession
//     {
//         public string UserId { get; set; }
//         public string DisplayName { get; set; }
//         public ulong ClientId { get; set; }
//         public DateTime LoginTime { get; set; }
//         public bool IsGuest { get; set; }
        
//         public bool IsValid => !string.IsNullOrEmpty(UserId) && !string.IsNullOrEmpty(DisplayName);
//         public TimeSpan SessionDuration => DateTime.UtcNow - LoginTime;
//     }
// }