using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Shared.Networking;

namespace Client
{
    /// <summary>
    /// Example UI controller demonstrating how to use AuthManager and SessionManager
    /// This shows how to integrate authentication and session management in a Unity UI
    /// </summary>
    public class AuthUIExample : MonoBehaviour
    {
        [Header("Login UI")]
        [SerializeField] private GameObject loginPanel;
        [SerializeField] private TMP_InputField displayNameInput;
        [SerializeField] private Button guestLoginButton;
        [SerializeField] private TMP_InputField usernameInput;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField] private Button loginButton;
        
        [Header("Session UI")]
        [SerializeField] private GameObject sessionPanel;
        [SerializeField] private TMP_Text userDisplayText;
        [SerializeField] private TMP_Text sessionInfoText;
        [SerializeField] private Button logoutButton;
        [SerializeField] private Button refreshSessionButton;
        
        [Header("Status")]
        [SerializeField] private TMP_Text statusText;
        
        private AuthManager _authManager;
        private SessionManager _sessionManager;
        
        void Start()
        {
            InitializeUI();
            SetupAuthEvents();
        }
        
        void Update()
        {
            UpdateSessionInfo();
        }
        
        private void InitializeUI()
        {
            // Setup button listeners
            if (guestLoginButton != null)
                guestLoginButton.onClick.AddListener(OnGuestLoginClicked);
            
            if (loginButton != null)
                loginButton.onClick.AddListener(OnLoginClicked);
            
            if (logoutButton != null)
                logoutButton.onClick.AddListener(OnLogoutClicked);
            
            if (refreshSessionButton != null)
                refreshSessionButton.onClick.AddListener(OnRefreshSessionClicked);
            
            // Set initial UI state
            ShowLoginPanel();
            UpdateStatus("Ready to login");
        }
        
        private void SetupAuthEvents()
        {
            // Wait for AuthManager to be available
            if (AuthManager.Instance == null)
            {
                Invoke(nameof(SetupAuthEvents), 0.1f);
                return;
            }
            
            _authManager = AuthManager.Instance;
            _sessionManager = SessionManager.Instance;
            
            // Subscribe to auth events
            _authManager.OnUserAuthenticated += OnUserAuthenticated;
            _authManager.OnUserLoggedOut += OnUserLoggedOut;
            _authManager.OnAuthenticationFailed += OnAuthenticationFailed;
            
            // Subscribe to session events
            if (_sessionManager != null)
            {
                _sessionManager.OnSessionCreated += OnSessionCreated;
                _sessionManager.OnSessionExpired += OnSessionExpired;
                _sessionManager.OnSessionUpdated += OnSessionUpdated;
            }
            
            // Check if already authenticated
            if (_authManager.IsAuthenticated)
            {
                ShowSessionPanel();
                UpdateUserDisplay(_authManager.LocalUserId, _authManager.LocalDisplayName);
            }
        }
        
        #region UI Event Handlers
        
        private void OnGuestLoginClicked()
        {
            var displayName = displayNameInput?.text?.Trim();
            
            if (string.IsNullOrEmpty(displayName))
            {
                UpdateStatus("Please enter a display name");
                return;
            }
            
            UpdateStatus("Logging in as guest...");
            _authManager.LoginAsGuest(displayName);
        }
        
        private void OnLoginClicked()
        {
            var username = usernameInput?.text?.Trim();
            var password = passwordInput?.text?.Trim();
            
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                UpdateStatus("Please enter username and password");
                return;
            }
            
            UpdateStatus("Logging in...");
            _authManager.Login(username, password);
        }
        
        private void OnLogoutClicked()
        {
            UpdateStatus("Logging out...");
            _authManager.Logout();
        }
        
        private void OnRefreshSessionClicked()
        {
            if (_sessionManager != null)
            {
                _sessionManager.UpdateActivityServerRpc();
                UpdateStatus("Session activity updated");
            }
        }
        
        #endregion
        
        #region Auth Event Handlers
        
        private void OnUserAuthenticated(string userId, string displayName)
        {
            UpdateStatus($"Welcome, {displayName}!");
            ShowSessionPanel();
            UpdateUserDisplay(userId, displayName);
        }
        
        private void OnUserLoggedOut(string userId)
        {
            UpdateStatus("Logged out successfully");
            ShowLoginPanel();
        }
        
        private void OnAuthenticationFailed(string errorMessage)
        {
            UpdateStatus($"Login failed: {errorMessage}");
        }
        
        #endregion
        
        #region Session Event Handlers
        
        private void OnSessionCreated(string sessionId)
        {
            Debug.Log($"[AuthUIExample] Session created: {sessionId}");
        }
        
        private void OnSessionExpired(string sessionId)
        {
            UpdateStatus("Session expired. Please login again.");
            ShowLoginPanel();
        }
        
        private void OnSessionUpdated(string sessionId, SessionData sessionData)
        {
            Debug.Log($"[AuthUIExample] Session updated: {sessionId}");
        }
        
        #endregion
        
        #region UI Updates
        
        private void ShowLoginPanel()
        {
            if (loginPanel != null) loginPanel.SetActive(true);
            if (sessionPanel != null) sessionPanel.SetActive(false);
        }
        
        private void ShowSessionPanel()
        {
            if (loginPanel != null) loginPanel.SetActive(false);
            if (sessionPanel != null) sessionPanel.SetActive(true);
        }
        
        private void UpdateStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;
            
            Debug.Log($"[AuthUIExample] Status: {message}");
        }
        
        private void UpdateUserDisplay(string userId, string displayName)
        {
            if (userDisplayText != null)
                userDisplayText.text = $"{displayName}\nID: {userId}";
        }
        
        private void UpdateSessionInfo()
        {
            if (sessionInfoText == null || _sessionManager == null) return;
            
            var session = _sessionManager.LocalSession;
            if (session != null)
            {
                var timeRemaining = session.TimeRemaining;
                var sessionDuration = session.SessionDuration;
                
                sessionInfoText.text = $"Session: {session.SessionId}\n" +
                                     $"Type: {(session.IsGuest ? "Guest" : "Registered")}\n" +
                                     $"Duration: {sessionDuration:hh\\:mm\\:ss}\n" +
                                     $"Expires in: {timeRemaining:hh\\:mm\\:ss}";
            }
            else
            {
                sessionInfoText.text = "No active session";
            }
        }
        
        #endregion
        
        void OnDestroy()
        {
            // Cleanup event subscriptions
            if (_authManager != null)
            {
                _authManager.OnUserAuthenticated -= OnUserAuthenticated;
                _authManager.OnUserLoggedOut -= OnUserLoggedOut;
                _authManager.OnAuthenticationFailed -= OnAuthenticationFailed;
            }
            
            if (_sessionManager != null)
            {
                _sessionManager.OnSessionCreated -= OnSessionCreated;
                _sessionManager.OnSessionExpired -= OnSessionExpired;
                _sessionManager.OnSessionUpdated -= OnSessionUpdated;
            }
        }
    }
}