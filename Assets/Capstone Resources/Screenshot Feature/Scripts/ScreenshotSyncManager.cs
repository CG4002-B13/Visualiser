using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;

public class ScreenshotSyncManager : MonoBehaviour
{
    public static ScreenshotSyncManager Instance { get; private set; }


    [Header("Sync Settings")]
    [SerializeField] private float downloadTimeout = 30f;
    [SerializeField] private float uploadTimeout = 30f;


    // Sync state tracking
    private bool isSyncInProgress = false;
    private Action<bool> currentSyncCallback = null;


    public bool IsSyncInProgress => isSyncInProgress;


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }


    private void OnDestroy()
    {
        StopAllCoroutines();
        s_pendingSyncResponse = null;
        currentSyncCallback = null;
    }


    /// <summary>
    /// Check sync status - LIGHTWEIGHT
    /// Load file list only when actually syncing, not on tab open
    /// </summary>
    public void CheckSyncStatus(Action<int, int> onComplete)
    {
        if (!WS_Client.Instance.IsConnected)
        {
            DebugViewController.AddDebugMessage("Cannot check sync - not connected");
            onComplete(0, 0);
            return;
        }


        StartCoroutine(SendSyncRequest(
            onResponse: (response) =>
            {
                int uploadsNeeded = 0;
                int downloadsNeeded = 0;


                try
                {
                    if (response != null && response.ContainsKey("data"))
                    {
                        JObject data = response["data"] as JObject;


                        if (data != null)
                        {
                            if (data.ContainsKey("GET"))
                            {
                                JArray getUrls = data["GET"] as JArray;
                                if (getUrls != null)
                                {
                                    downloadsNeeded = getUrls.Count;
                                }
                            }


                            if (data.ContainsKey("PUT"))
                            {
                                JArray putUrls = data["PUT"] as JArray;
                                if (putUrls != null)
                                {
                                    uploadsNeeded = putUrls.Count;
                                }
                            }
                        }
                    }


                    DebugViewController.AddDebugMessage($"Uploads: {uploadsNeeded}, Downloads: {downloadsNeeded}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error parsing sync response: {ex.Message}");
                }
                finally
                {
                    // Clear response to free memory
                    if (response != null)
                    {
                        response = null;
                    }
                    System.GC.Collect();
                }


                onComplete(uploadsNeeded, downloadsNeeded);
            }
        ));
    }


    /// <summary>
    /// Perform actual sync - download and upload files
    /// </summary>
    public void PerformSync(Action<bool> onComplete)
    {
        if (isSyncInProgress)
        {
            DebugViewController.AddDebugMessage("Sync already in progress");
            onComplete(false);
            return;
        }


        if (!WS_Client.Instance.IsConnected)
        {
            DebugViewController.AddDebugMessage("Cannot sync - not connected");
            onComplete(false);
            return;
        }


        isSyncInProgress = true;
        currentSyncCallback = onComplete;


        DebugViewController.AddDebugMessage("=== Sync Started ===");


        StartCoroutine(SendSyncRequest(
            onResponse: (response) =>
            {
                StartCoroutine(ProcessSyncResponse(response));
            }
        ));
    }


    /// <summary>
    /// Send sync request to server - LIGHTWEIGHT
    /// </summary>
    private IEnumerator SendSyncRequest(Action<JObject> onResponse)
    {
        string username = SettingsMenuController.Instance != null
            ? SettingsMenuController.Instance.GetUsername()
            : "default";


        if (string.IsNullOrWhiteSpace(username))
        {
            username = "default";
        }


        username = username.Trim().ToLower();


        // ===== LIGHTWEIGHT: Don't load all files into memory =====
        // Just get count and folder path
        string userFolder = Path.Combine(Application.persistentDataPath, "screenshots", username);
        string[] localFiles = new string[0];


        if (Directory.Exists(userFolder))
        {
            localFiles = Directory.GetFiles(userFolder, "*.jpg", SearchOption.TopDirectoryOnly);
        }


        JArray fileList = new JArray();


        // ===== CRITICAL: Only add files if count is reasonable =====
        if (localFiles.Length > 100)
        {
            DebugViewController.AddDebugMessage($"⚠️ Too many files ({localFiles.Length}) - only syncing first 100");
            // Limit to 100 files to prevent massive JSON
            for (int i = 0; i < 100; i++)
            {
                string filename = Path.GetFileName(localFiles[i]);
                fileList.Add($"{username}/{filename}");
            }
        }
        else
        {
            // Normal case - add all files
            foreach (string filePath in localFiles)
            {
                string filename = Path.GetFileName(filePath);
                fileList.Add($"{username}/{filename}");
            }
        }


        DebugViewController.AddDebugMessage($"Local files to sync: {fileList.Count}");


        JObject request = new JObject
        {
            ["EventType"] = "S3_SYNC_REQUEST",
            ["UserId"] = WS_Client.Instance.GetCurrentUserId(),
            ["SessionId"] = WS_Client.Instance.GetCurrentSessionId(),
            ["Timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ["Data"] = fileList
        };


        DebugViewController.AddDebugMessage("=== S3 Sync Request ===");


        string jsonRequest = request.ToString(Newtonsoft.Json.Formatting.None);
        WS_Client.Instance.SendMessage(jsonRequest);


        s_pendingSyncResponse = onResponse;


        yield return null;
    }


    private static Action<JObject> s_pendingSyncResponse = null;


    public static void OnSyncResponseReceived(JObject response)
    {
        if (s_pendingSyncResponse != null)
        {
            s_pendingSyncResponse.Invoke(response);
            s_pendingSyncResponse = null;
        }
    }


    /// <summary>
    /// Process sync response - LIGHTWEIGHT
    /// </summary>
    private IEnumerator ProcessSyncResponse(JObject response)
    {
        bool isValidResponse = response != null && response.ContainsKey("data");


        if (!isValidResponse)
        {
            DebugViewController.AddDebugMessage("ERROR: Invalid sync response");
            isSyncInProgress = false;
            currentSyncCallback?.Invoke(false);
            yield break;
        }


        JObject data = response["data"] as JObject;
        if (data == null || data.Count == 0)
        {
            DebugViewController.AddDebugMessage("Already in sync");
            isSyncInProgress = false;
            currentSyncCallback?.Invoke(true);
            yield break;
        }


        string username = SettingsMenuController.Instance != null
            ? SettingsMenuController.Instance.GetUsername()
            : "default";
        username = username.Trim().ToLower();


        bool allSuccess = true;


        // Process downloads (GET) - ONE AT A TIME to avoid memory spike
        if (data.ContainsKey("GET"))
        {
            DebugViewController.AddDebugMessage("=== Processing Downloads ===");
            JArray getUrls = data["GET"] as JArray;


            if (getUrls != null && getUrls.Count > 0)
            {
                DebugViewController.AddDebugMessage($"Downloading {getUrls.Count} file(s)");


                for (int i = 0; i < getUrls.Count; i++)
                {
                    // ===== CRITICAL: Download one file at a time =====
                    string presignedUrl = getUrls[i].ToString();
                    yield return StartCoroutine(DownloadFileFromS3(presignedUrl, username,
                        (success) =>
                        {
                            if (!success) allSuccess = false;
                        }
                    ));


                    // Memory cleanup between downloads
                    System.GC.Collect();
                    yield return null;
                }
            }
        }


        // Process uploads (PUT) - ONE AT A TIME to avoid memory spike
        if (data.ContainsKey("PUT"))
        {
            DebugViewController.AddDebugMessage("=== Processing Uploads ===");
            JArray putUrls = data["PUT"] as JArray;
            JArray putNames = data.ContainsKey("PUTNames") ? data["PUTNames"] as JArray : null;


            if (putUrls != null && putNames != null && putUrls.Count > 0)
            {
                DebugViewController.AddDebugMessage($"Uploading {putUrls.Count} file(s)");


                for (int i = 0; i < putUrls.Count; i++)
                {
                    // ===== CRITICAL: Upload one file at a time =====
                    string presignedUrl = putUrls[i].ToString();
                    string fullPath = putNames[i].ToString();


                    yield return StartCoroutine(UploadFileToS3(presignedUrl, fullPath, username,
                        (success) =>
                        {
                            if (!success) allSuccess = false;
                        }
                    ));


                    // Memory cleanup between uploads
                    System.GC.Collect();
                    yield return null;
                }
            }
        }


        DebugViewController.AddDebugMessage("=== Sync Complete ===");


        isSyncInProgress = false;
        currentSyncCallback?.Invoke(allSuccess);
    }


    /// <summary>
    /// Download file from S3 - LIGHTWEIGHT
    /// </summary>
    private IEnumerator DownloadFileFromS3(string presignedUrl, string username, Action<bool> onComplete)
    {
        string filename = ExtractFilenameFromUrl(presignedUrl);


        if (string.IsNullOrEmpty(filename))
        {
            DebugViewController.AddDebugMessage($"Failed to extract filename");
            onComplete(false);
            yield break;
        }


        DebugViewController.AddDebugMessage($"Downloading: {filename}");


        // ===== LIGHTWEIGHT: Download directly to disk, not memory =====
        string tempPath = Path.Combine(Path.GetTempPath(), filename);  // ← FIXED: Use Path.GetTempPath()
        UnityWebRequest request = new UnityWebRequest(presignedUrl);
        request.downloadHandler = new DownloadHandlerFile(tempPath);
        request.timeout = (int)downloadTimeout;


        yield return request.SendWebRequest();


        bool success = false;


        try
        {
            if (request.result == UnityWebRequest.Result.Success)
            {
                // Move from temp to final location
                string userFolder = Path.Combine(Application.persistentDataPath, "screenshots", username);
                if (!Directory.Exists(userFolder))
                {
                    Directory.CreateDirectory(userFolder);
                }


                string finalPath = Path.Combine(userFolder, filename);


                if (File.Exists(finalPath))
                {
                    File.Delete(finalPath);
                }


                File.Move(tempPath, finalPath);


                DebugViewController.AddDebugMessage($"✓ Downloaded: {filename}");
                success = true;
            }
            else
            {
                DebugViewController.AddDebugMessage($"Download failed: {request.error}");
            }
        }
        finally
        {
            request.Dispose();
            request = null;
        }


        onComplete(success);
    }


    /// <summary>
    /// Upload file to S3 - LIGHTWEIGHT
    /// </summary>
    private IEnumerator UploadFileToS3(string presignedUrl, string fullPath, string username, Action<bool> onComplete)
    {
        string filename = Path.GetFileName(fullPath);
        DebugViewController.AddDebugMessage($"Uploading: {filename}");


        string userFolder = Path.Combine(Application.persistentDataPath, "screenshots", username);
        string localFilePath = Path.Combine(userFolder, filename);


        if (!File.Exists(localFilePath))
        {
            DebugViewController.AddDebugMessage($"File not found: {filename}");
            onComplete(false);
            yield break;
        }


        byte[] fileData = null;
        try
        {
            fileData = File.ReadAllBytes(localFilePath);
        }
        catch (Exception e)
        {
            DebugViewController.AddDebugMessage($"ERROR reading file: {e.Message}");
            onComplete(false);
            yield break;
        }


        UnityWebRequest request = UnityWebRequest.Put(presignedUrl, fileData);
        request.timeout = (int)uploadTimeout;
        request.SetRequestHeader("Content-Type", "image/jpeg");


        yield return request.SendWebRequest();


        bool success = false;


        try
        {
            if (request.result == UnityWebRequest.Result.Success)
            {
                DebugViewController.AddDebugMessage($"✓ Uploaded: {filename}");
                success = true;
            }
            else
            {
                DebugViewController.AddDebugMessage($"Upload failed: {request.error}");
            }
        }
        finally
        {
            request.Dispose();
            request = null;
            fileData = null;
        }


        onComplete(success);
    }


    /// <summary>
    /// Extract filename from presigned URL
    /// </summary>
    private string ExtractFilenameFromUrl(string url)
    {
        try
        {
            int queryIndex = url.IndexOf('?');
            if (queryIndex > 0)
            {
                url = url.Substring(0, queryIndex);
            }


            string[] parts = url.Split('/');
            if (parts.Length > 0)
            {
                return parts[parts.Length - 1];
            }


            return null;
        }
        catch
        {
            return null;
        }
    }
}