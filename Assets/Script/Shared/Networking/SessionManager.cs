using Unity.Netcode;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Shared.Networking
{
    /// <summary>
    /// Manages user sessions across the client-server architecture
    /// Handles session persistence, validation, and cleanup
    /// </summary>
    public class SessionManager : NetworkBehaviour
    {
        public static SessionManager Instance { get; private set; }
        
        [Header("Session Settings")]
        [SerializeField] private float sessionTimeoutMinutes = 30f;
        [SerializeField] private float cleanupIntervalSeconds = 60f;
        [SerializeField] private bool enableSessionPersistence = true;
        [SerializeField] private bool debugMode = true;
        
        // Events
        public event Action<string> OnSessionCreated; // sessionId
        public event Action<string> OnSessionExpired; // sessionId
        public event Action<string, SessionData> OnSessionUpdated; // sessionId, sessionData
        
        // Session storage
        private Dictionary<string, SessionData> _sessions = new Dictionary<string, SessionData>();
        private Dictionary<ulong, string> _clientToSession = new Dictionary<ulong, string>();
        private SessionData _localSession;
        
        // Timing
        private float _lastCleanupTime;
        
        public SessionData LocalSession => _localSession;
        public bool HasActiveSession => _localSession != null && _localSession.IsValid;
        public string LocalSessionId => _localSession?.SessionId;
        
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

        void Start()
        {
            if (enableSessionPersistence && IsClient)
            {
                LoadLocalSession();
            }
        }

        void Update()
        {
            if (IsServer && Time.time - _lastCleanupTime > cleanupIntervalSeconds)
            {
                CleanupExpiredSessions();
                _lastCleanupTime = Time.time;
            }
        }

        #region Public API
        
        /// <summary>
        /// Creates a new session for the authenticated user
        /// </summary>
        public void CreateSession(string userId, string displayName, bool isGuest = false)
        {
            if (!IsServer)
            {
                Debug.LogError("[SessionManager] CreateSession can only be called from server");
                return;
            }
            
            var sessionId = Guid.NewGuid().ToString();
            var session = new SessionData
            {
                SessionId = sessionId,
                UserId = userId,
                DisplayName = displayName,
                IsGuest = isGuest,
                CreatedAt = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(sessionTimeoutMinutes)
            };
            
            _sessions[sessionId] = session;
            
            if (debugMode)
                Debug.Log($"[SessionManager] Session created - ID: {sessionId}, User: {userId}");
            
            OnSessionCreated?.Invoke(sessionId);
        }
        
        /// <summary>
        /// Associates a client with a session
        /// </summary>
        public void AssignSessionToClient(ulong clientId, string sessionId)
        {
            if (!IsServer)
            {
                Debug.LogError("[SessionManager] AssignSessionToClient can only be called from server");
                return;
            }
            
            if (_sessions.ContainsKey(sessionId))
            {
                _clientToSession[clientId] = sessionId;
                
                if (debugMode)
                    Debug.Log($"[SessionManager] Client {clientId} assigned to session {sessionId}");
                
                // Send session data to client
                var session = _sessions[sessionId];
                SendSessionDataClientRpc(session, new ClientRpcParams 
                { 
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } 
                });
            }
        }
        
        /// <summary>
        /// Updates session activity timestamp
        /// </summary>
        public void UpdateSessionActivity(string sessionId)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                session.LastActivity = DateTime.UtcNow;
                session.ExpiresAt = DateTime.UtcNow.AddMinutes(sessionTimeoutMinutes);
                
                OnSessionUpdated?.Invoke(sessionId, session);
                
                if (debugMode)
                    Debug.Log($"[SessionManager] Session activity updated: {sessionId}");
            }
        }
        
        /// <summary>
        /// Gets session data by session ID
        /// </summary>
        public SessionData GetSession(string sessionId)
        {
            return _sessions.TryGetValue(sessionId, out var session) ? session : null;
        }
        
        /// <summary>
        /// Gets session data by client ID (server only)
        /// </summary>
        public SessionData GetClientSession(ulong clientId)
        {
            if (_clientToSession.TryGetValue(clientId, out var sessionId))
            {
                return GetSession(sessionId);
            }
            return null;
        }
        
        /// <summary>
        /// Destroys a session
        /// </summary>
        public void DestroySession(string sessionId)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                _sessions.Remove(sessionId);
                
                // Remove client association
                var clientId = _clientToSession.FirstOrDefault(kvp => kvp.Value == sessionId).Key;
                if (clientId != 0)
                {
                    _clientToSession.Remove(clientId);
                }
                
                if (debugMode)
                    Debug.Log($"[SessionManager] Session destroyed: {sessionId}");
                
                OnSessionExpired?.Invoke(sessionId);
                
                // Notify client if server
                if (IsServer && clientId != 0)
                {
                    SendSessionExpiredClientRpc(sessionId, new ClientRpcParams 
                    { 
                        Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } 
                    });
                }
            }
        }
        
        /// <summary>
        /// Gets all active sessions (server only)
        /// </summary>
        public IEnumerable<SessionData> GetActiveSessions()
        {
            return _sessions.Values.Where(s => s.IsValid);
        }
        
        /// <summary>
        /// Gets session count
        /// </summary>
        public int GetSessionCount()
        {
            return _sessions.Count;
        }
        
        #endregion

        #region Server Operations
        
        private void CleanupExpiredSessions()
        {
            var expiredSessions = _sessions.Values
                .Where(s => s.IsExpired)
                .ToList();
            
            foreach (var session in expiredSessions)
            {
                if (debugMode)
                    Debug.Log($"[SessionManager] Cleaning up expired session: {session.SessionId}");
                
                DestroySession(session.SessionId);
            }
        }
        
        #endregion

        #region Client RPCs
        
        [ClientRpc]
        private void SendSessionDataClientRpc(SessionData sessionData, ClientRpcParams rpcParams = default)
        {
            _localSession = sessionData;
            
            if (enableSessionPersistence)
            {
                SaveLocalSession();
            }
            
            if (debugMode)
                Debug.Log($"[SessionManager] Received session data: {sessionData.SessionId}");
        }
        
        [ClientRpc]
        private void SendSessionExpiredClientRpc(string sessionId, ClientRpcParams rpcParams = default)
        {
            if (_localSession?.SessionId == sessionId)
            {
                _localSession = null;
                
                if (enableSessionPersistence)
                {
                    ClearLocalSession();
                }
                
                if (debugMode)
                    Debug.Log($"[SessionManager] Local session expired: {sessionId}");
                
                OnSessionExpired?.Invoke(sessionId);
            }
        }
        
        #endregion

        #region Server RPCs
        
        [ServerRpc(RequireOwnership = false)]
        public void UpdateActivityServerRpc(ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            
            if (_clientToSession.TryGetValue(clientId, out var sessionId))
            {
                UpdateSessionActivity(sessionId);
            }
        }
        
        #endregion

        #region Network Events
        
        public void OnClientDisconnected(ulong clientId)
        {
            if (IsServer && _clientToSession.TryGetValue(clientId, out var sessionId))
            {
                if (debugMode)
                    Debug.Log($"[SessionManager] Client {clientId} disconnected, destroying session {sessionId}");
                
                DestroySession(sessionId);
            }
        }
        
        #endregion

        #region Persistence
        
        private void SaveLocalSession()
        {
            if (_localSession == null) return;
            
            try
            {
                var json = JsonUtility.ToJson(_localSession);
                PlayerPrefs.SetString("LocalSession", json);
                PlayerPrefs.Save();
                
                if (debugMode)
                    Debug.Log("[SessionManager] Local session saved");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SessionManager] Failed to save local session: {ex.Message}");
            }
        }
        
        private void LoadLocalSession()
        {
            try
            {
                var json = PlayerPrefs.GetString("LocalSession", "");
                if (!string.IsNullOrEmpty(json))
                {
                    _localSession = JsonUtility.FromJson<SessionData>(json);
                    
                    // Validate loaded session
                    if (_localSession.IsExpired)
                    {
                        _localSession = null;
                        ClearLocalSession();
                        
                        if (debugMode)
                            Debug.Log("[SessionManager] Loaded session was expired, cleared");
                    }
                    else if (debugMode)
                    {
                        Debug.Log($"[SessionManager] Local session loaded: {_localSession.SessionId}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SessionManager] Failed to load local session: {ex.Message}");
                ClearLocalSession();
            }
        }
        
        private void ClearLocalSession()
        {
            PlayerPrefs.DeleteKey("LocalSession");
            PlayerPrefs.Save();
            
            if (debugMode)
                Debug.Log("[SessionManager] Local session cleared");
        }
        
        #endregion
    }

    /// <summary>
    /// Represents session data for an authenticated user
    /// </summary>
    [System.Serializable]
    public class SessionData
    {
        public string SessionId;
        public string UserId;
        public string DisplayName;
        public bool IsGuest;
        public DateTime CreatedAt;
        public DateTime LastActivity;
        public DateTime ExpiresAt;
        
        // Additional session properties
        public Dictionary<string, string> Properties = new Dictionary<string, string>();
        
        public bool IsValid => DateTime.UtcNow < ExpiresAt && !string.IsNullOrEmpty(SessionId);
        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
        public TimeSpan TimeRemaining => ExpiresAt - DateTime.UtcNow;
        public TimeSpan SessionDuration => DateTime.UtcNow - CreatedAt;
        
        /// <summary>
        /// Sets a custom property for this session
        /// </summary>
        public void SetProperty(string key, string value)
        {
            Properties[key] = value;
        }
        
        /// <summary>
        /// Gets a custom property for this session
        /// </summary>
        public string GetProperty(string key, string defaultValue = null)
        {
            return Properties.TryGetValue(key, out var value) ? value : defaultValue;
        }
    }
}