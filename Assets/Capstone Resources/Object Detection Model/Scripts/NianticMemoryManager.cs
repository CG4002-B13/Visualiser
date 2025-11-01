using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class NianticMemoryManager : MonoBehaviour
{
    public static NianticMemoryManager Instance { get; private set; }

    private ARSession arSession;
    private ARCameraManager arCameraManager;
    private bool isNianticActive = false;

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

        arSession = GetComponent<ARSession>();
        arCameraManager = GetComponent<ARCameraManager>();

        // Start with Niantic DISABLED to save memory
        DisableNiantic();
    }

    /// <summary>
    /// Enable Niantic Lightship subsystems (for object detection)
    /// Call this when starting object detection
    /// </summary>
    public void EnableNiantic()
    {
        if (isNianticActive)
        {
            Debug.Log("Niantic already active");
            return;
        }

        Debug.Log("=== Enabling Niantic Lightship ===");
        DebugViewController.AddDebugMessage("Initializing object detection...");

        if (arSession != null)
        {
            arSession.enabled = true;
        }

        if (arCameraManager != null)
        {
            arCameraManager.enabled = true;
        }

        isNianticActive = true;
        Debug.Log("Niantic Lightship enabled for object detection");
    }

    /// <summary>
    /// Disable Niantic Lightship subsystems (save memory)
    /// Call this when object detection stops
    /// </summary>
    public void DisableNiantic()
    {
        if (!isNianticActive)
        {
            Debug.Log("Niantic already disabled");
            return;
        }

        Debug.Log("=== Disabling Niantic Lightship ===");
        DebugViewController.AddDebugMessage("Stopping object detection...");

        if (arSession != null)
        {
            arSession.enabled = false;
        }

        if (arCameraManager != null)
        {
            arCameraManager.enabled = false;
        }

        isNianticActive = false;

        // Force garbage collection after disabling heavy subsystem
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();

        Debug.Log("Niantic Lightship disabled - memory freed");
    }

    public bool IsNianticActive => isNianticActive;
}
