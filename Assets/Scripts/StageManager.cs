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
    [SerializeField] private GameObject deleteButton;

    [Header("Object Detection UI")]
    [SerializeField] private GameObject boundingBoxOverlay;

    [Header("Common UI (visible in ObjectPlacement and ObjectDetection)")]
    [SerializeField] private GameObject topPane;
    [SerializeField] private GameObject objectDetectionButton;
    [SerializeField] private GameObject screenshotButton;
    [SerializeField] private GameObject connectionButton;
    [SerializeField] private GameObject xrModeToggleButton;
    [SerializeField] private GameObject settingsGearButton;

    [Header("Settings Panel")]
    [SerializeField] private GameObject settingsPanel;

    [Header("Managers")]
    [SerializeField] private ObjectDetectionSample objectDetectionSample;
    [SerializeField] private ObjectManager objectManager;
    [SerializeField] private SettingsPanelController settingsPanelController;

    // CanvasGroups for control visibility (alpha-based hiding)
    private CanvasGroup axialJoystickCanvasGroup;
    private CanvasGroup rotaryJoystickCanvasGroup;
    private CanvasGroup yawLeftButtonCanvasGroup;
    private CanvasGroup yawRightButtonCanvasGroup;
    private CanvasGroup zIncreaseButtonCanvasGroup;
    private CanvasGroup zDecreaseButtonCanvasGroup;

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
        // Cache CanvasGroup references
        CacheCanvasGroups();

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

        // Setup button listener for Connection Button
        if (connectionButton != null)
        {
            Button connButton = connectionButton.GetComponent<Button>();
            if (connButton != null)
            {
                connButton.onClick.AddListener(OnConnectionButtonClicked);
            }
        }

        // Start in Object Placement stage
        SetStage(Stage.ObjectPlacement);
    }

    private void CacheCanvasGroups()
    {
        // Get or add CanvasGroup components to all control GameObjects
        if (axialJoystick != null)
        {
            axialJoystickCanvasGroup = axialJoystick.GetComponent<CanvasGroup>();
            if (axialJoystickCanvasGroup == null)
            {
                Debug.LogWarning("StageManager: Adding CanvasGroup to axialJoystick");
                axialJoystickCanvasGroup = axialJoystick.AddComponent<CanvasGroup>();
            }
        }

        if (rotaryJoystick != null)
        {
            rotaryJoystickCanvasGroup = rotaryJoystick.GetComponent<CanvasGroup>();
            if (rotaryJoystickCanvasGroup == null)
            {
                Debug.LogWarning("StageManager: Adding CanvasGroup to rotaryJoystick");
                rotaryJoystickCanvasGroup = rotaryJoystick.AddComponent<CanvasGroup>();
            }
        }

        if (yawLeftButton != null)
        {
            yawLeftButtonCanvasGroup = yawLeftButton.GetComponent<CanvasGroup>();
            if (yawLeftButtonCanvasGroup == null)
            {
                Debug.LogWarning("StageManager: Adding CanvasGroup to yawLeftButton");
                yawLeftButtonCanvasGroup = yawLeftButton.AddComponent<CanvasGroup>();
            }
        }

        if (yawRightButton != null)
        {
            yawRightButtonCanvasGroup = yawRightButton.GetComponent<CanvasGroup>();
            if (yawRightButtonCanvasGroup == null)
            {
                Debug.LogWarning("StageManager: Adding CanvasGroup to yawRightButton");
                yawRightButtonCanvasGroup = yawRightButton.AddComponent<CanvasGroup>();
            }
        }

        if (zIncreaseButton != null)
        {
            zIncreaseButtonCanvasGroup = zIncreaseButton.GetComponent<CanvasGroup>();
            if (zIncreaseButtonCanvasGroup == null)
            {
                Debug.LogWarning("StageManager: Adding CanvasGroup to zIncreaseButton");
                zIncreaseButtonCanvasGroup = zIncreaseButton.AddComponent<CanvasGroup>();
            }
        }

        if (zDecreaseButton != null)
        {
            zDecreaseButtonCanvasGroup = zDecreaseButton.GetComponent<CanvasGroup>();
            if (zDecreaseButtonCanvasGroup == null)
            {
                Debug.LogWarning("StageManager: Adding CanvasGroup to zDecreaseButton");
                zDecreaseButtonCanvasGroup = zDecreaseButton.AddComponent<CanvasGroup>();
            }
        }
    }

    private void OnConnectionButtonClicked()
    {
        if (DebugViewController.Instance == null)
        {
            Debug.LogError("StageManager: DebugViewController.Instance is null!");
            return;
        }

        // Delegate to DebugViewController's connection management
        DebugViewController.Instance.ToggleConnection();
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
        // Enable Object Placement UI (SetActive)
        SetUIActive(axialJoystick, true);
        SetUIActive(rotaryJoystick, true);
        SetUIActive(selectionPane, true);
        SetUIActive(yawLeftButton, true);
        SetUIActive(yawRightButton, true);
        SetUIActive(zIncreaseButton, true);
        SetUIActive(zDecreaseButton, true);

        // Update control visibility based on connection status
        UpdateControlsVisibility();

        // Disable Object Detection UI
        SetUIActive(boundingBoxOverlay, false);

        // Enable Common UI
        SetUIActive(topPane, true);
        SetUIActive(objectDetectionButton, true);
        SetUIActive(screenshotButton, true);
        SetUIActive(connectionButton, true);
        SetUIActive(xrModeToggleButton, true);
        SetUIActive(settingsGearButton, true);

        // Disable Settings Panel
        SetUIActive(settingsPanel, false);

        // Stop object detection
        if (objectDetectionSample != null)
        {
            objectDetectionSample.enabled = false;
        }

        // ===== DISABLE NIANTIC TO FREE MEMORY =====
        if (NianticMemoryManager.Instance != null)
        {
            NianticMemoryManager.Instance.DisableNiantic();
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
        SetUIActive(deleteButton, false);

        // Enable Object Detection UI
        SetUIActive(boundingBoxOverlay, true);

        // Enable Common UI
        SetUIActive(topPane, true);
        SetUIActive(objectDetectionButton, true);
        SetUIActive(screenshotButton, true);
        SetUIActive(connectionButton, true);
        SetUIActive(xrModeToggleButton, true);
        SetUIActive(settingsGearButton, true);

        // Disable Settings Panel
        SetUIActive(settingsPanel, false);

        // ===== ENABLE NIANTIC FOR OBJECT DETECTION =====
        if (NianticMemoryManager.Instance != null)
        {
            NianticMemoryManager.Instance.EnableNiantic();
        }

        // Start object detection
        if (objectDetectionSample != null)
        {
            objectDetectionSample.enabled = true;
        }

        // Hide all placed objects (but keep their data)
        if (objectManager != null)
        {
            objectManager.HideAllObjects();
            objectManager.DeselectAll();
        }

        // Update UI Manager
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateCurrentStage("Object Detection");
            UIManager.Instance.ClearObjectInfo();
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
        SetUIActive(deleteButton, false);

        // Disable Object Detection UI
        SetUIActive(boundingBoxOverlay, false);

        // Disable Common UI
        SetUIActive(topPane, false);
        SetUIActive(objectDetectionButton, false);
        SetUIActive(screenshotButton, false);
        SetUIActive(connectionButton, false);
        SetUIActive(xrModeToggleButton, false);
        SetUIActive(settingsGearButton, false);

        // Enable Settings Panel
        SetUIActive(settingsPanel, true);

        // Stop object detection if it was running
        if (objectDetectionSample != null)
        {
            objectDetectionSample.enabled = false;
        }

        // ===== DISABLE NIANTIC TO FREE MEMORY =====
        if (NianticMemoryManager.Instance != null)
        {
            NianticMemoryManager.Instance.DisableNiantic();
        }

        // Deselect any selected objects before entering settings
        if (objectManager != null)
        {
            objectManager.DeselectAll();
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

    // ===== CONTROL VISIBILITY MANAGEMENT =====

    public void UpdateControlsVisibility()
    {
        if (WS_Client.Instance == null)
        {
            Debug.LogWarning("StageManager: WS_Client.Instance is null, defaulting to visible controls");
            SetControlsAlpha(1.0f); // Default to visible
            return;
        }

        bool isConnected = WS_Client.Instance.IsConnected;

        if (isConnected)
        {
            // Connected = Remote mode = Hide controls
            SetControlsAlpha(0.0f);
            DebugViewController.AddDebugMessage("StageManager: Controls hidden (remote mode)");
        }
        else
        {
            // Disconnected = Local mode = Show controls
            SetControlsAlpha(1.0f);
            DebugViewController.AddDebugMessage("StageManager: Controls visible (local mode)");
        }
    }

    private void SetControlsAlpha(float alpha)
    {
        if (axialJoystickCanvasGroup != null) axialJoystickCanvasGroup.alpha = alpha;
        if (rotaryJoystickCanvasGroup != null) rotaryJoystickCanvasGroup.alpha = alpha;
        if (yawLeftButtonCanvasGroup != null) yawLeftButtonCanvasGroup.alpha = alpha;
        if (yawRightButtonCanvasGroup != null) yawRightButtonCanvasGroup.alpha = alpha;
        if (zIncreaseButtonCanvasGroup != null) zIncreaseButtonCanvasGroup.alpha = alpha;
        if (zDecreaseButtonCanvasGroup != null) zDecreaseButtonCanvasGroup.alpha = alpha;

        Debug.Log($"StageManager: Set all control alphas to {alpha}");
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