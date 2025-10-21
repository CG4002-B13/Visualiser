using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GalleryViewController : MonoBehaviour
{
    public static GalleryViewController Instance { get; private set; }

    [Header("Prefab References")]
    [SerializeField] private GameObject thumbnailPrefab; // GalleryImageButtonPrefab

    [Header("Gallery Grid")]
    [SerializeField] private Transform gridContent; // Content GameObject (child of ScrollView)

    [Header("Full-Screen Overlay")]
    [SerializeField] private GameObject fullScreenOverlay; // FullScreenImageOverlay
    [SerializeField] private RawImage fullScreenRawImage; // FullScreenRawImage (inside ImageContainer)
    [SerializeField] private Button backgroundDimmer; // BackgroundDimmer (to close on tap)
    [SerializeField] private Button closeFSIOButton; // CloseFSIOButton (explicit close)
    [SerializeField] private Button deleteFSIOButton; // DeleteFSIOButton (delete screenshot)
    [SerializeField] private TextMeshProUGUI timestampText; // TimeStampSIOText (display timestamp)

    // Currently displayed screenshot in full-screen view
    private SessionScreenshotData currentDisplayedScreenshot;

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
        // Setup full-screen overlay button listeners
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

        // Ensure overlay is hidden on start
        if (fullScreenOverlay != null)
        {
            fullScreenOverlay.SetActive(false);
        }

        Debug.Log("GalleryViewController initialized");
    }

    /// <summary>
    /// Called when the Gallery Tab is opened via SettingsPanelController
    /// </summary>
    public void OnTabOpened()
    {
        RefreshGallery();

        // Log to Debug panel for sanity check
        int screenshotCount = ScreenshotManagerIOS.Instance != null
            ? ScreenshotManagerIOS.Instance.GetSessionScreenshotCount()
            : 0;

        DebugViewController.AddDebugMessage($"Gallery opened: {screenshotCount} screenshot(s) available");
        Debug.Log($"Gallery Tab Opened - {screenshotCount} screenshots in session");
    }

    /// <summary>
    /// Refresh the gallery grid by rebuilding all thumbnails from session screenshots
    /// </summary>
    public void RefreshGallery()
    {
        // Clear existing thumbnails
        ClearGalleryGrid();

        // Get session screenshots from ScreenshotManagerIOS
        if (ScreenshotManagerIOS.Instance == null)
        {
            Debug.LogWarning("GalleryViewController: ScreenshotManagerIOS.Instance is null!");
            return;
        }

        List<SessionScreenshotData> screenshots = ScreenshotManagerIOS.Instance.GetSessionScreenshots();

        if (screenshots == null || screenshots.Count == 0)
        {
            Debug.Log("No screenshots in session to display");
            return;
        }

        // Create thumbnails for each screenshot
        foreach (SessionScreenshotData data in screenshots)
        {
            CreateThumbnail(data);
        }

        Debug.Log($"Gallery refreshed with {screenshots.Count} screenshot(s)");
    }

    /// <summary>
    /// Called by ScreenshotManagerIOS when a new screenshot is captured
    /// </summary>
    public void OnScreenshotCaptured(SessionScreenshotData data)
    {
        // Only create thumbnail if gallery tab is currently active
        // This avoids instantiation issues when gallery is not visible
        if (gameObject.activeInHierarchy)
        {
            CreateThumbnail(data);
            Debug.Log($"New thumbnail added to gallery: {data.fileName}");
        }
    }

    /// <summary>
    /// Create and instantiate a thumbnail button for a screenshot
    /// </summary>
    private void CreateThumbnail(SessionScreenshotData data)
    {
        if (thumbnailPrefab == null || gridContent == null)
        {
            Debug.LogError("GalleryViewController: thumbnailPrefab or gridContent is not assigned!");
            return;
        }

        // Instantiate thumbnail prefab as child of grid content
        GameObject thumbnailObj = Instantiate(thumbnailPrefab, gridContent);

        // Store reference to thumbnail object in session data
        data.thumbnailObject = thumbnailObj;

        // Get the RawImage component from the thumbnail
        RawImage thumbnailImage = thumbnailObj.GetComponentInChildren<RawImage>();
        if (thumbnailImage != null && data.texture != null)
        {
            // Assign the screenshot texture
            thumbnailImage.texture = data.texture;
        }
        else
        {
            Debug.LogError("GalleryViewController: RawImage component not found in thumbnail prefab or texture is null!");
        }

        // Get the Button component and add click listener
        Button button = thumbnailObj.GetComponent<Button>();
        if (button != null)
        {
            // Use lambda to capture the specific screenshot data for this button
            button.onClick.AddListener(() => ShowFullScreenImage(data));
        }
        else
        {
            Debug.LogError("GalleryViewController: Button component not found in thumbnail prefab!");
        }
    }

    /// <summary>
    /// Clear all thumbnails from the gallery grid
    /// </summary>
    private void ClearGalleryGrid()
    {
        if (gridContent == null) return;

        // Destroy all children of the grid content
        foreach (Transform child in gridContent)
        {
            Destroy(child.gameObject);
        }
    }

    /// <summary>
    /// Show the full-screen overlay with the selected screenshot
    /// </summary>
    private void ShowFullScreenImage(SessionScreenshotData data)
    {
        if (fullScreenOverlay == null || fullScreenRawImage == null)
        {
            Debug.LogError("GalleryViewController: Full-screen overlay components not assigned!");
            return;
        }

        // Store reference to currently displayed screenshot
        currentDisplayedScreenshot = data;

        // Assign texture to full-screen RawImage
        fullScreenRawImage.texture = data.texture;

        // Update timestamp text if available
        if (timestampText != null)
        {
            timestampText.text = data.timestamp.ToString("MMM dd, yyyy\nhh:mm tt");
        }

        // Show the overlay
        fullScreenOverlay.SetActive(true);

        Debug.Log($"Showing full-screen image: {data.fileName}");
    }

    /// <summary>
    /// Hide the full-screen overlay and return to gallery grid
    /// </summary>
    private void HideFullScreenImage()
    {
        if (fullScreenOverlay != null)
        {
            fullScreenOverlay.SetActive(false);
        }

        // Clear the texture reference
        if (fullScreenRawImage != null)
        {
            fullScreenRawImage.texture = null;
        }

        // Clear current screenshot reference
        currentDisplayedScreenshot = null;

        Debug.Log("Full-screen image closed");
    }

    /// <summary>
    /// Delete the currently displayed screenshot from session and disk
    /// </summary>
    private void DeleteCurrentScreenshot()
    {
        if (currentDisplayedScreenshot == null)
        {
            Debug.LogWarning("GalleryViewController: No screenshot is currently displayed to delete");
            return;
        }

        Debug.Log($"Deleting screenshot: {currentDisplayedScreenshot.fileName}");

        // Destroy the thumbnail GameObject from the grid
        if (currentDisplayedScreenshot.thumbnailObject != null)
        {
            Destroy(currentDisplayedScreenshot.thumbnailObject);
        }

        // Remove from ScreenshotManagerIOS (this also deletes the file and texture)
        if (ScreenshotManagerIOS.Instance != null)
        {
            ScreenshotManagerIOS.Instance.RemoveScreenshotFromSession(currentDisplayedScreenshot);
        }

        // Close the full-screen overlay
        HideFullScreenImage();

        // Log to Debug panel
        int remainingCount = ScreenshotManagerIOS.Instance != null
            ? ScreenshotManagerIOS.Instance.GetSessionScreenshotCount()
            : 0;

        DebugViewController.AddDebugMessage($"Screenshot deleted. Remaining: {remainingCount}");
    }
}