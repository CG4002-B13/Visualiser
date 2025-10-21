using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data structure to store information about a screenshot captured during the current session.
/// Used by ScreenshotManagerIOS and GalleryViewController to manage in-memory screenshots.
/// </summary>
[System.Serializable]
public class SessionScreenshotData
{
    /// <summary>
    /// The screenshot texture stored in memory for quick display
    /// </summary>
    public Texture2D texture;

    /// <summary>
    /// Full path to the PNG file saved in persistentDataPath
    /// </summary>
    public string filePath;

    /// <summary>
    /// Filename only (without path) for display purposes
    /// </summary>
    public string fileName;

    /// <summary>
    /// Timestamp when the screenshot was captured
    /// </summary>
    public DateTime timestamp;

    /// <summary>
    /// Reference to the instantiated thumbnail GameObject in the gallery grid
    /// Used for quick deletion when user removes a screenshot
    /// </summary>
    public GameObject thumbnailObject;

    /// <summary>
    /// Constructor to create a new session screenshot entry
    /// </summary>
    public SessionScreenshotData(Texture2D tex, string path, DateTime time)
    {
        texture = tex;
        filePath = path;
        fileName = System.IO.Path.GetFileName(path);
        timestamp = time;
        thumbnailObject = null; // Will be assigned when thumbnail is instantiated
    }
}
