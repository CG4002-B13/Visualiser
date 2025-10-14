using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GalleryViewController : MonoBehaviour
{
    [Header("Gallery Components")]
    [SerializeField] private Transform galleryContentTransform; // Content area for gallery items
    [SerializeField] private GameObject galleryItemPrefab; // Prefab for each screenshot item (future)

    // Add more gallery-related components here as needed

    private void Start()
    {
        // Initialize gallery here
        Debug.Log("Gallery View Controller Initialized");
    }

    public void OnTabOpened()
    {
        // Called when this tab is opened
        // Refresh or load gallery items here
        Debug.Log("Gallery Viewer Opened");

        // Future: Load and display screenshots
    }

    // Future: Methods to add/remove screenshots from gallery
}