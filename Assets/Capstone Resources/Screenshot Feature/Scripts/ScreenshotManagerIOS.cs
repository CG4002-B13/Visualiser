using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

public class ScreenshotManagerIOS : MonoBehaviour
{
    public static ScreenshotManagerIOS Instance { get; private set; }

    [Header("Screenshot Button")]
    [SerializeField] private Button screenshotButton;

    [Header("Screenshot Settings")]
    [SerializeField] private int screenshotScale = 2;
    [SerializeField] private bool hideButtonDuringCapture = true;
    [SerializeField][Range(0, 100)] private int jpgQuality = 85;

    [Header("Visual Feedback")]
    [SerializeField] private Image flashPanel;
    [SerializeField] private float flashDuration = 0.15f;

    [Header("Audio Feedback")]
    [SerializeField] private AudioClip shutterSoundClip;
    private AudioSource audioSource;

    // Session storage for gallery feature (DEPRECATED - kept for compatibility)
    private List<SessionScreenshotData> sessionScreenshots = new List<SessionScreenshotData>();

    private bool isCapturing = false;

#if UNITY_IOS
    [DllImport("__Internal")]
    private static extern void _SaveImageToGallery(string path);
#endif

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

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        if (shutterSoundClip != null)
        {
            audioSource.clip = shutterSoundClip;
        }
    }

    private void Start()
    {
        if (screenshotButton != null)
        {
            screenshotButton.onClick.AddListener(CaptureScreenshot);
        }

        if (flashPanel != null)
        {
            flashPanel.gameObject.SetActive(false);
        }
    }

    public void CaptureScreenshot()
    {
        if (!isCapturing)
        {
            StartCoroutine(CaptureScreenshotCoroutine());
        }
    }

    private IEnumerator CaptureScreenshotCoroutine()
    {
        isCapturing = true;

        // Get username from Settings (convert to lowercase)
        string username = SettingsMenuController.Instance != null
            ? SettingsMenuController.Instance.GetUsername()
            : "default";

        if (string.IsNullOrWhiteSpace(username))
        {
            username = "default";
            DebugViewController.AddDebugMessage("WARNING: No username set, using 'default'");
        }

        username = username.Trim().ToLower();  // Convert to lowercase

        // Hide UI elements we don't want in screenshot
        bool wasButtonActive = false;
        if (hideButtonDuringCapture && screenshotButton != null)
        {
            wasButtonActive = screenshotButton.gameObject.activeSelf;
            screenshotButton.gameObject.SetActive(false);
        }

        // Wait for end of frame
        yield return new WaitForEndOfFrame();

        // Generate epoch timestamp
        long epochTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Build folder structure: screenshots/{username}/
        string screenshotsFolder = Path.Combine(Application.persistentDataPath, "screenshots");
        string userFolder = Path.Combine(screenshotsFolder, username);

        // Create folders if they don't exist
        if (!Directory.Exists(userFolder))
        {
            Directory.CreateDirectory(userFolder);
            Debug.Log($"Created user folder: {userFolder}");
        }

        // Generate filename: {epoch}.jpg
        string filename = $"{epochTimestamp}.jpg";
        string filePath = Path.Combine(userFolder, filename);

        bool captureSuccess = false;
        Texture2D screenshot = null;

        // Capture screenshot
        try
        {
            screenshot = CaptureScreenshotAsTexture();

            if (screenshot != null)
            {
                // Encode as JPG
                byte[] bytes = screenshot.EncodeToJPG(jpgQuality);
                File.WriteAllBytes(filePath, bytes);

                Debug.Log($"Screenshot saved to: {filePath}");
                DebugViewController.AddDebugMessage($"=== Screenshot Captured ===");
                DebugViewController.AddDebugMessage($"Filename: {username}/{filename}");
                DebugViewController.AddDebugMessage($"Size: {bytes.Length / 1024}KB");
                DebugViewController.AddDebugMessage($"Quality: {jpgQuality}");

                // Play shutter sound
                if (audioSource != null && shutterSoundClip != null)
                {
                    audioSource.PlayOneShot(shutterSoundClip);
                }

                // Show flash effect
                StartCoroutine(FlashEffect());

                // Destroy screenshot texture (we'll load from disk when needed)
                Destroy(screenshot);

                captureSuccess = true;

                // Initiate S3 upload
                if (ScreenshotUploadManager.Instance != null)
                {
                    ScreenshotUploadManager.Instance.UploadScreenshot(filePath, epochTimestamp);
                }
                else
                {
                    DebugViewController.AddDebugMessage("WARNING: ScreenshotUploadManager not found");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Screenshot failed: {e.Message}");
            DebugViewController.AddDebugMessage($"Screenshot failed: {e.Message}");
            captureSuccess = false;

            // Cleanup on failure
            if (screenshot != null)
            {
                Destroy(screenshot);
            }
        }

        // Restore button
        if (hideButtonDuringCapture && screenshotButton != null)
        {
            screenshotButton.gameObject.SetActive(wasButtonActive);
        }

        isCapturing = false;
    }

    private Texture2D CaptureScreenshotAsTexture()
    {
        int width = Screen.width * screenshotScale;
        int height = Screen.height * screenshotScale;

        Texture2D screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);

        // Read pixels from screen
        RenderTexture rt = new RenderTexture(width, height, 24);
        Camera.main.targetTexture = rt;
        Camera.main.Render();

        RenderTexture.active = rt;
        screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        screenshot.Apply();

        // Cleanup
        Camera.main.targetTexture = null;
        RenderTexture.active = null;
        Destroy(rt);

        return screenshot;
    }

    private IEnumerator FlashEffect()
    {
        if (flashPanel == null) yield break;

        flashPanel.gameObject.SetActive(true);
        Color color = Color.white;

        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Lerp(1f, 0f, elapsed / flashDuration);
            flashPanel.color = color;
            yield return null;
        }

        flashPanel.gameObject.SetActive(false);
    }

    // DEPRECATED: Kept for compatibility - SessionScreenshotData no longer used
    public List<SessionScreenshotData> GetSessionScreenshots()
    {
        return sessionScreenshots;
    }

    public void RemoveScreenshotFromSession(SessionScreenshotData data)
    {
        // This method is deprecated - gallery now uses disk-based loading
        Debug.LogWarning("RemoveScreenshotFromSession is deprecated");
    }

    public int GetSessionScreenshotCount()
    {
        return sessionScreenshots.Count;
    }

    public int GetMaxSessionScreenshots()
    {
        return 0; // No longer applicable
    }

    /// <summary>
    /// Get screenshots folder path for specific username (lowercase)
    /// </summary>
    public string GetUserScreenshotsFolder(string username)
    {
        string screenshotsFolder = Path.Combine(Application.persistentDataPath, "screenshots");
        return Path.Combine(screenshotsFolder, username.ToLower());
    }

    /// <summary>
    /// Get all screenshot file paths for specific username (lowercase)
    /// </summary>
    public string[] GetUserScreenshotFiles(string username)
    {
        string userFolder = GetUserScreenshotsFolder(username.ToLower());

        if (!Directory.Exists(userFolder))
        {
            return new string[0];
        }

        return Directory.GetFiles(userFolder, "*.jpg", SearchOption.TopDirectoryOnly);
    }

    /// <summary>
    /// Delete screenshot file from disk
    /// </summary>
    public bool DeleteScreenshot(string filePath)
    {
        if (File.Exists(filePath))
        {
            try
            {
                File.Delete(filePath);
                DebugViewController.AddDebugMessage($"Deleted: {Path.GetFileName(filePath)}");
                return true;
            }
            catch (Exception e)
            {
                DebugViewController.AddDebugMessage($"Delete failed: {e.Message}");
                return false;
            }
        }

        return false;
    }

#if UNITY_IOS
    /// <summary>
    /// Save screenshot to iOS Photos (called manually from FSIO button)
    /// </summary>
    public void SaveToIOSPhotos(string filePath)
    {
        if (File.Exists(filePath))
        {
            try
            {
                _SaveImageToGallery(filePath);
                DebugViewController.AddDebugMessage("Saved to iOS Photos");
            }
            catch (Exception e)
            {
                DebugViewController.AddDebugMessage($"iOS Photos save failed: {e.Message}");
            }
        }
    }
#endif
}