using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Server;

namespace Client
{
    public class LobbyUI : MonoBehaviour
    {
        [Header("Room UI")]
        [SerializeField] private TMP_Text roomCodeText;
        [SerializeField] private TMP_Text statusText;

        [Header("Player Slots")]
        // Giữ nguyên các tham chiếu/logic UpdatePlayerSlot(...) hiện có
        // [SerializeField] private PlayerSlotUI slot1; ...

        [Header("Ready System")]
        [SerializeField] private Toggle readyToggle;
        [SerializeField] private Button startGameButton;
        [SerializeField] private TMP_Text startButtonText;

        private bool _isReady = false;
        private bool _isOwner = false;
        private bool _isInRoom = false;

        void Start()
        {
            // ... existing code (nếu có)
            if (readyToggle != null)
                readyToggle.onValueChanged.AddListener(OnReadyToggleChanged);

            if (startGameButton != null)
                startGameButton.onClick.AddListener(OnStartGameClicked);

            // Ẩn nút Start lúc đầu
            if (startGameButton != null)
                startGameButton.gameObject.SetActive(false);
        }

        /// <summary>
        /// User gạt Ready/Not Ready.
        /// </summary>
        void OnReadyToggleChanged(bool isReady)
        {
            _isReady = isReady;
            UpdateStatus(isReady ? "You are ready!" : "Not ready");

            var lobbyManager = FindObjectOfType<Server.LobbyManager>();
            if (lobbyManager != null)
            {
                try
                {
                    lobbyManager.SetReadyServerRpc(isReady);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[CLIENT] SetReadyServerRpc failed: {e.Message}");
                    ShowError("Failed to send ready state to server.");
                }
            }
            else
            {
                ShowError("LobbyManager not found.");
            }
        }

        /// <summary>
        /// Chỉ chủ phòng mới được Start Game.
        /// </summary>
        void OnStartGameClicked()
        {
            if (!_isOwner)
            {
                UpdateStatus("Only room owner can start the game");
                return;
            }

            UpdateStatus("Starting game...");
            if (startGameButton != null) startGameButton.interactable = false;

            var lobbyManager = FindObjectOfType<Server.LobbyManager>();
            if (lobbyManager != null)
            {
                try
                {
                    lobbyManager.StartGameServerRpc();
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[CLIENT] StartGameServerRpc failed: {e.Message}");
                    ShowError("Failed to start game.");
                }
            }
            else
            {
                ShowError("LobbyManager not found.");
            }
        }

        /// <summary>
        /// Gọi từ Network layer khi có RoomStateUpdate.
        /// </summary>
        public void OnRoomStateUpdate(RoomStateUpdate state)
        {
            Debug.Log($"[LOBBY UI] Room state updated: {state.RoomId}");
            _isInRoom = true;
            ShowCurrentRoom();

            // Update room code
            if (roomCodeText != null)
                roomCodeText.text = $"Room Code: {state.RoomId}";

            // Xác định owner hiện tại
            if (ClientGameState.Instance?.CurrentUser != null)
            {
                _isOwner = state.OwnerUserId.ToString() == ClientGameState.Instance.CurrentUser.Id;
            }
            else
            {
                _isOwner = false;
            }

            // Cập nhật player slots
            UpdatePlayerSlot(0, state.Player1);
            UpdatePlayerSlot(1, state.Player2);
            UpdatePlayerSlot(2, state.Player3);
            UpdatePlayerSlot(3, state.Player4);

            // Cập nhật nút Start
            if (startGameButton != null && startButtonText != null)
            {
                if (_isOwner)
                {
                    startGameButton.gameObject.SetActive(true);
                    startGameButton.interactable = state.CanStart;

                    if (state.CanStart)
                    {
                        startButtonText.text = "START GAME";
                        startButtonText.color = Color.green;
                    }
                    else
                    {
                        startButtonText.text = "Waiting for players to ready...";
                        startButtonText.color = Color.gray;
                    }
                }
                else
                {
                    startGameButton.gameObject.SetActive(false);
                }
            }

            UpdateStatus($"In room with {state.PlayerCount} player(s)");
        }

        /// <summary>
        /// Hiển thị lỗi và re-enable các nút nếu cần.
        /// </summary>
        public void ShowError(string message)
        {
            UpdateStatus($"Error: {message}");
            if (startGameButton != null)
                startGameButton.interactable = true;
        }

        // =========================
        // Các hàm đã có sẵn trong class – giữ nguyên hoặc tuỳ chỉnh
        // =========================

        private void UpdateStatus(string msg)
        {
            if (statusText != null)
                statusText.text = msg;
            Debug.Log($"[LOBBY UI] {msg}");
        }

        private void ShowCurrentRoom()
        {
            // Tuỳ theo UI của chị (bật panel lobby, ẩn panel trước đó, v.v.)
        }

        private void UpdatePlayerSlot(int index, PlayerInfo p)
        {
            // Map dữ liệu player lên UI slot tương ứng.
            // Ví dụ: slots[index].Set(p);
        }
    }

    // ====== Các kiểu dữ liệu tham chiếu (giữ nguyên nếu đã tồn tại trong project) ======
    [System.Serializable]
    public class RoomStateUpdate
    {
        public string RoomId;
        public System.Guid OwnerUserId;
        public int PlayerCount;
        public bool CanStart;

        public PlayerInfo Player1;
        public PlayerInfo Player2;
        public PlayerInfo Player3;
        public PlayerInfo Player4;
    }

    [System.Serializable]
    public class PlayerInfo
    {
        public string UserId;
        public string DisplayName;
        public bool IsReady;
        public bool IsOwner;
    }

    public class ClientGameState
    {
        public static ClientGameState Instance;
        public CurrentUserInfo CurrentUser;

        public class CurrentUserInfo
        {
            public string Id;
            public string Name;
        }
    }
}
