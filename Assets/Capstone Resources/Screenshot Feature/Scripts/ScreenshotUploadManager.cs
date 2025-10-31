using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;

public class ScreenshotUploadManager : MonoBehaviour
{
    public static ScreenshotUploadManager Instance { get; private set; }

    [Header("Upload Settings")]
    [SerializeField] private int maxRetryAttempts = 4;
    [SerializeField] private float uploadTimeout = 30f;

    // Upload queue and status tracking
    private bool isUploading = false;
    private string pendingPresignedUrl = null;
    private string pendingFilePath = null;
    private long pendingTimestamp = 0;
    private int currentAttempt = 0;

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

    /// <summary>
    /// Initiate upload process for a screenshot
    /// Called by ScreenshotManagerIOS after capture
    /// </summary>
    public void UploadScreenshot(string filePath, long epochTimestamp)
    {
        if (isUploading)
        {
            DebugViewController.AddDebugMessage("Upload already in progress, skipping");
            return;
        }

        // Check internet connectivity
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            DebugViewController.AddDebugMessage("No internet connection - upload skipped");
            DebugViewController.AddDebugMessage("Screenshot saved locally, sync later to upload");
            return;
        }

        // Store pending upload info
        pendingFilePath = filePath;
        pendingTimestamp = epochTimestamp;
        currentAttempt = 0;

        // Request presigned URL from server via WebSocket
        RequestPresignedURL(epochTimestamp);
    }

    /// <summary>
    /// Send S3_UPLOAD_REQUEST via WebSocket to get presigned URL
    /// </summary>
    private void RequestPresignedURL(long epochTimestamp)
    {
        if (WS_Client.Instance == null)
        {
            DebugViewController.AddDebugMessage("ERROR: WS_Client.Instance is null");
            return;
        }

        if (!WS_Client.Instance.IsConnected)
        {
            DebugViewController.AddDebugMessage("WebSocket not connected - upload skipped");
            DebugViewController.AddDebugMessage("Screenshot saved locally, sync later to upload");
            return;
        }

        // Get username from Settings
        string username = SettingsMenuController.Instance != null
            ? SettingsMenuController.Instance.GetUsername()
            : "unknown";

        if (string.IsNullOrWhiteSpace(username))
        {
            DebugViewController.AddDebugMessage("ERROR: Username not set, cannot upload");
            return;
        }

        username = username.Trim();

        // Build request message
        JObject request = new JObject
        {
            ["EventType"] = "S3_UPLOAD_REQUEST",
            ["UserId"] = $"{username}-{epochTimestamp}",
            ["SessionId"] = username,
            ["Timestamp"] = epochTimestamp,
            ["Data"] = null
        };

        string jsonRequest = request.ToString(Newtonsoft.Json.Formatting.None);

        DebugViewController.AddDebugMessage("=== S3 Upload Request ===");
        DebugViewController.AddDebugMessage($"Requesting presigned URL for: {username}/{epochTimestamp}.jpg");

        WS_Client.Instance.SendMessage(jsonRequest);
    }

    /// <summary>
    /// Handle S3_UPLOAD_RESPONSE from server
    /// Called by CommandHandler when response is received
    /// </summary>
    public void OnUploadResponseReceived(string presignedUrl)
    {
        if (string.IsNullOrEmpty(presignedUrl))
        {
            DebugViewController.AddDebugMessage("ERROR: Received empty presigned URL");
            return;
        }

        pendingPresignedUrl = presignedUrl;
        DebugViewController.AddDebugMessage("✓ Presigned URL received");

        // Start actual upload to S3
        StartCoroutine(UploadToS3(presignedUrl, pendingFilePath));
    }

    /// <summary>
    /// Handle S3_ERROR from server
    /// Called by CommandHandler when error is received
    /// </summary>
    public void OnUploadErrorReceived(string errorMessage)
    {
        DebugViewController.AddDebugMessage($"=== S3 Upload Error ===");
        DebugViewController.AddDebugMessage($"Server error: {errorMessage}");
        DebugViewController.AddDebugMessage("Screenshot saved locally, sync later to upload");

        // Clear pending state
        isUploading = false;
        pendingPresignedUrl = null;
        pendingFilePath = null;
    }

    /// <summary>
    /// Upload JPG file to S3 using presigned URL
    /// </summary>
    private IEnumerator UploadToS3(string presignedUrl, string filePath)
    {
        isUploading = true;
        currentAttempt++;

        DebugViewController.AddDebugMessage($"=== S3 Upload Attempt {currentAttempt}/{maxRetryAttempts} ===");

        // Check if file exists
        if (!File.Exists(filePath))
        {
            DebugViewController.AddDebugMessage($"ERROR: File not found: {filePath}");
            isUploading = false;
            yield break;
        }

        // Read file as byte array
        byte[] fileData = null;
        try
        {
            fileData = File.ReadAllBytes(filePath);
            DebugViewController.AddDebugMessage($"File loaded: {fileData.Length} bytes ({fileData.Length / 1024}KB)");
        }
        catch (Exception e)
        {
            DebugViewController.AddDebugMessage($"ERROR reading file: {e.Message}");
            isUploading = false;
            yield break;
        }

        // Create PUT request
        UnityWebRequest request = UnityWebRequest.Put(presignedUrl, fileData);
        request.timeout = (int)uploadTimeout;
        request.SetRequestHeader("Content-Type", "image/jpeg");

        // Send request
        DebugViewController.AddDebugMessage($"Uploading to S3... (timeout: {uploadTimeout}s)");
        yield return request.SendWebRequest();

        // Handle response
        if (request.result == UnityWebRequest.Result.Success)
        {
            // Upload successful!
            DebugViewController.AddDebugMessage("=== S3 Upload SUCCESS ===");
            DebugViewController.AddDebugMessage($"Screenshot uploaded: {Path.GetFileName(filePath)}");
            DebugViewController.AddDebugMessage($"Response code: {request.responseCode}");

            // Clear pending state
            isUploading = false;
            pendingPresignedUrl = null;
            pendingFilePath = null;
            currentAttempt = 0;
        }
        else
        {
            // Upload failed
            string errorMsg = request.error;
            long responseCode = request.responseCode;

            DebugViewController.AddDebugMessage($"=== S3 Upload FAILED ===");
            DebugViewController.AddDebugMessage($"Attempt: {currentAttempt}/{maxRetryAttempts}");
            DebugViewController.AddDebugMessage($"Error: {errorMsg}");
            DebugViewController.AddDebugMessage($"Response code: {responseCode}");
            DebugViewController.AddDebugMessage($"File: {Path.GetFileName(filePath)}");
            DebugViewController.AddDebugMessage($"Size: {fileData.Length / 1024}KB");

            // Retry logic
            if (currentAttempt < maxRetryAttempts)
            {
                DebugViewController.AddDebugMessage($"Retrying upload... ({currentAttempt + 1}/{maxRetryAttempts})");
                yield return new WaitForSeconds(1f); // Wait 1 second before retry
                StartCoroutine(UploadToS3(presignedUrl, filePath)); // Retry with same URL
            }
            else
            {
                DebugViewController.AddDebugMessage("=== Max retry attempts reached ===");
                DebugViewController.AddDebugMessage("Screenshot saved locally");
                DebugViewController.AddDebugMessage("Use Sync to retry upload later");

                // Clear pending state
                isUploading = false;
                pendingPresignedUrl = null;
                pendingFilePath = null;
                currentAttempt = 0;
            }
        }

        request.Dispose();
    }
}
