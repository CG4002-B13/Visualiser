using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SettingsPanelController : MonoBehaviour
{
    [Header("Tab Buttons")]
    [SerializeField] private Button settingsTabButton;
    [SerializeField] private Button debugTabButton;
    [SerializeField] private Button galleryTabButton;

    [Header("Tab Content Panels")]
    [SerializeField] private GameObject settingsMenu;
    [SerializeField] private GameObject debugViewer;
    [SerializeField] private GameObject galleryViewer;

    [Header("Close Button")]
    [SerializeField] private Button closeSettingsPanelButton;

    [Header("Individual Page Controllers")]
    [SerializeField] private SettingsMenuController settingsMenuController;
    [SerializeField] private DebugViewController debugViewController;
    [SerializeField] private GalleryViewController galleryViewController;

    private void Start()
    {
        // Setup tab button listeners
        if (settingsTabButton != null)
        {
            settingsTabButton.onClick.AddListener(ShowSettingsTab);
        }
        if (debugTabButton != null)
        {
            debugTabButton.onClick.AddListener(ShowDebugTab);
        }
        if (galleryTabButton != null)
        {
            galleryTabButton.onClick.AddListener(ShowGalleryTab);
        }

        // Setup close button listener
        if (closeSettingsPanelButton != null)
        {
            closeSettingsPanelButton.onClick.AddListener(CloseSettingsPanel);
        }
    }

    // ===== TAB SWITCHING =====

    public void ShowSettingsTab()
    {
        SetTabActive(settingsMenu, true);
        SetTabActive(debugViewer, false);
        SetTabActive(galleryViewer, false);

        // Initialize the settings menu page if needed
        if (settingsMenuController != null)
        {
            settingsMenuController.OnTabOpened();
        }

        Debug.Log("Settings Tab Activated");
    }

    public void ShowDebugTab()
    {
        SetTabActive(settingsMenu, false);
        SetTabActive(debugViewer, true);
        SetTabActive(galleryViewer, false);

        // Initialize the debug viewer page if needed
        if (debugViewController != null)
        {
            debugViewController.OnTabOpened();
        }

        Debug.Log("Debug Tab Activated");
    }

    public void ShowGalleryTab()
    {
        SetTabActive(settingsMenu, false);
        SetTabActive(debugViewer, false);
        SetTabActive(galleryViewer, true);

        // Initialize the gallery viewer page if needed
        if (galleryViewController != null)
        {
            galleryViewController.OnTabOpened();
        }

        Debug.Log("Gallery Tab Activated");
    }

    private void SetTabActive(GameObject tab, bool active)
    {
        if (tab != null)
        {
            tab.SetActive(active);
        }
    }

    public void CloseSettingsPanel()
    {
        if (StageManager.Instance != null)
        {
            StageManager.Instance.CloseSettings();
        }
        Debug.Log("Closing Settings Panel");
    }
}
