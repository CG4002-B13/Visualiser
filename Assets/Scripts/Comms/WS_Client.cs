using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Networking;
using AOT;

public class WS_Client : MonoBehaviour
{
    public static WS_Client Instance { get; private set; }

#if UNITY_IOS && !UNITY_EDITOR
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void MessageCallback(string message);
    
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ErrorCallback(string error);
    
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ConnectCallback();

    [DllImport("__Internal")]
    private static extern IntPtr _CreateWebSocket(string url, string certPath, string certPassword);
    
    [DllImport("__Internal")]
    private static extern void _ConnectWebSocket(IntPtr instance);
    
    [DllImport("__Internal")]
    private static extern void _SendMessage(IntPtr instance, string message);
    
    [DllImport("__Internal")]
    private static extern void _CloseWebSocket(IntPtr instance);
    
    [DllImport("__Internal")]
    private static extern void _SetMessageCallback(IntPtr instance, MessageCallback callback);
    
    [DllImport("__Internal")]
    private static extern void _SetErrorCallback(IntPtr instance, ErrorCallback callback);
    
    [DllImport("__Internal")]
    private static extern void _SetConnectCallback(IntPtr instance, ConnectCallback callback);
#endif

    [Header("Configuration")]
    [SerializeField] private string baseUrl = "wss://ec2-13-229-220-104.ap-southeast-1.compute.amazonaws.com:8443/ws";
    [SerializeField] private string certificateFileName = "devices-client2.p12";
    [SerializeField] private string certificatePassword = "pass";

    private IntPtr wsInstance;
    private bool isConnected = false;

    // Store current connection credentials for debugging
    private string currentUserId = "";
    private string currentSessionId = "";

    public bool IsConnected => isConnected;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        DebugViewController.AddDebugMessage("WS_Client initialized. Ready to connect.");
    }

    public void ConnectWebSocket()
    {
#if UNITY_IOS && !UNITY_EDITOR
        if (isConnected)
        {
            DebugViewController.AddDebugMessage("Already connected!");
            return;
        }

        // === RETRIEVE USERNAME FROM SETTINGS ===
        if (SettingsMenuController.Instance == null)
        {
            DebugViewController.AddDebugMessage("ERROR: SettingsMenuController.Instance is null");
            DebugViewController.UpdateConnectionButtons(false);
            return;
        }

        string username = SettingsMenuController.Instance.GetUsername();
        
        // === VALIDATE USERNAME ===
        if (string.IsNullOrWhiteSpace(username))
        {
            DebugViewController.AddDebugMessage("=== CONNECTION FAILED ===");
            DebugViewController.AddDebugMessage("ERROR: Username is required");
            DebugViewController.AddDebugMessage("Please enter a username in Settings before connecting");
            DebugViewController.UpdateConnectionButtons(false);
            return;
        }

        // Trim whitespace
        username = username.Trim();

        try
        {
            DebugViewController.AddDebugMessage("=== Connection Attempt Started ===");
            DebugViewController.AddDebugMessage($"Username: {username}");
            
            // === GENERATE USERID AND SESSIONID ===
            long epochTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            currentUserId = $"{username}-{epochTime}";
            currentSessionId = username;
            
            DebugViewController.AddDebugMessage($"Generated userId: {currentUserId}");
            DebugViewController.AddDebugMessage($"Generated sessionId: {currentSessionId}");
            
            // === BUILD DYNAMIC URL ===
            string encodedUserId = UnityWebRequest.EscapeURL(currentUserId);
            string encodedSessionId = UnityWebRequest.EscapeURL(currentSessionId);
            string fullUrl = $"{baseUrl}?userId={encodedUserId}&sessionId={encodedSessionId}";
            
            DebugViewController.AddDebugMessage($"Connection URL: {fullUrl}");
            
            // === CERTIFICATE SETUP ===
            string certPath = System.IO.Path.Combine(Application.streamingAssetsPath, certificateFileName);
            
            DebugViewController.AddDebugMessage($"Certificate path: {certPath}");
            DebugViewController.AddDebugMessage($"Looking for file: {certificateFileName}");
            
            if (!System.IO.File.Exists(certPath))
            {
                DebugViewController.AddDebugMessage($"ERROR: Certificate file not found at: {certPath}");
                DebugViewController.UpdateConnectionButtons(false);
                return;
            }
            
            DebugViewController.AddDebugMessage($"Certificate found! Creating WebSocket...");
            
            // === CREATE WEBSOCKET WITH DYNAMIC URL ===
            wsInstance = _CreateWebSocket(fullUrl, certPath, certificatePassword);
            
            if (wsInstance != IntPtr.Zero)
            {
                _SetMessageCallback(wsInstance, OnMessageReceived);
                _SetErrorCallback(wsInstance, OnError);
                _SetConnectCallback(wsInstance, OnConnected);
                
                DebugViewController.AddDebugMessage("Connecting...");
                _ConnectWebSocket(wsInstance);
            }
            else
            {
                DebugViewController.AddDebugMessage("ERROR: Failed to create WebSocket instance");
                DebugViewController.UpdateConnectionButtons(false);
            }
        }
        catch (Exception ex)
        {
            DebugViewController.AddDebugMessage($"Exception: {ex.Message}");
            DebugViewController.UpdateConnectionButtons(false);
        }
#else
        DebugViewController.AddDebugMessage("Native WebSocket only works on iOS device");
        DebugViewController.UpdateConnectionButtons(false);
#endif
    }

    public void ManualDisconnect()
    {
#if UNITY_IOS && !UNITY_EDITOR
        if (wsInstance != IntPtr.Zero)
        {
            _CloseWebSocket(wsInstance);
            wsInstance = IntPtr.Zero;
            isConnected = false;
            
            DebugViewController.AddDebugMessage("WebSocket disconnected");
            DebugViewController.AddDebugMessage($"Disconnected from userId: {currentUserId}");
            
            // Clear credentials
            currentUserId = "";
            currentSessionId = "";
            
            DebugViewController.UpdateConnectionButtons(false);
        }
#else
        DebugViewController.AddDebugMessage("Disconnect only available on iOS device");
#endif
    }

    public void SendPingMessage()
    {
        SendMessage("{\"type\":\"ping\",\"timestamp\":" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "}");
    }

    public void SendMessage(string message)
    {
#if UNITY_IOS && !UNITY_EDITOR
        if (wsInstance != IntPtr.Zero && isConnected)
        {
            _SendMessage(wsInstance, message);
            DebugViewController.AddDebugMessage($"SENT: {message}");
        }
        else
        {
            DebugViewController.AddDebugMessage("Cannot send - not connected");
        }
#else
        DebugViewController.AddDebugMessage($"[Editor] Would send: {message}");
#endif
    }

