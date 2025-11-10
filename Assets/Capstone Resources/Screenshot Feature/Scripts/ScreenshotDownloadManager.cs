using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class ScreenshotDownloadManager : MonoBehaviour
{
    public static ScreenshotDownloadManager Instance { get; private set; }

    // Download state tracking
    private bool isDownloadPending = false;
    private string pendingFilePath = null;

    public bool IsDownloadPending => isDownloadPending;

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
    /// Download screenshot to iOS Photos app
    /// Called when user clicks "Download Image" button in FSIO
    /// </summary>
    public void DownloadScreenshotToPhotos(string filePath)
    {
        if (isDownloadPending)
        {
            DebugViewController.AddDebugMessage("Download already in progress, please wait");
            return;
        }

        if (!File.Exists(filePath))
        {
            DebugViewController.AddDebugMessage("ERROR: File not found");
            return;
        }

        isDownloadPending = true;
        pendingFilePath = filePath;

        DebugViewController.AddDebugMessage("=== Starting Image Download ===");
        DebugViewController.AddDebugMessage($"File: {Path.GetFileName(filePath)}");

        StartCoroutine(SaveToPhotosAsync(filePath));
    }

    /// <summary>
    /// Async save to iOS Photos using native plugin
    /// </summary>
    private IEnumerator SaveToPhotosAsync(string filePath)
    {
        // Simulate async operation (native plugin call is synchronous but we'll handle it on main thread)
        yield return null;

        try
        {
#if UNITY_IOS && !UNITY_EDITOR
            // Call native iOS function to save to Photos
            ScreenshotManagerIOS.Instance.SaveToIOSPhotos(filePath);
            
            DebugViewController.AddDebugMessage(" Image saved to Photos app");
#else
            DebugViewController.AddDebugMessage("Download only available on iOS device");
#endif

            isDownloadPending = false;
            pendingFilePath = null;

            // Notify gallery that download completed
            if (GalleryViewController.Instance != null)
            {
                GalleryViewController.Instance.OnDownloadComplete(true, filePath);
            }
        }
        catch (Exception ex)
        {
            DebugViewController.AddDebugMessage($"ERROR: Save failed: {ex.Message}");
            isDownloadPending = false;
            pendingFilePath = null;

            if (GalleryViewController.Instance != null)
            {
                GalleryViewController.Instance.OnDownloadComplete(false, filePath);
            }
        }
    }
}
