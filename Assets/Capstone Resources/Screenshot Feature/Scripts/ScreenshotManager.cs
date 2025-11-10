using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Not using this file in the project. 
/// This file was made as for use before we transitioned to using ScreenshotManagerIOS file.
/// </summary>

public class ScreenshotManager : MonoBehaviour
{
    public static ScreenshotManager Instance { get; private set; }

    [Header("Screenshot Button")]
    [SerializeField] private Button screenshotButton;

    [Header("Screenshot Settings")]
    [SerializeField] private int screenshotScale = 1; // 1 = native resolution, 2 = 2x resolution
    [SerializeField] private bool hideButtonDuringCapture = true;

    [Header("Visual Feedback")]
    [SerializeField] private Image flashPanel; // Optional: white panel for flash effect
    [SerializeField] private float flashDuration = 0.1f;

    [Header("Audio Feedback (Optional)")]
    [SerializeField] private AudioSource shutterSound;

    private bool isCapturing = false;

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
        // Setup button listener
        if (screenshotButton != null)
        {
            screenshotButton.onClick.AddListener(CaptureScreenshot);
        }

        // Hide flash panel initially
        if (flashPanel != null)
        {
            flashPanel.gameObject.SetActive(false);
        }

        // Request photo library permissions for iOS
#if UNITY_IOS
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            Application.RequestUserAuthorization(UserAuthorization.WebCam);
        }
#endif
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

        // Hide screenshot button if configured
        bool wasButtonActive = false;
        if (hideButtonDuringCapture && screenshotButton != null)
        {
            wasButtonActive = screenshotButton.gameObject.activeSelf;
            screenshotButton.gameObject.SetActive(false);
        }

        // Wait for end of frame to ensure all rendering is complete
        yield return new WaitForEndOfFrame();

        // Generate filename and path
        string filename = GenerateFilename();
        string fullPath = Path.Combine(Application.persistentDataPath, filename);

        bool success = false;

        try
        {
            // Capture screenshot with UI overlays
            ScreenCapture.CaptureScreenshot(filename, screenshotScale);

            Debug.Log($"Screenshot captured: {fullPath}");

            // Play shutter sound
            if (shutterSound != null)
            {
                shutterSound.Play();
            }

            // Show flash effect
            if (flashPanel != null)
            {
                StartCoroutine(FlashEffect());
            }

            success = true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to capture screenshot: {e.Message}");
            success = false;
        }

        // MOVED OUTSIDE try-catch: Wait for file to be written
        if (success)
        {
            yield return new WaitForSeconds(0.5f);

            // Save to iOS photo library
#if UNITY_IOS
            SaveToPhotoLibrary(fullPath);
#endif

            // Show success feedback
            if (UIManager.Instance != null)
            {
                Debug.Log("Screenshot saved to Photos!");
            }
        }

        // Restore button visibility
        if (hideButtonDuringCapture && screenshotButton != null)
        {
            screenshotButton.gameObject.SetActive(wasButtonActive);
        }

        isCapturing = false;
    }


    private string GenerateFilename()
    {
        // Format: Screenshot_2025-10-05_15-30-45.png
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        return $"Screenshot_{timestamp}.png";
    }

    private IEnumerator FlashEffect()
    {
        if (flashPanel == null) yield break;

        // Show white flash
        flashPanel.gameObject.SetActive(true);
        Color flashColor = flashPanel.color;
        flashColor.a = 1f;
        flashPanel.color = flashColor;

        // Fade out
        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / flashDuration);
            flashColor.a = alpha;
            flashPanel.color = flashColor;
            yield return null;
        }

        flashPanel.gameObject.SetActive(false);
    }

#if UNITY_IOS
    private void SaveToPhotoLibrary(string filePath)
    {
        if (File.Exists(filePath))
        {
            // Use NativeGallery plugin or iOS native code to save
            // For now, we'll use a simple approach with Application.persistentDataPath
            // The file will be accessible through Files app

            // Note: For direct Photos app integration, you'll need to add
            // NSPhotoLibraryUsageDescription and NSPhotoLibraryAddUsageDescription
            // to your Info.plist

            Debug.Log($"Screenshot saved to: {filePath}");
            Debug.Log("Access via Files app > On My iPhone > [Your App Name]");
        }
    }
#endif
}
