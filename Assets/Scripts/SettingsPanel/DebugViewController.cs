using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DebugViewController : MonoBehaviour
{
    [Header("Debug Viewer Components")]
    [SerializeField] private Transform debugContentTransform; // Content GameObject inside ScrollView
    [SerializeField] private GameObject debugOutputPrefab; // Prefab for each log line (TextMeshPro)

    [Header("Debug Control Buttons")]
    [SerializeField] private Button connectButton;
    [SerializeField] private Button disconnectButton;
    [SerializeField] private Button sendPingButton;
    [SerializeField] private Button clearLogsButton;

    [Header("Optional: WS_Client Reference")]
    [SerializeField] private WS_Client wsClient; // Optional if you have websocket functionality

    [Header("Debug Settings")]
    [SerializeField] private int maxLogLines = 100;

    private Queue<GameObject> logGameObjects = new Queue<GameObject>();
    private static DebugViewController instance;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        // Setup debug button listeners
        if (connectButton != null)
        {
            connectButton.onClick.AddListener(OnConnectClicked);
        }
        if (disconnectButton != null)
        {
            disconnectButton.onClick.AddListener(OnDisconnectClicked);
        }
        if (sendPingButton != null)
        {
            sendPingButton.onClick.AddListener(OnSendPingClicked);
        }
        if (clearLogsButton != null)
        {
            clearLogsButton.onClick.AddListener(ClearDebugLog);
        }

        // Initialize connection buttons state
        UpdateConnectionButtons(false);
    }

    private void OnDestroy()
    {
        instance = null;
    }

    public void OnTabOpened()
    {
        // Called when this tab is opened
        Debug.Log("Debug Viewer Opened");
    }

    // ===== DEBUG CONTROL FUNCTIONS =====

    public void OnConnectClicked()
    {
        AddDebugMessage("=== Connection Attempt Started ===");

        if (connectButton != null)
        {
            connectButton.interactable = false;
        }
        if (wsClient != null)
        {
            wsClient.ConnectWebSocket();
        }
    }

    public void OnDisconnectClicked()
    {
        AddDebugMessage("=== Manual Disconnect Requested ===");
        if (wsClient != null)
        {
            wsClient.ManualDisconnect();
        }
    }

    public void OnSendPingClicked()
    {
        if (wsClient != null)
        {
            wsClient.SendPingMessage();
        }
    }

    public static void UpdateConnectionButtons(bool isConnected)
    {
        if (instance == null) return;

        if (instance.connectButton != null)
        {
            instance.connectButton.interactable = !isConnected;
        }
        if (instance.disconnectButton != null)
        {
            instance.disconnectButton.interactable = isConnected;
        }
        if (instance.sendPingButton != null)
        {
            instance.sendPingButton.interactable = isConnected;
        }
    }

    public static void AddDebugMessage(string message)
    {
        if (instance == null) return;

        string timestamp = System.DateTime.Now.ToString("HH:mm:ss.fff");
        string formattedMsg = $"[{timestamp}] {message}";

        // Create new DebugOutput GameObject
        GameObject newLogEntry = Instantiate(instance.debugOutputPrefab, instance.debugContentTransform);

        // Set it as last sibling to appear at bottom
        newLogEntry.transform.SetAsLastSibling();

        // Get the TextMeshProUGUI component and set text
        TextMeshProUGUI textComponent = newLogEntry.GetComponent<TextMeshProUGUI>();
        if (textComponent != null)
        {
            textComponent.text = formattedMsg;
        }

        // Add to queue
        instance.logGameObjects.Enqueue(newLogEntry);

        // Remove oldest if exceeding limit
        if (instance.logGameObjects.Count > instance.maxLogLines)
        {
            GameObject oldestLog = instance.logGameObjects.Dequeue();
            Destroy(oldestLog);
        }

        // Force canvas update for ScrollView
        Canvas.ForceUpdateCanvases();

        // Log to Unity console for debugging
        Debug.Log(formattedMsg);
    }

    public void ClearDebugLog()
    {
        // Destroy all existing log GameObjects
        while (logGameObjects.Count > 0)
        {
            GameObject logEntry = logGameObjects.Dequeue();
            if (logEntry != null)
            {
                Destroy(logEntry);
            }
        }

        // Add confirmation message after clearing
        AddDebugMessage("--- Logs Cleared ---");
    }
}
