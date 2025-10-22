using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DebugViewController : MonoBehaviour
{
    public static DebugViewController Instance { get; private set; }

    [Header("Debug Viewer Components")]
    [SerializeField] private Transform debugContentTransform;
    [SerializeField] private GameObject debugOutputPrefab;

    [Header("Debug Control Buttons")]
    [SerializeField] private Button connectButton;
    [SerializeField] private Button disconnectButton;
    [SerializeField] private Button sendPingButton;
    [SerializeField] private Button clearLogsButton;

    [Header("Optional: WS_Client Reference")]
    [SerializeField] private WS_Client wsClient;

    [Header("Debug Settings")]
    [SerializeField] private int maxLogLines = 100;

    private Queue<GameObject> logGameObjects = new Queue<GameObject>();
    private bool lastKnownConnectionState = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            AddDebugMessage("DebugViewController Instance created");
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
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

        UpdateConnectionButtons(false);
        LogPlatformInfo();
    }

    private void OnEnable()
    {
        AddDebugMessage("=== DebugViewController OnEnable() FIRED ===");
        StartCoroutine(CoSyncUIWithConnectionState());
    }

    private IEnumerator CoSyncUIWithConnectionState()
    {
        yield return null; // Wait a frame so Unity UI is fully set up

        if (WS_Client.Instance != null)
        {
            bool connectionState = WS_Client.Instance.IsConnected;
            AddDebugMessage($"CoSyncUI: WS_Client.Instance.IsConnected = {connectionState}");
            UpdateConnectionButtons(connectionState);
            lastKnownConnectionState = connectionState;
            AddDebugMessage($"CoSyncUI: UpdateConnectionButtons called with {connectionState}");
        }
        else
        {
            AddDebugMessage("CoSyncUI: ERROR - WS_Client.Instance is NULL!");
        }
    }

    private void Update()
    {
        if (WS_Client.Instance != null)
        {
            bool currentState = WS_Client.Instance.IsConnected;
            if (currentState != lastKnownConnectionState)
            {
                AddDebugMessage($"Update: State change detected {lastKnownConnectionState} → {currentState}");
                lastKnownConnectionState = currentState;
                UpdateConnectionButtons(currentState);
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void OnTabOpened()
    {
        AddDebugMessage("=== OnTabOpened FIRED ===");
        AddDebugMessage("Debug Viewer Opened");

        if (WS_Client.Instance != null)
        {
            bool state = WS_Client.Instance.IsConnected;
            AddDebugMessage($"OnTabOpened: Connection state = {state}");
            UpdateConnectionButtons(state);
            lastKnownConnectionState = state;

            Canvas.ForceUpdateCanvases();
        }
        else
        {
            AddDebugMessage("OnTabOpened: ERROR - WS_Client.Instance is NULL!");
        }
    }

    private void LogPlatformInfo()
    {
        AddDebugMessage($"Platform: {Application.platform}");

#if UNITY_IOS && !UNITY_EDITOR
        AddDebugMessage("Running on iOS device - mTLS WebSocket available");
#elif UNITY_EDITOR
        AddDebugMessage("Running in Unity Editor - mTLS WebSocket NOT available");
#else
        AddDebugMessage($"Running on {Application.platform} - mTLS WebSocket NOT available");
#endif

        if (wsClient == null)
        {
            AddDebugMessage("WARNING: WS_Client reference is not assigned!");
        }
    }

    // ===== DEBUG CONTROL FUNCTIONS =====

    public void ToggleConnection()
    {
        if (WS_Client.Instance == null)
        {
            AddDebugMessage("ERROR: WS_Client.Instance is null!");
            return;
        }

        if (WS_Client.Instance.IsConnected)
        {
            AddDebugMessage("ToggleConnection: Calling OnDisconnectClicked");
            OnDisconnectClicked();
        }
        else
        {
            AddDebugMessage("ToggleConnection: Calling OnConnectClicked");
            OnConnectClicked();
        }
    }

    public void OnConnectClicked()
    {
        AddDebugMessage("=== Connection Attempt Started ===");

        if (wsClient == null)
        {
            AddDebugMessage("ERROR: WS_Client reference is not assigned");
            UpdateConnectionButtons(false);
            return;
        }

        if (connectButton != null)
        {
            connectButton.interactable = false;
        }

        wsClient.ConnectWebSocket();
    }

    public void OnDisconnectClicked()
    {
        AddDebugMessage("=== Manual Disconnect Requested ===");

        if (wsClient == null)
        {
            AddDebugMessage("ERROR: WS_Client reference is not assigned");
            return;
        }

        wsClient.ManualDisconnect();
    }

    public void OnSendPingClicked()
    {
        if (wsClient == null)
        {
            AddDebugMessage("ERROR: WS_Client reference is not assigned");
            return;
        }

        wsClient.SendPingMessage();
    }

    public static void UpdateConnectionButtons(bool isConnected)
    {
        if (Instance == null) return;

        AddDebugMessage($"UpdateConnectionButtons called: isConnected={isConnected}");

        if (Instance.connectButton != null)
        {
            Instance.connectButton.gameObject.SetActive(false);
            Instance.connectButton.interactable = !isConnected;
            Instance.connectButton.gameObject.SetActive(true);
            AddDebugMessage($"  Connect button interactable = {!isConnected}");
        }
        else
        {
            AddDebugMessage("  ERROR: connectButton is NULL!");
        }

        if (Instance.disconnectButton != null)
        {
            Instance.disconnectButton.gameObject.SetActive(false);
            Instance.disconnectButton.interactable = isConnected;
            Instance.disconnectButton.gameObject.SetActive(true);
            AddDebugMessage($"  Disconnect button interactable = {isConnected}");
        }
        else
        {
            AddDebugMessage("  ERROR: disconnectButton is NULL!");
        }

        if (Instance.sendPingButton != null)
        {
            Instance.sendPingButton.gameObject.SetActive(false);
            Instance.sendPingButton.interactable = isConnected;
            Instance.sendPingButton.gameObject.SetActive(true);
            AddDebugMessage($"  SendPing button interactable = {isConnected}");
        }
        else
        {
            AddDebugMessage("  ERROR: sendPingButton is NULL!");
        }

        Canvas.ForceUpdateCanvases();
        AddDebugMessage("  Forced Canvas update + GameObject toggle");
    }

    public static void AddDebugMessage(string message)
    {
        if (Instance == null)
        {
            Debug.Log($"[DebugView] {message}");
            return;
        }

        string timestamp = System.DateTime.Now.ToString("HH:mm:ss.fff");
        string formattedMsg = $"[{timestamp}] {message}";

        if (Instance.debugContentTransform == null)
        {
            Debug.LogError("DebugViewController: debugContentTransform is not assigned!");
            return;
        }

        if (Instance.debugOutputPrefab == null)
        {
            Debug.LogError("DebugViewController: debugOutputPrefab is not assigned!");
            return;
        }

        GameObject newLogEntry = Instantiate(Instance.debugOutputPrefab, Instance.debugContentTransform);
        newLogEntry.transform.SetAsLastSibling();

        TextMeshProUGUI textComponent = newLogEntry.GetComponent<TextMeshProUGUI>();
        if (textComponent != null)
        {
            textComponent.text = formattedMsg;
        }
        else
        {
            Debug.LogError("DebugViewController: DebugOutputPrefab does not have TextMeshProUGUI component!");
        }

        Instance.logGameObjects.Enqueue(newLogEntry);

        if (Instance.logGameObjects.Count > Instance.maxLogLines)
        {
            GameObject oldestLog = Instance.logGameObjects.Dequeue();
            Destroy(oldestLog);
        }

        Canvas.ForceUpdateCanvases();
        Debug.Log(formattedMsg);
    }

    public void ClearDebugLog()
    {
        while (logGameObjects.Count > 0)
        {
            GameObject logEntry = logGameObjects.Dequeue();
            if (logEntry != null)
            {
                Destroy(logEntry);
            }
        }

        AddDebugMessage("--- Logs Cleared ---");
    }
}