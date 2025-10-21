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
    [SerializeField] private int maxSessionScreenshots = 12; // Maximum screenshots stored in session

    [Header("Visual Feedback")]
    [SerializeField] private Image flashPanel;
    [SerializeField] private float flashDuration = 0.15f;

    [Header("Audio Feedback")]
    [SerializeField] private AudioClip shutterSoundClip;
    private AudioSource audioSource;

    // Session storage for gallery feature
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
            // Check if we've reached the session limit
            if (sessionScreenshots.Count >= maxSessionScreenshots)
            {
                DebugViewController.AddDebugMessage($"Screenshot limit reached ({maxSessionScreenshots}). Delete some screenshots first.");
                Debug.LogWarning($"Cannot take more screenshots. Session limit: {maxSessionScreenshots}");
                return;
            }

            StartCoroutine(CaptureScreenshotCoroutine());
        }
    }

    private IEnumerator CaptureScreenshotCoroutine()
    {
        isCapturing = true;

        // Hide UI elements we don't want in screenshot
        bool wasButtonActive = false;
        if (hideButtonDuringCapture && screenshotButton != null)
        {
            wasButtonActive = screenshotButton.gameObject.activeSelf;
            screenshotButton.gameObject.SetActive(false);
        }

        // Wait for end of frame - OUTSIDE try-catch
        yield return new WaitForEndOfFrame();

        // Generate filename and path
        string filename = $"Screenshot_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";
        string filePath = Path.Combine(Application.persistentDataPath, filename);

        bool captureSuccess = false;
        Texture2D screenshot = null;

        // Capture screenshot - try-catch without yield
        try
        {
            screenshot = CaptureScreenshotAsTexture();

            if (screenshot != null)
            {
                // Save to file
                byte[] bytes = screenshot.EncodeToPNG();
                File.WriteAllBytes(filePath, bytes);

                Debug.Log($"Screenshot saved to: {filePath}");
                DebugViewController.AddDebugMessage($"Screenshot captured: {filename}");

                // Play shutter sound
                if (audioSource != null && shutterSoundClip != null)
                {
                    audioSource.PlayOneShot(shutterSoundClip);
                }

                // Show flash effect
                StartCoroutine(FlashEffect());

                // NEW: Store in session memory for gallery
                SessionScreenshotData sessionData = new SessionScreenshotData(
                    screenshot,
                    filePath,
                    DateTime.Now
                );
                sessionScreenshots.Add(sessionData);

                // NEW: Notify GalleryViewController
                if (GalleryViewController.Instance != null)
                {
                    GalleryViewController.Instance.OnScreenshotCaptured(sessionData);
                }

                DebugViewController.AddDebugMessage($"Session screenshots: {sessionScreenshots.Count}/{maxSessionScreenshots}");

                captureSuccess = true;

                // NOTE: We do NOT destroy the texture anymore - it's kept in sessionScreenshots for gallery use
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

        // Wait for file to be written - OUTSIDE try-catch
        if (captureSuccess)
        {
            yield return new WaitForSeconds(0.1f);

            // Save to iOS Photos
#if UNITY_IOS
            SaveToIOSGallery(filePath);
#endif
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

    private void SaveToIOSGallery(string filePath)
    {
#if UNITY_IOS
        if (File.Exists(filePath))
        {
            try
            {
                _SaveImageToGallery(filePath);
                Debug.Log("Screenshot saved to iOS Photos!");
                DebugViewController.AddDebugMessage("Saved to iOS Photos");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save to gallery: {e.Message}");
                DebugViewController.AddDebugMessage($"iOS Photos save failed: {e.Message}");
            }
        }
#endif
    }

    /// <summary>
    /// Get the list of screenshots captured in the current session (for GalleryViewController)
    /// </summary>
    public List<SessionScreenshotData> GetSessionScreenshots()
    {
        return sessionScreenshots;
    }

    /// <summary>
    /// Remove a screenshot from the session list and delete the file from disk
    /// Called by GalleryViewController when user deletes a screenshot
    /// </summary>
    public void RemoveScreenshotFromSession(SessionScreenshotData data)
    {
        if (data == null) return;

        // Remove from session list
        sessionScreenshots.Remove(data);

        // Destroy the texture to free memory
        if (data.texture != null)
        {
            Destroy(data.texture);
        }

        // Delete the PNG file from disk
        if (File.Exists(data.filePath))
        {
            try
            {
                File.Delete(data.filePath);
                Debug.Log($"Deleted screenshot file: {data.fileName}");
                DebugViewController.AddDebugMessage($"Deleted: {data.fileName}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to delete file: {e.Message}");
                DebugViewController.AddDebugMessage($"Delete failed: {e.Message}");
            }
        }

        DebugViewController.AddDebugMessage($"Session screenshots: {sessionScreenshots.Count}/{maxSessionScreenshots}");
    }

    /// <summary>
    /// Get the current count of session screenshots
    /// </summary>
    public int GetSessionScreenshotCount()
    {
        return sessionScreenshots.Count;
    }

    /// <summary>
    /// Get the maximum allowed session screenshots
    /// </summary>
    public int GetMaxSessionScreenshots()
    {
        return maxSessionScreenshots;
    }
}