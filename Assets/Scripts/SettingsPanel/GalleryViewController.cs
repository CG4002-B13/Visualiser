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
    [SerializeField] private TextMeshProUGUI timestampText;

    [Header("Delete Button")]
    [SerializeField] private TextMeshProUGUI deleteFSIOButtonText;

    // Currently displayed screenshot data
    private string currentDisplayedFilePath;
    private Texture2D currentDisplayedTexture;
    private DateTime currentDisplayedTimestamp;

    // Thumbnail cache (loaded textures for gallery grid)
    private List<Texture2D> thumbnailTextures = new List<Texture2D>();

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

        if (fullScreenOverlay != null)
        {
            fullScreenOverlay.SetActive(false);
        }

        Debug.Log("GalleryViewController initialized (disk-based mode)");
    }

    public void OnTabOpened()
    {
        RefreshGallery();
    }

    /// <summary>
    /// Refresh gallery by loading screenshots from disk
    /// </summary>
    public void RefreshGallery()
    {
        // Clear existing thumbnails and textures
        ClearGalleryGrid();
        UnloadThumbnailTextures();

        // Get current username (lowercase)
        string username = SettingsMenuController.Instance != null
            ? SettingsMenuController.Instance.GetUsername()
            : "default";

        if (string.IsNullOrWhiteSpace(username))
        {
            username = "default";
        }

        username = username.Trim().ToLower();

        DebugViewController.AddDebugMessage($"=== Gallery Opened ===");
        DebugViewController.AddDebugMessage($"Loading screenshots for: {username}");

        // Get screenshot files from disk
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

        // Sort by filename (epoch timestamp) - newest first
        Array.Sort(screenshotFiles);
        Array.Reverse(screenshotFiles);

        DebugViewController.AddDebugMessage($"Found {screenshotFiles.Length} screenshot(s)");

        // Create thumbnails for each screenshot
        foreach (string filePath in screenshotFiles)
        {
            CreateThumbnailFromFile(filePath);
        }

        Debug.Log($"Gallery refreshed with {screenshotFiles.Length} screenshot(s)");
    }

    /// <summary>
    /// DEPRECATED: Kept for compatibility
    /// </summary>
    public void OnScreenshotCaptured(SessionScreenshotData data)
    {
        Debug.Log("OnScreenshotCaptured called (deprecated - using disk-based loading)");
    }

    /// <summary>
    /// Create thumbnail from file on disk
    /// </summary>
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

        // Load texture from file
        Texture2D texture = LoadTextureFromFile(filePath);
        if (texture == null)
        {
            Debug.LogWarning($"Failed to load texture: {filePath}");
            return;
        }

        // Store texture reference for cleanup
        thumbnailTextures.Add(texture);

        // Instantiate thumbnail prefab
        GameObject thumbnailObj = Instantiate(thumbnailPrefab, gridContent);

        // Get RawImage component and assign texture
        RawImage thumbnailImage = thumbnailObj.GetComponentInChildren<RawImage>();
        if (thumbnailImage != null)
        {
            thumbnailImage.texture = texture;
        }
        else
        {
            Debug.LogError("GalleryViewController: RawImage not found in thumbnail prefab!");
        }

        // Get Button component and add click listener
        Button button = thumbnailObj.GetComponent<Button>();
        if (button != null)
        {
            // Capture filePath in closure
            string capturedPath = filePath;
            button.onClick.AddListener(() => ShowFullScreenImage(capturedPath));
        }
        else
        {
            Debug.LogError("GalleryViewController: Button not found in thumbnail prefab!");
        }
    }

    /// <summary>
    /// Load texture from JPG file
    /// </summary>
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

    /// <summary>
    /// Clear all thumbnails from grid
    /// </summary>
    private void ClearGalleryGrid()
    {
        if (gridContent == null) return;

        foreach (Transform child in gridContent)
        {
            Destroy(child.gameObject);
        }
    }

    /// <summary>
    /// Unload all thumbnail textures to free memory
    /// </summary>
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
    /// Show full-screen image overlay
    /// </summary>
    private void ShowFullScreenImage(string filePath)
    {
        if (fullScreenOverlay == null || fullScreenRawImage == null)
        {
            Debug.LogError("GalleryViewController: Full-screen components not assigned!");
            return;
        }

        // Load full-resolution texture
        Texture2D texture = LoadTextureFromFile(filePath);
        if (texture == null)
        {
            DebugViewController.AddDebugMessage($"Failed to load image: {Path.GetFileName(filePath)}");
            return;
        }

        // Store current display info
        currentDisplayedFilePath = filePath;
        currentDisplayedTexture = texture;

        // Parse timestamp from filename (epoch)
        string filename = Path.GetFileNameWithoutExtension(filePath);
        if (long.TryParse(filename, out long epoch))
        {
            currentDisplayedTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(epoch).DateTime.ToLocalTime();
        }
        else
        {
            currentDisplayedTimestamp = File.GetCreationTime(filePath);
        }

        // Assign texture to full-screen image
        fullScreenRawImage.texture = texture;

        // Update timestamp text
        if (timestampText != null)
        {
            timestampText.text = currentDisplayedTimestamp.ToString("MMM dd, yyyy\nhh:mm tt");
        }

        // Reset delete button state
        SetDeleteButtonState(true, "Delete");

        // Show overlay
        fullScreenOverlay.SetActive(true);

        Debug.Log($"Showing full-screen: {Path.GetFileName(filePath)}");
    }

    /// <summary>
    /// Hide full-screen overlay
    /// </summary>
    private void HideFullScreenImage()
    {
        if (fullScreenOverlay != null)
        {
            fullScreenOverlay.SetActive(false);
        }

        // Clear texture reference
        if (fullScreenRawImage != null)
        {
            fullScreenRawImage.texture = null;
        }

        // Unload full-resolution texture to free memory
        if (currentDisplayedTexture != null)
        {
            Destroy(currentDisplayedTexture);
            currentDisplayedTexture = null;
        }

        currentDisplayedFilePath = null;

        Debug.Log("Full-screen image closed");
    }

    /// <summary>
    /// Delete currently displayed screenshot
    /// Called when user taps delete button
    /// </summary>
    private void DeleteCurrentScreenshot()
    {
        if (string.IsNullOrEmpty(currentDisplayedFilePath))
        {
            Debug.LogWarning("GalleryViewController: No screenshot currently displayed");
            return;
        }

        // Check if delete is already in progress
        if (ScreenshotDeleteManager.Instance != null && ScreenshotDeleteManager.Instance.IsDeletePending)
        {
            DebugViewController.AddDebugMessage("Delete already in progress, please wait");
            return;
        }

        string filename = Path.GetFileName(currentDisplayedFilePath);
        DebugViewController.AddDebugMessage($"=== Delete Initiated ===");
        DebugViewController.AddDebugMessage($"File: {filename}");

        // Disable delete button and show "Deleting..." text
        SetDeleteButtonState(false, "Deleting...");

        // Initiate delete via ScreenshotDeleteManager
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
    /// Callback when delete operation completes
    /// Called by ScreenshotDeleteManager
    /// </summary>
    public void OnDeleteComplete(bool success, string filePath)
    {
        if (success)
        {
            DebugViewController.AddDebugMessage("✓ Delete operation complete");

            // Close FSIO
            HideFullScreenImage();

            // Refresh gallery to remove deleted screenshot
            RefreshGallery();
        }
        else
        {
            DebugViewController.AddDebugMessage("✗ Delete operation failed");

            // Re-enable delete button
            SetDeleteButtonState(true, "Delete");

            // Keep FSIO open so user can see screenshot is still there
        }
    }

    /// <summary>
    /// Set delete button state (enabled/disabled) and text
    /// </summary>
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

    private void OnDestroy()
    {
        // Cleanup textures when controller is destroyed
        UnloadThumbnailTextures();

        if (currentDisplayedTexture != null)
        {
            Destroy(currentDisplayedTexture);
        }
    }
}