#if UNITY_IOS && !UNITY_EDITOR
    [MonoPInvokeCallback(typeof(MessageCallback))]
    private static void OnMessageReceived(string message)
    {
        if (Instance != null)
        {
            UnityMainThreadDispatcher.Enqueue(() => {
                DebugViewController.AddDebugMessage($"RECEIVED: {message}");
            
                if (CommandHandler.Instance != null)
                {
                    CommandHandler.Instance.HandleWebSocketMessage(message);
                }
            });
        }
    }

    [MonoPInvokeCallback(typeof(ErrorCallback))]
    private static void OnError(string error)
    {
        if (Instance != null)
        {
            UnityMainThreadDispatcher.Enqueue(() => {
                DebugViewController.AddDebugMessage($"ERROR: {error}");
                Instance.isConnected = false;
                DebugViewController.UpdateConnectionButtons(false);
            });
        }
    }

    [MonoPInvokeCallback(typeof(ConnectCallback))]
    private static void OnConnected()
    {
        if (Instance != null)
        {
            UnityMainThreadDispatcher.Enqueue(() => {
                DebugViewController.AddDebugMessage("===== WebSocket connected successfully! =====");
                DebugViewController.AddDebugMessage($"Connected as: {Instance.currentUserId}");
                Instance.isConnected = true;
                DebugViewController.UpdateConnectionButtons(true);
            });
        }
    }
#endif

    private void OnDestroy()
    {
#if UNITY_IOS && !UNITY_EDITOR
        if (wsInstance != IntPtr.Zero)
        {
            _CloseWebSocket(wsInstance);
        }
#endif

        if (Instance == this)
        {
            Instance = null;
        }
    }

    // Public getter for debugging
    public string GetCurrentUserId() => currentUserId;
    public string GetCurrentSessionId() => currentSessionId;
}