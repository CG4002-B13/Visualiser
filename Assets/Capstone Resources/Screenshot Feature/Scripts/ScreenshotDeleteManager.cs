using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;

public class ScreenshotDeleteManager : MonoBehaviour
{
    public static ScreenshotDeleteManager Instance { get; private set; }

    [Header("Delete Settings")]
    [SerializeField] private int maxRetryAttempts = 4;
    [SerializeField] private float deleteTimeout = 30f;

    // Delete state tracking
    private bool isDeletePending = false;
    private string pendingFilePath = null;
    private string pendingPresignedUrl = null;
    private string pendingS3Key = null;
    private int currentAttempt = 0;

    public bool IsDeletePending => isDeletePending;

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
    /// Initiate delete process for a screenshot
    /// Called by GalleryViewController when user taps delete
    /// </summary>
    public void DeleteScreenshot(string filePath)
    {
        if (isDeletePending)
        {
            DebugViewController.AddDebugMessage("Delete already in progress, please wait");
            return;
        }

        if (!File.Exists(filePath))
        {
            DebugViewController.AddDebugMessage("ERROR: File not found locally");
            return;
        }

        // Check internet connectivity
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            DebugViewController.AddDebugMessage("=== Cannot Delete Offline ===");
            DebugViewController.AddDebugMessage("Internet connection required to delete from cloud");
            DebugViewController.AddDebugMessage("Connect to internet and try again");
            return;
        }

        // Check WebSocket connection
        if (WS_Client.Instance == null || !WS_Client.Instance.IsConnected)
        {
            DebugViewController.AddDebugMessage("=== Cannot Delete ===");
            DebugViewController.AddDebugMessage("WebSocket not connected");
            DebugViewController.AddDebugMessage("Connect to server and try again");
            return;
        }

        // Extract filename and build S3 key
        string filename = Path.GetFileName(filePath);
        string username = SettingsMenuController.Instance != null
            ? SettingsMenuController.Instance.GetUsername()
            : "default";

        if (string.IsNullOrWhiteSpace(username))
        {
            username = "default";
        }

        username = username.Trim().ToLower();

        // Build S3 key: username/filename.jpg
        string s3Key = $"{username}/{filename}";

        // Store pending delete info
        pendingFilePath = filePath;
        pendingS3Key = s3Key;
        currentAttempt = 0;
        isDeletePending = true;

