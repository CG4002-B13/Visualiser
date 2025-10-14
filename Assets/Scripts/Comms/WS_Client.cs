using System;
using System.Runtime.InteropServices;
using UnityEngine;
using AOT;

public class WS_Client : MonoBehaviour
{
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
    [SerializeField] private string serverUrl = "wss://ec2-13-229-220-104.ap-southeast-1.compute.amazonaws.com:8443/ws?userId=parth&sessionId=1";
    [SerializeField] private string certificateFileName = "devices-client2.p12";
    [SerializeField] private string certificatePassword = "pass";

    private IntPtr wsInstance;
    private bool isConnected = false;
    private static WS_Client instance;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        // Don't auto-connect - let debug panel control this
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

        try
        {
            // Direct access - no copying needed on iOS
            string certPath = System.IO.Path.Combine(Application.streamingAssetsPath, certificateFileName);
            
            DebugViewController.AddDebugMessage($"Certificate path: {certPath}");
            DebugViewController.AddDebugMessage($"Looking for file: {certificateFileName}");
            
            // Verify the file exists
            if (!System.IO.File.Exists(certPath))
            {
                DebugViewController.AddDebugMessage($"ERROR: Certificate file not found at: {certPath}");
                DebugViewController.UpdateConnectionButtons(false);
                return;
            }
            
            DebugViewController.AddDebugMessage($"Certificate found! Creating WebSocket...");
            wsInstance = _CreateWebSocket(serverUrl, certPath, certificatePassword);
            
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
        if (instance != null)
        {
            UnityMainThreadDispatcher.Enqueue(() => {
                DebugViewController.AddDebugMessage($"RECEIVED: {message}");
            });
        }
    }

    [MonoPInvokeCallback(typeof(ErrorCallback))]
    private static void OnError(string error)
    {
        if (instance != null)
        {
            UnityMainThreadDispatcher.Enqueue(() => {
                DebugViewController.AddDebugMessage($"ERROR: {error}");
                instance.isConnected = false;
                DebugViewController.UpdateConnectionButtons(false);
            });
        }
    }

    [MonoPInvokeCallback(typeof(ConnectCallback))]
    private static void OnConnected()
    {
        if (instance != null)
        {
            UnityMainThreadDispatcher.Enqueue(() => {
                DebugViewController.AddDebugMessage("✓ WebSocket connected successfully!");
                instance.isConnected = true;
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
        instance = null;
    }
}
