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

    [Header("Visual Feedback")]
    [SerializeField] private Image flashPanel;
    [SerializeField] private float flashDuration = 0.15f;

    [Header("Audio Feedback")]
    [SerializeField] private AudioClip shutterSoundClip;
    private AudioSource audioSource;

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

                // Play shutter sound
                if (audioSource != null && shutterSoundClip != null)
                {
                    audioSource.PlayOneShot(shutterSoundClip);
                }

                // Show flash effect
                StartCoroutine(FlashEffect());

                captureSuccess = true;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Screenshot failed: {e.Message}");
            captureSuccess = false;
        }
        finally
        {
            // Cleanup texture
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
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save to gallery: {e.Message}");
            }
        }
#endif
    }
}