        // Request presigned DELETE URL from server
        RequestDeletePresignedURL(s3Key);
    }

    /// <summary>
    /// Send S3_DELETE_REQUEST via WebSocket to get presigned DELETE URL
    /// UserId: Connection username-timestamp
    /// Timestamp: Current unix epoch time
    /// </summary>
    private void RequestDeletePresignedURL(string s3Key)
    {
        if (WS_Client.Instance == null)
        {
            DebugViewController.AddDebugMessage("ERROR: WS_Client.Instance is null");
            isDeletePending = false;
            return;
        }

        // Get connection UserId from WS_Client (username-connectionTimestamp)
        string userId = WS_Client.Instance.GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
        {
            DebugViewController.AddDebugMessage("ERROR: UserId not available from WS_Client");
            isDeletePending = false;
            return;
        }

        // Get SessionId (just username)
        string sessionId = WS_Client.Instance.GetCurrentSessionId();
        if (string.IsNullOrEmpty(sessionId))
        {
            DebugViewController.AddDebugMessage("ERROR: SessionId not available from WS_Client");
            isDeletePending = false;
            return;
        }

        // Generate current timestamp for this request
        long currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Build delete request message
        JObject request = new JObject
        {
            ["EventType"] = "S3_DELETE_REQUEST",
            ["UserId"] = userId,              // From connection (e.g., "parth-1761884142983")
            ["SessionId"] = sessionId,        // From connection (e.g., "parth")
            ["Timestamp"] = currentTimestamp, // Current time
            ["Data"] = s3Key                  // File to delete (e.g., "parth/1761928201282.jpg")
        };

        string jsonRequest = request.ToString(Newtonsoft.Json.Formatting.None);

        DebugViewController.AddDebugMessage("=== S3 Delete Request ===");
        DebugViewController.AddDebugMessage($"Requesting presigned DELETE URL for: {s3Key}");
        DebugViewController.AddDebugMessage($"UserId: {userId}");
        DebugViewController.AddDebugMessage($"Timestamp: {currentTimestamp}");

        WS_Client.Instance.SendMessage(jsonRequest);
    }

    /// <summary>
    /// Handle S3_DELETE_RESPONSE from server
    /// Called by CommandHandler when response is received
    /// </summary>
    public void OnDeleteResponseReceived(string presignedUrl)
    {
        if (string.IsNullOrEmpty(presignedUrl))
        {
            DebugViewController.AddDebugMessage("ERROR: Received empty presigned URL");
            isDeletePending = false;

            // Notify gallery that delete failed
            if (GalleryViewController.Instance != null)
            {
                GalleryViewController.Instance.OnDeleteComplete(false, pendingFilePath);
            }
            return;
        }

        pendingPresignedUrl = presignedUrl;

        // Debug log URL details
        DebugViewController.AddDebugMessage("✓ Presigned DELETE URL received");
        DebugViewController.AddDebugMessage($"URL length: {presignedUrl.Length}");
        DebugViewController.AddDebugMessage($"URL preview: {presignedUrl.Substring(0, Math.Min(80, presignedUrl.Length))}...");

        // Start actual delete from S3
        StartCoroutine(DeleteFromS3(presignedUrl, pendingFilePath));
    }

    /// <summary>
    /// Handle S3_ERROR from server
    /// Called by CommandHandler when error is received
    /// </summary>
    public void OnDeleteErrorReceived(string errorMessage)
    {
        DebugViewController.AddDebugMessage($"=== S3 Delete Error ===");
        DebugViewController.AddDebugMessage($"Server error: {errorMessage}");
        DebugViewController.AddDebugMessage("Screenshot NOT deleted");

        // Clear pending state
        isDeletePending = false;
        pendingPresignedUrl = null;
        pendingFilePath = null;
        pendingS3Key = null;

        // Notify gallery that delete failed
        if (GalleryViewController.Instance != null)
        {
            GalleryViewController.Instance.OnDeleteComplete(false, pendingFilePath);
        }
    }

    /// <summary>
    /// Delete file from S3 using presigned URL, then delete local file
    /// Uses proper HTTP DELETE method compatible with S3 presigned URLs
    /// </summary>
    private IEnumerator DeleteFromS3(string presignedUrl, string localFilePath)
    {
        currentAttempt++;

        DebugViewController.AddDebugMessage($"=== S3 Delete Attempt {currentAttempt}/{maxRetryAttempts} ===");
        DebugViewController.AddDebugMessage($"File: {Path.GetFileName(localFilePath)}");

        // ===== FIXED: Use proper HTTP DELETE method for S3 =====
        // Unity's UnityWebRequest.Delete() doesn't work properly with presigned URLs
        // We must create a custom DELETE request instead
        UnityWebRequest request = new UnityWebRequest(presignedUrl, "DELETE");
        request.downloadHandler = new DownloadHandlerBuffer();
        request.uploadHandler = new UploadHandlerRaw(new byte[0]); // Empty body
        request.timeout = (int)deleteTimeout;

        // Send request
        DebugViewController.AddDebugMessage($"Sending HTTP DELETE to S3... (timeout: {deleteTimeout}s)");
        yield return request.SendWebRequest();

        // ===== ENHANCED: Detailed response logging =====
        DebugViewController.AddDebugMessage($"Request result: {request.result}");
        DebugViewController.AddDebugMessage($"Response code: {request.responseCode}");

        // Log response body if available (useful for debugging S3 errors)
        if (!string.IsNullOrEmpty(request.downloadHandler.text))
        {
            DebugViewController.AddDebugMessage($"Response body: {request.downloadHandler.text}");
        }

        // ===== FIXED: Strict response code checking =====
        // S3 DELETE returns 204 No Content on success (sometimes 200 OK)
        // Only these codes indicate successful deletion
        bool isSuccessfulDelete = request.result == UnityWebRequest.Result.Success &&
                                   (request.responseCode == 204 || request.responseCode == 200);

        if (isSuccessfulDelete)
        {
            // S3 delete successful!
            DebugViewController.AddDebugMessage("=== S3 Delete SUCCESS ===");
            DebugViewController.AddDebugMessage($"Response code: {request.responseCode}");
            DebugViewController.AddDebugMessage($"File deleted from S3: {pendingS3Key}");

            // Now delete local file
            bool localDeleteSuccess = DeleteLocalFile(localFilePath);

            if (localDeleteSuccess)
            {
                DebugViewController.AddDebugMessage("✓ Local file deleted");
                DebugViewController.AddDebugMessage("Screenshot deleted from both cloud and device");

                // Clear pending state
                isDeletePending = false;
                pendingPresignedUrl = null;
                pendingFilePath = null;
                pendingS3Key = null;
                currentAttempt = 0;

                // Notify gallery that delete succeeded
                if (GalleryViewController.Instance != null)
                {
                    GalleryViewController.Instance.OnDeleteComplete(true, localFilePath);
                }
            }
            else
            {
                DebugViewController.AddDebugMessage("ERROR: Failed to delete local file");
                DebugViewController.AddDebugMessage("Cloud copy deleted, but local copy remains");

                // Clear pending state
                isDeletePending = false;

                // Notify gallery (partial success - S3 deleted but local failed)
                if (GalleryViewController.Instance != null)
                {
                    GalleryViewController.Instance.OnDeleteComplete(false, localFilePath);
                }
            }
        }
        else
        {
            // S3 delete failed - wrong response code or network error
            string errorMsg = request.error;
            long responseCode = request.responseCode;

            DebugViewController.AddDebugMessage($"=== S3 Delete FAILED ===");
            DebugViewController.AddDebugMessage($"Attempt: {currentAttempt}/{maxRetryAttempts}");

            if (!string.IsNullOrEmpty(errorMsg))
            {
                DebugViewController.AddDebugMessage($"Error: {errorMsg}");
            }

            DebugViewController.AddDebugMessage($"Response code: {responseCode}");

            // Log specific S3 error codes
            if (responseCode == 403)
            {
                DebugViewController.AddDebugMessage("403 Forbidden - Signature may be invalid or URL expired");
            }
            else if (responseCode == 404)
            {
                DebugViewController.AddDebugMessage("404 Not Found - File may have been already deleted");
            }
            else if (responseCode == 400)
            {
                DebugViewController.AddDebugMessage("400 Bad Request - URL format issue");
            }

            DebugViewController.AddDebugMessage($"File: {Path.GetFileName(localFilePath)}");

            // Retry logic
            if (currentAttempt < maxRetryAttempts)
            {
                DebugViewController.AddDebugMessage($"Retrying delete... ({currentAttempt + 1}/{maxRetryAttempts})");
                yield return new WaitForSeconds(1f); // Wait 1 second before retry
                StartCoroutine(DeleteFromS3(presignedUrl, localFilePath)); // Retry with same URL
            }
            else
            {
                DebugViewController.AddDebugMessage("=== Max retry attempts reached ===");
                DebugViewController.AddDebugMessage("Screenshot NOT deleted from cloud");
                DebugViewController.AddDebugMessage("Local copy kept (still in gallery)");
                DebugViewController.AddDebugMessage("Try deleting again later");

                // Clear pending state
                isDeletePending = false;
                pendingPresignedUrl = null;
                pendingFilePath = null;
                pendingS3Key = null;
                currentAttempt = 0;

                // Notify gallery that delete failed
                if (GalleryViewController.Instance != null)
                {
                    GalleryViewController.Instance.OnDeleteComplete(false, localFilePath);
                }
            }
        }

        request.Dispose();
    }

    /// <summary>
    /// Delete file from local disk
    /// </summary>
    private bool DeleteLocalFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            DebugViewController.AddDebugMessage("Local file already deleted or not found");
            return false;
        }

        try
        {
            File.Delete(filePath);
            return true;
        }
        catch (Exception e)
        {
            DebugViewController.AddDebugMessage($"Local delete error: {e.Message}");
            return false;
        }
    }
}