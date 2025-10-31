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

        // Get current username
        string username = SettingsMenuController.Instance != null
            ? SettingsMenuController.Instance.GetUsername()
            : "default";

        if (string.IsNullOrWhiteSpace(username))
        {
            username = "default";
        }

        username = username.Trim();

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
        // No longer used - gallery refreshes from disk when opened
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
    /// </summary>
    private void DeleteCurrentScreenshot()
    {
        if (string.IsNullOrEmpty(currentDisplayedFilePath))
        {
            Debug.LogWarning("GalleryViewController: No screenshot currently displayed");
            return;
        }

        string filename = Path.GetFileName(currentDisplayedFilePath);
        DebugViewController.AddDebugMessage($"=== Deleting Screenshot ===");
        DebugViewController.AddDebugMessage($"File: {filename}");

        // Delete from disk
        bool deleteSuccess = false;
        if (ScreenshotManagerIOS.Instance != null)
        {
            deleteSuccess = ScreenshotManagerIOS.Instance.DeleteScreenshot(currentDisplayedFilePath);
        }

        if (deleteSuccess)
        {
            DebugViewController.AddDebugMessage("Local file deleted");

            // TODO: Delete from S3 (Phase 2 - Delete implementation)
            DebugViewController.AddDebugMessage("Cloud deletion not yet implemented");

            // Close full-screen overlay
            HideFullScreenImage();

            // Refresh gallery to show updated list
            RefreshGallery();
        }
        else
        {
            DebugViewController.AddDebugMessage("Failed to delete local file");
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