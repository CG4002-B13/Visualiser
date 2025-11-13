using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GalleryViewController : MonoBehaviour
{
    public static GalleryViewController Instance { get; private set; }

    [Header("Prefab References")]
    [SerializeField] private GameObject thumbnailPrefab;

    [Header("Gallery Grid")]
    [SerializeField] private Transform gridContent;

    [Header("Full-Screen Overlay")]
    [SerializeField] private GameObject fullScreenOverlay;
    [SerializeField] private RawImage fullScreenRawImage;
    [SerializeField] private Button backgroundDimmer;
    [SerializeField] private Button closeFSIOButton;
    [SerializeField] private Button deleteFSIOButton;
    [SerializeField] private Button downloadFSIOButton;
    [SerializeField] private TextMeshProUGUI timestampText;

    [Header("Delete Button")]
    [SerializeField] private TextMeshProUGUI deleteFSIOButtonText;

    [Header("Download Button")]
    [SerializeField] private TextMeshProUGUI downloadFSIOButtonText;

    [Header("Sync Status UI")]
    [SerializeField] private TextMeshProUGUI connectionStatusText;
    [SerializeField] private TextMeshProUGUI syncRequiredText;
    [SerializeField] private Button syncButton;
    [SerializeField] private TextMeshProUGUI syncButtonText;

    // Currently displayed screenshot data
    private string currentDisplayedFilePath;
    private Texture2D currentDisplayedTexture;
    private DateTime currentDisplayedTimestamp;
    private string currentDisplayedFilename;  // ← Store filename

    // Thumbnail cache (loaded textures for gallery grid)
    private List<Texture2D> thumbnailTextures = new List<Texture2D>();

    // Sync tracking
    private int pendingUploads = 0;
    private int pendingDownloads = 0;
    private bool isSyncInProgress = false;

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
        if (closeFSIOButton != null)
        {
            closeFSIOButton.onClick.AddListener(HideFullScreenImage);
        }

        if (backgroundDimmer != null)
        {
            backgroundDimmer.onClick.AddListener(HideFullScreenImage);
        }

        if (deleteFSIOButton != null)
        {
            deleteFSIOButton.onClick.AddListener(DeleteCurrentScreenshot);
        }

        if (downloadFSIOButton != null)
        {
            downloadFSIOButton.onClick.AddListener(DownloadCurrentScreenshot);
        }

        if (syncButton != null)
        {
            syncButton.onClick.AddListener(OnSyncButtonClicked);
        }

        if (fullScreenOverlay != null)
        {
            fullScreenOverlay.SetActive(false);
        }

        Debug.Log("GalleryViewController initialized (disk-based mode)");
    }

    private void OnEnable()
    {
        WS_Client.OnConnectionStatusChanged += OnConnectionStatusChanged;
    }

    private void OnDisable()
    {
        WS_Client.OnConnectionStatusChanged -= OnConnectionStatusChanged;
    }

    public void OnTabOpened()
    {
        DebugViewController.AddDebugMessage("=== Gallery Tab Opened ===");

        RefreshGallery();
        UpdateConnectionStatus();
        StartCoroutine(DeferredSyncCheck());
    }

    private IEnumerator DeferredSyncCheck()
    {
        yield return new WaitForSeconds(2f);

        if (gameObject.activeInHierarchy)
        {
            CheckSyncRequired();
        }
    }

    private void OnConnectionStatusChanged(bool isConnected)
    {
        UpdateConnectionStatus();

        if (!isConnected)
        {
            SetSyncButtonState(false);
            DebugViewController.AddDebugMessage("Connection lost - sync disabled");
        }
    }

    public void RefreshGallery()
    {
        ClearGalleryGrid();
        UnloadThumbnailTextures();

        string username = SettingsMenuController.Instance != null
            ? SettingsMenuController.Instance.GetUsername()
            : "default";

        if (string.IsNullOrWhiteSpace(username))
        {
            username = "default";
        }

        username = username.Trim().ToLower();

        DebugViewController.AddDebugMessage($"Loading screenshots for: {username}");

        if (ScreenshotManagerIOS.Instance == null)
        {
            Debug.LogWarning("GalleryViewController: ScreenshotManagerIOS.Instance is null!");
            return;
        }

        string[] screenshotFiles = ScreenshotManagerIOS.Instance.GetUserScreenshotFiles(username);

        if (screenshotFiles == null || screenshotFiles.Length == 0)
        {
            DebugViewController.AddDebugMessage("No screenshots found for this user");
            Debug.Log($"No screenshots found for user: {username}");
            return;
        }

        Array.Sort(screenshotFiles);
        Array.Reverse(screenshotFiles);

        DebugViewController.AddDebugMessage($"Found {screenshotFiles.Length} screenshot(s)");

        foreach (string filePath in screenshotFiles)
        {
            CreateThumbnailFromFile(filePath);
        }

        Debug.Log($"Gallery refreshed with {screenshotFiles.Length} screenshot(s)");
    }

    private void CheckSyncRequired()
    {
        if (!WS_Client.Instance.IsConnected)
        {
            DebugViewController.AddDebugMessage("Cannot check sync - offline");
            syncRequiredText.text = "";
            SetSyncButtonState(false);
            return;
        }

        long memoryUsedMB = System.GC.GetTotalMemory(false) / (1024 * 1024);
        long availableMemoryMB = 2048 - memoryUsedMB;

        DebugViewController.AddDebugMessage($"Memory check: {memoryUsedMB}MB used, {availableMemoryMB}MB available");

        if (availableMemoryMB < 200)
        {
            DebugViewController.AddDebugMessage("===Insufficient memory for sync check - skipping===");
            syncRequiredText.text = "Low memory - try later";
            SetSyncButtonState(false);
            return;
        }

        DebugViewController.AddDebugMessage("Checking sync status...");

        if (ScreenshotSyncManager.Instance != null)
        {
            ScreenshotSyncManager.Instance.CheckSyncStatus(
                onComplete: (uploadsNeeded, downloadsNeeded) =>
                {
                    if (!gameObject.activeInHierarchy) return;

                    pendingUploads = uploadsNeeded;
                    pendingDownloads = downloadsNeeded;
                    UpdateSyncRequiredText();
                    SetSyncButtonState(true);
                }
            );
        }
    }

    private void UpdateSyncRequiredText()
    {
        if (pendingUploads == 0 && pendingDownloads == 0)
        {
            syncRequiredText.text = "";
            DebugViewController.AddDebugMessage(" ===Fully synced with S3===");
        }
        else
        {
            syncRequiredText.text = $"Sync Required - {pendingUploads} Uploads, {pendingDownloads} Downloads";
            DebugViewController.AddDebugMessage($"Sync status: {pendingUploads} uploads, {pendingDownloads} downloads needed");
        }
    }

    private void OnSyncButtonClicked()
    {
        if (isSyncInProgress)
        {
            DebugViewController.AddDebugMessage("Sync already in progress");
            return;
        }

        if (!WS_Client.Instance.IsConnected)
        {
            DebugViewController.AddDebugMessage("Cannot sync - not connected");
            return;
        }

        SetSyncButtonState(false);
        if (syncButtonText != null)
        {
            syncButtonText.text = "Syncing...";
        }
        isSyncInProgress = true;

        DebugViewController.AddDebugMessage("Starting sync operation...");

        if (ScreenshotSyncManager.Instance != null)
        {
            ScreenshotSyncManager.Instance.PerformSync(
                onComplete: (success) =>
                {
                    isSyncInProgress = false;

                    if (success)
                    {
                        DebugViewController.AddDebugMessage(" ===Sync completed successfully===");
                        RefreshGallery();
                        CheckSyncRequired();
                    }
                    else
                    {
                        DebugViewController.AddDebugMessage(" =#= Sync failed =#=");
                        SetSyncButtonState(true);
                        if (syncButtonText != null)
                        {
                            syncButtonText.text = "Sync";
                        }
                    }
                }
            );
        }
    }

    private void UpdateConnectionStatus()
    {
        if (WS_Client.Instance != null && WS_Client.Instance.IsConnected)
        {
            if (connectionStatusText != null)
            {
                connectionStatusText.text = "Connected to Server: Yes";
            }
        }
        else
        {
            if (connectionStatusText != null)
            {
                connectionStatusText.text = "Connected to Server: No";
            }
        }
    }

    private void SetSyncButtonState(bool enabled)
    {
        if (syncButton != null)
        {
            syncButton.interactable = enabled;
        }

        if (enabled && syncButtonText != null)
        {
            syncButtonText.text = "Sync";
        }
    }

    private void CreateThumbnailFromFile(string filePath)
    {
        if (thumbnailPrefab == null || gridContent == null)
        {
            Debug.LogError("GalleryViewController: thumbnailPrefab or gridContent not assigned!");
            return;
        }

        if (!File.Exists(filePath))
        {
            Debug.LogWarning($"File not found: {filePath}");
            return;
        }

        Texture2D texture = LoadTextureFromFile(filePath);
        if (texture == null)
        {
            Debug.LogWarning($"Failed to load texture: {filePath}");
            return;
        }

        thumbnailTextures.Add(texture);
        GameObject thumbnailObj = Instantiate(thumbnailPrefab, gridContent);

        RawImage thumbnailImage = thumbnailObj.GetComponentInChildren<RawImage>();
        if (thumbnailImage != null)
        {
            thumbnailImage.texture = texture;
        }
        else
        {
            Debug.LogError("GalleryViewController: RawImage not found in thumbnail prefab!");
        }

        Button button = thumbnailObj.GetComponent<Button>();
        if (button != null)
        {
            string capturedPath = filePath;
            button.onClick.AddListener(() => ShowFullScreenImage(capturedPath));
        }
        else
        {
            Debug.LogError("GalleryViewController: Button not found in thumbnail prefab!");
        }
    }

    private Texture2D LoadTextureFromFile(string filePath)
    {
        try
        {
            byte[] fileData = File.ReadAllBytes(filePath);
            Texture2D texture = new Texture2D(2, 2);
            if (texture.LoadImage(fileData))
            {
                return texture;
            }
            else
            {
                Destroy(texture);
                return null;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load texture: {e.Message}");
            return null;
        }
    }

    private void ClearGalleryGrid()
    {
        if (gridContent == null) return;

        foreach (Transform child in gridContent)
        {
            Destroy(child.gameObject);
        }
    }

    private void UnloadThumbnailTextures()
    {
        foreach (Texture2D texture in thumbnailTextures)
        {
            if (texture != null)
            {
                Destroy(texture);
            }
        }

        thumbnailTextures.Clear();
    }

    /// <summary>
    /// Show full-screen image overlay with filename and timestamp
    /// </summary>
    private void ShowFullScreenImage(string filePath)
    {
        if (fullScreenOverlay == null || fullScreenRawImage == null)
        {
            Debug.LogError("GalleryViewController: Full-screen components not assigned!");
            return;
        }

        Texture2D texture = LoadTextureFromFile(filePath);
        if (texture == null)
        {
            DebugViewController.AddDebugMessage($"Failed to load image: {Path.GetFileName(filePath)}");
            return;
        }

        currentDisplayedFilePath = filePath;
        currentDisplayedTexture = texture;
        currentDisplayedFilename = Path.GetFileName(filePath);  // Store filename

        string filename = Path.GetFileNameWithoutExtension(filePath);
        if (long.TryParse(filename, out long epoch))
        {
            currentDisplayedTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(epoch).DateTime.ToLocalTime();
        }
        else
        {
            currentDisplayedTimestamp = File.GetCreationTime(filePath);
        }

        fullScreenRawImage.texture = texture;

        // ===== Show filename + timestamp =====
        if (timestampText != null)
        {
            string timeString = currentDisplayedTimestamp.ToString("MMM dd, yyyy\nhh:mm tt");
            timestampText.text = $"{currentDisplayedFilename}\n{timeString}";
        }

        SetDeleteButtonState(true, "Delete");
        SetDownloadButtonState(true, "Download Image");

        fullScreenOverlay.SetActive(true);

        Debug.Log($"Showing full-screen: {Path.GetFileName(filePath)}");
    }

    private void HideFullScreenImage()
    {
        if (fullScreenOverlay != null)
        {
            fullScreenOverlay.SetActive(false);
        }

        if (fullScreenRawImage != null)
        {
            fullScreenRawImage.texture = null;
        }

        if (currentDisplayedTexture != null)
        {
            Destroy(currentDisplayedTexture);
            currentDisplayedTexture = null;
        }

        currentDisplayedFilePath = null;
        currentDisplayedFilename = null;

        Debug.Log("Full-screen image closed");
    }

    private void DeleteCurrentScreenshot()
    {
        if (string.IsNullOrEmpty(currentDisplayedFilePath))
        {
            Debug.LogWarning("GalleryViewController: No screenshot currently displayed");
            return;
        }

        if (ScreenshotDeleteManager.Instance != null && ScreenshotDeleteManager.Instance.IsDeletePending)
        {
            DebugViewController.AddDebugMessage("Delete already in progress, please wait");
            return;
        }

        string filename = Path.GetFileName(currentDisplayedFilePath);
        DebugViewController.AddDebugMessage($"=== Delete Initiated ===");
        DebugViewController.AddDebugMessage($"File: {filename}");

        SetDeleteButtonState(false, "Deleting...");

        if (ScreenshotDeleteManager.Instance != null)
        {
            ScreenshotDeleteManager.Instance.DeleteScreenshot(currentDisplayedFilePath);
        }
        else
        {
            DebugViewController.AddDebugMessage("ERROR: ScreenshotDeleteManager not found");
            SetDeleteButtonState(true, "Delete");
        }
    }

    /// <summary>
    /// Download currently displayed screenshot to iOS Photos
    /// </summary>
    private void DownloadCurrentScreenshot()
    {
        if (string.IsNullOrEmpty(currentDisplayedFilePath))
        {
            Debug.LogWarning("GalleryViewController: No screenshot currently displayed");
            return;
        }

        if (ScreenshotDownloadManager.Instance != null && ScreenshotDownloadManager.Instance.IsDownloadPending)
        {
            DebugViewController.AddDebugMessage("Download already in progress, please wait");
            return;
        }

        string filename = Path.GetFileName(currentDisplayedFilePath);
        DebugViewController.AddDebugMessage($"=== Download Initiated ===");
        DebugViewController.AddDebugMessage($"File: {filename}");

        SetDownloadButtonState(false, "Downloading Image...");

        if (ScreenshotDownloadManager.Instance != null)
        {
            ScreenshotDownloadManager.Instance.DownloadScreenshotToPhotos(currentDisplayedFilePath);
        }
        else
        {
            DebugViewController.AddDebugMessage("ERROR: ScreenshotDownloadManager not found");
            SetDownloadButtonState(true, "Download Image");
        }
    }

    public void OnDeleteComplete(bool success, string filePath)
    {
        if (success)
        {
            DebugViewController.AddDebugMessage(" === Delete operation complete ===");
            HideFullScreenImage();
            RefreshGallery();
            CheckSyncRequired();
        }
        else
        {
            DebugViewController.AddDebugMessage(" =#= Delete operation failed =#=");
            SetDeleteButtonState(true, "Delete");
        }
    }

    public void OnDownloadComplete(bool success, string filePath)
    {
        if (success)
        {
            DebugViewController.AddDebugMessage(" === Download operation complete ===");
            SetDownloadButtonState(true, "Download Image");
        }
        else
        {
            DebugViewController.AddDebugMessage(" =#= Download operation failed =#=");
            SetDownloadButtonState(true, "Download Image");
        }
    }

    private void SetDeleteButtonState(bool enabled, string text)
    {
        if (deleteFSIOButton != null)
        {
            deleteFSIOButton.interactable = enabled;
        }

        if (deleteFSIOButtonText != null)
        {
            deleteFSIOButtonText.text = text;
        }
    }

    private void SetDownloadButtonState(bool enabled, string text)
    {
        if (downloadFSIOButton != null)
        {
            downloadFSIOButton.interactable = enabled;
        }

        if (downloadFSIOButtonText != null)
        {
            downloadFSIOButtonText.text = text;
        }
    }

    private void OnDestroy()
    {
        UnloadThumbnailTextures();

        if (currentDisplayedTexture != null)
        {
            Destroy(currentDisplayedTexture);
        }
    }
}