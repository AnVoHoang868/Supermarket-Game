using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_SERVER
using Server.Database;
#endif

namespace Core
{
    public class NetworkBootstrap : MonoBehaviour
    {
        [Header("Network Settings")]
        [SerializeField] private string serverIP = "127.0.0.1";
        [SerializeField] private ushort port = 7777;
        
        [Header("Scene References")]
        [SerializeField] private string loginSceneName = "LoginScene";
        [SerializeField] private string lobbySceneName = "LobbyScene";

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // Kiểm tra xem đang chạy ở chế độ nào
            if (Application.isBatchMode)
            {
                // Headless Server Mode
                StartAsServer();
            }
            else
            {
                // Client Mode
                StartAsClient();
            }
        }

        private void StartAsServer()
        {
            Debug.Log("[BOOTSTRAP] ========== STARTING AS SERVER ==========");
            
            #if UNITY_SERVER
            // Initialize Database Service
            GameObject dbObject = new GameObject("DatabaseService");
            DontDestroyOnLoad(dbObject);
            var dbService = dbObject.AddComponent<DatabaseService>();
            dbService.Initialize();
            
            Debug.Log("[BOOTSTRAP] Database initialized");
            #endif
            
            // Configure transport
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetConnectionData("0.0.0.0", port);
            
            // Start server
            bool started = NetworkManager.Singleton.StartServer();
            
            if (started)
            {
                Debug.Log($"[BOOTSTRAP] Server started successfully on port {port}");
                
                // Đăng ký callbacks
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            }
            else
            {
                Debug.LogError("[BOOTSTRAP] Failed to start server!");
            }
        }

        private void StartAsClient()
        {
            Debug.Log("[BOOTSTRAP] ========== STARTING AS CLIENT ==========");
            
            // Load Login Scene
            if (!string.IsNullOrEmpty(loginSceneName))
            {
                SceneManager.LoadScene(loginSceneName);
                Debug.Log($"[BOOTSTRAP] Loading {loginSceneName}");
            }
            else
            {
                Debug.LogError("[BOOTSTRAP] Login scene name not set!");
            }
        }

        /// <summary>
        /// Gọi từ UI để connect đến server
        /// </summary>
        public void ConnectToServer(string ip)
        {
            Debug.Log($"[BOOTSTRAP] Attempting to connect to {ip}:{port}");
            
            // Configure transport với IP từ input
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetConnectionData(ip, port);
            
            // Đăng ký callbacks trước khi connect
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedToServer;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectedFromServer;
            
            // Start client
            bool started = NetworkManager.Singleton.StartClient();
            
            if (!started)
            {
                Debug.LogError("[BOOTSTRAP] Failed to start client!");
            }
        }

        #region Server Callbacks
        
        private void OnClientConnected(ulong clientId)
        {
            Debug.Log($"[SERVER] Client {clientId} connected");
        }

        private void OnClientDisconnected(ulong clientId)
        {
            Debug.Log($"[SERVER] Client {clientId} disconnected");
        }
        
        #endregion

        #region Client Callbacks
        
        private void OnClientConnectedToServer(ulong clientId)
        {
            Debug.Log($"[CLIENT] Successfully connected to server! ClientId: {clientId}");
        }

        private void OnClientDisconnectedFromServer(ulong clientId)
        {
            Debug.LogWarning($"[CLIENT] Disconnected from server");
            
            // Quay về màn hình login
            if (!string.IsNullOrEmpty(loginSceneName))
            {
                SceneManager.LoadScene(loginSceneName);
            }
        }
        
        #endregion

        private void OnDestroy()
        {
            // Cleanup callbacks
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedToServer;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnectedFromServer;
            }
        }
    }
}