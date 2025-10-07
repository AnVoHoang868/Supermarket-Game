using Unity.Netcode;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Shared.Networking
{
    /// <summary>
    /// Manages user sessions with persistence, validation, and automatic cleanup
    /// Tracks active sessions across client-server connections
    /// </summary>
    public class SessionManager : NetworkBehaviour
    {
        public static SessionManager Instance { get; private set; }
        
        [Header("Session Settings")]
        [SerializeField] private float sessionTimeoutMinutes = 30f;
        [SerializeField] private float cleanupIntervalSeconds = 60f;
        [SerializeField] private bool debugMode = true;
        
        // Events
        public event Action<string> OnSessionCreated; // sessionId
        public event Action<string> OnSessionExpired; // sessionId
        public event Action<string> OnSessionDestroyed; // sessionId
        
        // Session storage
        private Dictionary<string, UserSession> _activeSessions = new Dictionary<string, UserSession>();
        private Dictionary<ulong, string> _clientToSession = new Dictionary<ulong, string>();
        private float _lastCleanupTime;
        
        public int ActiveSessionCount => _activeSessions.Count;
        public TimeSpan SessionTimeout => TimeSpan.FromMinutes(sessionTimeoutMinutes);
        
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
                Debug.Log($"[SessionManager] NetworkSpawn - IsServer: {IsServer}, IsClient: {IsClient}");
                
            _lastCleanupTime = Time.time;
        }

        void Update()
        {
            if (IsServer && Time.time - _lastCleanupTime >= cleanupIntervalSeconds)
            {
                CleanupExpiredSessions();
                _lastCleanupTime = Time.time;
            }
        }

        #region Public API
        
        /// <summary>
        /// Creates a new session for a user (server only)
        /// </summary>
        public string CreateSession(string userId, string displayName, bool isGuest = false)
        {
            if (!IsServer)
            {
                Debug.LogError("[SessionManager] CreateSession can only be called on server");
                return null;
            }
            
            var sessionId = Guid.NewGuid().ToString();
            var session = new UserSession
            {
                SessionId = sessionId,
                UserId = userId,
                DisplayName = displayName,
                IsGuest = isGuest,
                CreatedAt = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow,
                IsActive = true
            };
            
            _activeSessions[sessionId] = session;
            
            if (debugMode)
                Debug.Log($"[SessionManager] Session created - SessionId: {sessionId}, UserId: {userId}");
            
            OnSessionCreated?.Invoke(sessionId);
            return sessionId;
        }
        
        /// <summary>
        /// Associates a session with a client ID (server only)
        /// </summary>
        public void AssignSessionToClient(ulong clientId, string userId)
        {
            if (!IsServer) return;
            
            var session = _activeSessions.Values.FirstOrDefault(s => s.UserId == userId && s.IsActive);
            if (session != null)
            {
                _clientToSession[clientId] = session.SessionId;
                session.ClientId = clientId;
                
                if (debugMode)
                    Debug.Log($"[SessionManager] Session assigned to client - ClientId: {clientId}, SessionId: {session.SessionId}");
            }
        }
        
        /// <summary>
        /// Gets a session by session ID
        /// </summary>
        public UserSession GetSession(string sessionId)
        {
            return _activeSessions.TryGetValue(sessionId, out var session) ? session : null;
        }
        
        /// <summary>
        /// Gets a session by client ID
        /// </summary>
        public UserSession GetClientSession(ulong clientId)
        {
            if (_clientToSession.TryGetValue(clientId, out var sessionId))
            {
                return GetSession(sessionId);
            }
            return null;
        }
        
        /// <summary>
        /// Gets a session by user ID
        /// </summary>
        public UserSession GetUserSession(string userId)
        {
            return _activeSessions.Values.FirstOrDefault(s => s.UserId == userId && s.IsActive);
        }
        
        /// <summary>
        /// Updates session activity timestamp (server only)
        /// </summary>
        public void UpdateSessionActivity(string sessionId)
        {
            if (!IsServer) return;
            
            if (_activeSessions.TryGetValue(sessionId, out var session))
            {
                session.LastActivity = DateTime.UtcNow;
                
                if (debugMode && Time.frameCount % 300 == 0) // Log every 5 seconds at 60fps
                    Debug.Log($"[SessionManager] Session activity updated - SessionId: {sessionId}");
            }
        }
        
        /// <summary>
        /// Updates session activity by client ID (server only)
        /// </summary>
        public void UpdateClientActivity(ulong clientId)
        {
            if (_clientToSession.TryGetValue(clientId, out var sessionId))
            {
                UpdateSessionActivity(sessionId);
            }
        }
        
        /// <summary>
        /// Destroys a session (server only)
        /// </summary>
        public void DestroySession(string sessionId)
        {
            if (!IsServer) return;
            
            if (_activeSessions.TryGetValue(sessionId, out var session))
            {
                session.IsActive = false;
                session.EndedAt = DateTime.UtcNow;
                
                // Remove client mapping
                if (session.ClientId.HasValue)
                {
                    _clientToSession.Remove(session.ClientId.Value);
                }
                
                _activeSessions.Remove(sessionId);
                
                if (debugMode)
                    Debug.Log($"[SessionManager] Session destroyed - SessionId: {sessionId}, UserId: {session.UserId}");
                
                OnSessionDestroyed?.Invoke(sessionId);
            }
        }
        
        /// <summary>
        /// Destroys session by client ID (server only)
        /// </summary>
        public void DestroyClientSession(ulong clientId)
        {
            if (_clientToSession.TryGetValue(clientId, out var sessionId))
            {
                DestroySession(sessionId);
            }
        }
        
        /// <summary>
        /// Gets all active sessions (server only)
        /// </summary>
        public IEnumerable<UserSession> GetActiveSessions()
        {
            return _activeSessions.Values.Where(s => s.IsActive);
        }
        
        /// <summary>
        /// Validates if a session is still valid
        /// </summary>
        public bool IsSessionValid(string sessionId)
        {
            if (_activeSessions.TryGetValue(sessionId, out var session))
            {
                return session.IsActive && 
                       DateTime.UtcNow - session.LastActivity <= SessionTimeout;
            }
            return false;
        }
        
        #endregion

        #region Private Methods
        
        /// <summary>
        /// Cleans up expired sessions (server only)
        /// </summary>
        private void CleanupExpiredSessions()
        {
            if (!IsServer) return;
            
            var now = DateTime.UtcNow;
            var expiredSessions = _activeSessions.Values
                .Where(s => s.IsActive && now - s.LastActivity > SessionTimeout)
                .ToList();
            
            foreach (var session in expiredSessions)
            {
                if (debugMode)
                    Debug.Log($"[SessionManager] Session expired - SessionId: {session.SessionId}, UserId: {session.UserId}");
                
                OnSessionExpired?.Invoke(session.SessionId);
                DestroySession(session.SessionId);
            }
            
            if (expiredSessions.Count > 0 && debugMode)
            {
                Debug.Log($"[SessionManager] Cleaned up {expiredSessions.Count} expired sessions. Active sessions: {ActiveSessionCount}");
            }
        }
        
        #endregion

        #region Network Events
        
        public void OnClientDisconnected(ulong clientId)
        {
            if (IsServer)
            {
                if (debugMode)
                    Debug.Log($"[SessionManager] Client {clientId} disconnected, destroying session");
                
                DestroyClientSession(clientId);
            }
        }
        
        #endregion

        #region Server RPCs
        
        [ServerRpc(RequireOwnership = false)]
        public void PingSessionServerRpc(ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            UpdateClientActivity(clientId);
        }
        
        #endregion

        #region Persistence (Future Implementation)
        
        /// <summary>
        /// Saves session data to persistent storage (future implementation)
        /// </summary>
        public void SaveSessionData()
        {
            // TODO: Implement session persistence to database or file
            if (debugMode)
                Debug.Log($"[SessionManager] Saving {ActiveSessionCount} active sessions");
        }
        
        /// <summary>
        /// Loads session data from persistent storage (future implementation)
        /// </summary>
        public void LoadSessionData()
        {
            // TODO: Implement session loading from database or file
            if (debugMode)
                Debug.Log("[SessionManager] Loading saved sessions");
        }
        
        #endregion
    }

    /// <summary>
    /// Represents a user session with persistence and validation data
    /// </summary>
    [System.Serializable]
    public class UserSession
    {
        public string SessionId { get; set; }
        public string UserId { get; set; }
        public string DisplayName { get; set; }
        public bool IsGuest { get; set; }
        public bool IsActive { get; set; }
        public ulong? ClientId { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime LastActivity { get; set; }
        public DateTime? EndedAt { get; set; }
        
        public TimeSpan Duration => (EndedAt ?? DateTime.UtcNow) - CreatedAt;
        public TimeSpan TimeSinceLastActivity => DateTime.UtcNow - LastActivity;
        public bool IsExpired => TimeSinceLastActivity > TimeSpan.FromMinutes(30);
        
        public override string ToString()
        {
            return $"Session[{SessionId}] User: {UserId}({DisplayName}) Active: {IsActive} Client: {ClientId} Duration: {Duration:hh\\:mm\\:ss}";
        }
    }
}