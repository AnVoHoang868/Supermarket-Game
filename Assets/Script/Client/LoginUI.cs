using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Core;

namespace Client
{
    public class LoginUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_InputField serverIPInput;
        [SerializeField] private TMP_InputField displayNameInput;
        [SerializeField] private Button connectButton;
        [SerializeField] private TMP_Text statusText;

        [Header("Validation")]
        [SerializeField] private int minNameLength = 3;
        [SerializeField] private int maxNameLength = 20;

        private NetworkBootstrap networkBootstrap;

        private void Start()
        {
            // Find NetworkBootstrap (DontDestroyOnLoad object)
            networkBootstrap = FindObjectOfType<NetworkBootstrap>();
            
            if (networkBootstrap == null)
            {
                Debug.LogError("[LoginUI] NetworkBootstrap not found! Make sure BootScene loaded first.");
                SetStatus("ERROR: Network system not initialized!", Color.red);
                connectButton.interactable = false;
                return;
            }

            // Setup button
            connectButton.onClick.AddListener(OnConnectClicked);
            
            // Setup input field listeners
            serverIPInput.onValueChanged.AddListener(OnInputChanged);
            displayNameInput.onValueChanged.AddListener(OnInputChanged);
            
            // Initial validation
            ValidateInputs();
            
            SetStatus("Ready to connect", Color.white);
        }

        private void OnInputChanged(string value)
        {
            ValidateInputs();
        }

        private void ValidateInputs()
        {
            bool isValid = true;
            
            // Validate server IP
            string ip = serverIPInput.text.Trim();
            if (string.IsNullOrEmpty(ip))
            {
                isValid = false;
            }
            
            // Validate display name
            string displayName = displayNameInput.text.Trim();
            if (displayName.Length < minNameLength || displayName.Length > maxNameLength)
            {
                isValid = false;
            }
            
            connectButton.interactable = isValid;
        }

        private void OnConnectClicked()
        {
            string ip = serverIPInput.text.Trim();
            string displayName = displayNameInput.text.Trim();

            // Validate
            if (string.IsNullOrEmpty(ip))
            {
                SetStatus("Please enter server IP!", Color.red);
                return;
            }

            if (displayName.Length < minNameLength)
            {
                SetStatus($"Name must be at least {minNameLength} characters!", Color.red);
                return;
            }

            if (displayName.Length > maxNameLength)
            {
                SetStatus($"Name must be less than {maxNameLength} characters!", Color.red);
                return;
            }

            // Disable button để tránh spam
            connectButton.interactable = false;
            SetStatus($"Connecting to {ip}...", Color.yellow);

            // Lưu display name vào PlayerPrefs (dùng sau khi login)
            PlayerPrefs.SetString("DisplayName", displayName);
            PlayerPrefs.Save();

            // Connect
            networkBootstrap.ConnectToServer(ip);

            // Re-enable sau 3 giây
            Invoke(nameof(ReEnableButton), 3f);
        }

        private void ReEnableButton()
        {
            connectButton.interactable = true;
        }

        private void SetStatus(string message, Color color)
        {
            if (statusText != null)
            {
                statusText.text = message;
                statusText.color = color;
            }
            Debug.Log($"[LoginUI] {message}");
        }

        private void OnDestroy()
        {
            if (connectButton != null)
            {
                connectButton.onClick.RemoveListener(OnConnectClicked);
            }
        }
    }
}