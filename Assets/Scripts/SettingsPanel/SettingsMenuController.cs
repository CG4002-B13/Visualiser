using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SettingsMenuController : MonoBehaviour
{
    [Header("Future: XR Mode Toggle")]
    [SerializeField] private Toggle xrModeToggle;

    // Add more settings controls here as needed

    private void Start()
    {
        // Initialize settings controls here
        if (xrModeToggle != null)
        {
            // Setup toggle listener when you implement XR mode
            // xrModeToggle.onValueChanged.AddListener(OnXRModeToggled);
        }
    }

    public void OnTabOpened()
    {
        // Called when this tab is opened
        // Refresh or initialize any settings values here
        Debug.Log("Settings Menu Opened");
    }

    // Future implementation
    private void OnXRModeToggled(bool isOn)
    {
        Debug.Log($"XR Mode toggled: {isOn}");
        // Implement XR mode switching logic here
    }
}
