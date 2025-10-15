using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    public enum Stage
    {
        ObjectPlacement,
        ObjectDetection,
        Settings
    }

    [Header("Current Stage")]
    [SerializeField] private Stage currentStage = Stage.ObjectPlacement;
    private Stage previousStage = Stage.ObjectPlacement;

    [Header("Object Placement UI")]
    [SerializeField] private GameObject axialJoystick;
    [SerializeField] private GameObject rotaryJoystick;
    [SerializeField] private GameObject selectionPane;
    [SerializeField] private GameObject yawLeftButton;
    [SerializeField] private GameObject yawRightButton;
    [SerializeField] private GameObject zIncreaseButton;
    [SerializeField] private GameObject zDecreaseButton;
    // REMOVED: gridOutline reference - UIManager handles this
    [SerializeField] private GameObject deleteButton;

    [Header("Object Detection UI")]
    [SerializeField] private GameObject boundingBoxOverlay;

    [Header("Common UI (visible in ObjectPlacement and ObjectDetection)")]
    [SerializeField] private GameObject topPane;
    [SerializeField] private GameObject objectDetectionButton;
    [SerializeField] private GameObject screenshotButton;
    [SerializeField] private GameObject settingsGearButton;

    [Header("Settings Panel")]
    [SerializeField] private GameObject settingsPanel;

    [Header("Managers")]
    [SerializeField] private ObjectDetectionSample objectDetectionSample;
    [SerializeField] private ObjectManager objectManager;
    [SerializeField] private SettingsPanelController settingsPanelController;

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
        // Setup button listener for Object Detection toggle
        if (objectDetectionButton != null)
        {
            Button odButton = objectDetectionButton.GetComponent<Button>();
            if (odButton != null)
            {
                odButton.onClick.AddListener(ToggleStage);
            }
        }

        // Setup button listener for Settings Gear
        if (settingsGearButton != null)
        {
            Button settingsButton = settingsGearButton.GetComponent<Button>();
            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(OpenSettings);
            }
        }

        // Start in Object Placement stage
        SetStage(Stage.ObjectPlacement);
    }

    public void ToggleStage()
    {
        if (currentStage == Stage.ObjectPlacement)
        {
            SetStage(Stage.ObjectDetection);
        }
        else if (currentStage == Stage.ObjectDetection)
        {
            SetStage(Stage.ObjectPlacement);
        }
    }

    public void OpenSettings()
    {
        // Store the current stage so we can return to it
        if (currentStage != Stage.Settings)
        {
            previousStage = currentStage;
        }
        SetStage(Stage.Settings);
    }

    public void CloseSettings()
    {
        // Return to the previous stage
        SetStage(previousStage);
    }

    public void SetStage(Stage newStage)
    {
        currentStage = newStage;

        if (currentStage == Stage.ObjectPlacement)
        {
            ActivateObjectPlacementStage();
        }
        else if (currentStage == Stage.ObjectDetection)
        {
            ActivateObjectDetectionStage();
        }
        else if (currentStage == Stage.Settings)
        {
            ActivateSettingsStage();
        }
    }

    private void ActivateObjectPlacementStage()
    {
        // Enable Object Placement UI
        SetUIActive(axialJoystick, true);
        SetUIActive(rotaryJoystick, true);
        SetUIActive(selectionPane, true);
        SetUIActive(yawLeftButton, true);
        SetUIActive(yawRightButton, true);
        SetUIActive(zIncreaseButton, true);
        SetUIActive(zDecreaseButton, true);

        // GridOutline and DeleteButton visibility are managed by UIManager based on selection
        // Don't touch them here

        // Disable Object Detection UI
        SetUIActive(boundingBoxOverlay, false);

        // Enable Common UI
        SetUIActive(topPane, true);
        SetUIActive(objectDetectionButton, true);
        SetUIActive(screenshotButton, true);
        SetUIActive(settingsGearButton, true);

        // Disable Settings Panel
        SetUIActive(settingsPanel, false);

        // Stop object detection
        if (objectDetectionSample != null)
        {
            objectDetectionSample.enabled = false;
        }

        // Show all placed objects
        if (objectManager != null)
        {
            objectManager.ShowAllObjects();
        }

        // Update UI Manager
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateCurrentStage("Object Placement");
        }

        Debug.Log("Switched to Object Placement Stage");
    }

    private void ActivateObjectDetectionStage()
    {
        // Disable Object Placement UI
        SetUIActive(axialJoystick, false);
        SetUIActive(rotaryJoystick, false);
        SetUIActive(selectionPane, false);
        SetUIActive(yawLeftButton, false);
        SetUIActive(yawRightButton, false);
        SetUIActive(zIncreaseButton, false);
        SetUIActive(zDecreaseButton, false);

        // FIXED: Don't force-hide gridOutline - let UIManager handle it
        // REMOVED: SetUIActive(gridOutline, false);

        SetUIActive(deleteButton, false);

        // Enable Object Detection UI
        SetUIActive(boundingBoxOverlay, true);

        // Enable Common UI
        SetUIActive(topPane, true);
        SetUIActive(objectDetectionButton, true);
        SetUIActive(screenshotButton, true);
        SetUIActive(settingsGearButton, true);

        // Disable Settings Panel
        SetUIActive(settingsPanel, false);

        // Start object detection
        if (objectDetectionSample != null)
        {
            objectDetectionSample.enabled = true;
        }

        // Hide all placed objects (but keep their data)
        if (objectManager != null)
        {
            objectManager.HideAllObjects();
            objectManager.DeselectAll(); // This will call UIManager to hide gridOutline
        }

        // Update UI Manager
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateCurrentStage("Object Detection");
            UIManager.Instance.ClearObjectInfo(); // This also hides the grid outline properly
        }

        Debug.Log("Switched to Object Detection Stage");
    }

    private void ActivateSettingsStage()
    {
        // Disable Object Placement UI
        SetUIActive(axialJoystick, false);
        SetUIActive(rotaryJoystick, false);
        SetUIActive(selectionPane, false);
        SetUIActive(yawLeftButton, false);
        SetUIActive(yawRightButton, false);
        SetUIActive(zIncreaseButton, false);
        SetUIActive(zDecreaseButton, false);

        // FIXED: Don't force-hide gridOutline - let UIManager handle it
        // REMOVED: SetUIActive(gridOutline, false);

        SetUIActive(deleteButton, false);

        // Disable Object Detection UI
        SetUIActive(boundingBoxOverlay, false);

        // Disable Common UI
        SetUIActive(topPane, false);
        SetUIActive(objectDetectionButton, false);
        SetUIActive(screenshotButton, false);
        SetUIActive(settingsGearButton, false);

        // Enable Settings Panel
        SetUIActive(settingsPanel, true);

        // Stop object detection if it was running
        if (objectDetectionSample != null)
        {
            objectDetectionSample.enabled = false;
        }

        // Deselect any selected objects before entering settings
        if (objectManager != null)
        {
            objectManager.DeselectAll(); // This will hide gridOutline via UIManager
        }

        // Initialize settings panel to show default tab
        if (settingsPanelController != null)
        {
            settingsPanelController.ShowSettingsTab();
        }

        // Update UI Manager
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateCurrentStage("Settings");
        }

        Debug.Log("Switched to Settings Stage");
    }

    private void SetUIActive(GameObject uiElement, bool active)
    {
        if (uiElement != null)
        {
            uiElement.SetActive(active);
        }
    }

    public Stage GetCurrentStage()
    {
        return currentStage;
    }

    public bool IsObjectPlacementStage()
    {
        return currentStage == Stage.ObjectPlacement;
    }

    public bool IsObjectDetectionStage()
    {
        return currentStage == Stage.ObjectDetection;
    }

    public bool IsSettingsStage()
    {
        return currentStage == Stage.Settings;
    }
}