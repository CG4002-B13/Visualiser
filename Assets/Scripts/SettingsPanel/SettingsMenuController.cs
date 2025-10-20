using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsMenuController : MonoBehaviour
{
    [Header("User Connection Settings")]
    [SerializeField] private TMP_InputField userIdInputField;
    [SerializeField] private TMP_InputField sessionIdInputField;

    [Header("Discrete Packet Debugging")]
    [SerializeField] private Button discretePacketToggleButton;
    [SerializeField] private TextMeshProUGUI discretePacketButtonText;

    [Header("Controls Sensitivity")]
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private TextMeshProUGUI sensitivityValueText;

    // Global state variables (apply to ALL objects)
    private bool isDiscreteMode = false;  // Default: OFF (Streaming Mode)
    private float currentSensitivity = 1.0f;  // Default: 1.0 (standard)

    // Saved keys for PlayerPrefs
    private const string PREF_USERID = "Settings_UserID";
    private const string PREF_SESSIONID = "Settings_SessionID";
    private const string PREF_DISCRETE_MODE = "Settings_DiscreteMode";
    private const string PREF_SENSITIVITY = "Settings_Sensitivity";

    private void Start()
    {
        // CRITICAL LOG: Settings initialization
        DebugViewController.AddDebugMessage("=== SETTINGS MENU INITIALIZED ===");

        // Load saved settings
        LoadSettings();

        // Setup button listener for Discrete Packet toggle
        if (discretePacketToggleButton != null)
        {
            discretePacketToggleButton.onClick.AddListener(ToggleDiscretePacketMode);
        }
        else
        {
            Debug.LogError("SettingsMenuController: discretePacketToggleButton is not assigned!");
        }

        // Setup slider listener for Controls Sensitivity
        if (sensitivitySlider != null)
        {
            sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        }
        else
        {
            Debug.LogError("SettingsMenuController: sensitivitySlider is not assigned!");
        }

        // Setup input field listeners
        if (userIdInputField != null)
        {
            userIdInputField.onEndEdit.AddListener(OnUserIdChanged);
        }
        if (sessionIdInputField != null)
        {
            sessionIdInputField.onEndEdit.AddListener(OnSessionIdChanged);
        }

        // Initialize UI to match current state
        UpdateDiscretePacketUI();
        UpdateSensitivityUI();

        // Log current configuration
        LogCurrentConfiguration();
    }

    public void OnTabOpened()
    {
        // Called when Settings tab is opened
        DebugViewController.AddDebugMessage("--- Settings Tab Opened ---");

        // Refresh UI to show current state
        UpdateDiscretePacketUI();
        UpdateSensitivityUI();

        // Show current config
        LogCurrentConfiguration();
    }

    // ===== DISCRETE PACKET DEBUGGING TOGGLE =====

    private void ToggleDiscretePacketMode()
    {
        // Toggle state
        isDiscreteMode = !isDiscreteMode;

        // CRITICAL LOG: Mode toggle
        DebugViewController.AddDebugMessage("=====================================");
        DebugViewController.AddDebugMessage($"MODE TOGGLE: {(isDiscreteMode ? "Streaming → Discrete" : "Discrete → Streaming")}");
        DebugViewController.AddDebugMessage("=====================================");

        // Update UI
        UpdateDiscretePacketUI();

        // Apply mode change to ALL objects (not just selected one)
        ApplyGlobalDataFlowMode();

        // Save preference
        PlayerPrefs.SetInt(PREF_DISCRETE_MODE, isDiscreteMode ? 1 : 0);
        PlayerPrefs.Save();

        DebugViewController.AddDebugMessage($"Settings saved: DiscreteMode={isDiscreteMode}");

        // Log new configuration
        LogCurrentConfiguration();
    }

    private void UpdateDiscretePacketUI()
    {
        if (discretePacketButtonText != null)
        {
            discretePacketButtonText.text = isDiscreteMode ? "ON" : "OFF";
        }

        // Change button color based on state
        if (discretePacketToggleButton != null)
        {
            ColorBlock colors = discretePacketToggleButton.colors;
            if (isDiscreteMode)
            {
                // ON state - green tint
                colors.normalColor = new Color(0.7f, 1.0f, 0.7f);
            }
            else
            {
                // OFF state - white/default
                colors.normalColor = new Color(1.0f, 1.0f, 1.0f);
            }
            discretePacketToggleButton.colors = colors;
        }
    }

    /// <summary>
    /// Apply the current global data flow mode to ALL instantiated objects
    /// This ensures consistent behavior across all furniture
    /// </summary>
    private void ApplyGlobalDataFlowMode()
    {
        if (ObjectManager.Instance == null)
        {
            DebugViewController.AddDebugMessage("ObjectManager not found - mode will apply when objects created");
            return;
        }

        VirtualJoystickState.DataFlowMode mode = isDiscreteMode
            ? VirtualJoystickState.DataFlowMode.Discrete
            : VirtualJoystickState.DataFlowMode.Streaming;

        // Get all instantiated objects
        Dictionary<string, GameObject> allObjects = ObjectManager.Instance.GetInstantiatedObjects();

        if (allObjects.Count == 0)
        {
            DebugViewController.AddDebugMessage($"No objects instantiated yet");
            DebugViewController.AddDebugMessage($"Mode will apply to new objects: {mode}");
            return;
        }

        // Apply mode to ALL objects
        int successCount = 0;
        foreach (var kvp in allObjects)
        {
            GameObject obj = kvp.Value;
            if (obj != null)
            {
                ControllableObject controllable = obj.GetComponent<ControllableObject>();
                if (controllable != null)
                {
                    VirtualJoystickState virtualState = controllable.GetVirtualState();
                    if (virtualState != null)
                    {
                        virtualState.SetDataFlowMode(mode);
                        successCount++;
                    }
                }
            }
        }

        // CRITICAL LOG: Mode applied to objects
        DebugViewController.AddDebugMessage($"✓ Mode {mode} applied to {successCount}/{allObjects.Count} objects");

        if (successCount < allObjects.Count)
        {
            DebugViewController.AddDebugMessage($"Warning: {allObjects.Count - successCount} objects failed to update");
        }
    }

    // ===== CONTROLS SENSITIVITY SLIDER =====

    private void OnSensitivityChanged(float value)
    {
        float oldSensitivity = currentSensitivity;
        currentSensitivity = value;

        // Update UI text
        UpdateSensitivityUI();

        // Apply sensitivity to ALL objects
        ApplyGlobalSensitivity();

        // Save preference
        PlayerPrefs.SetFloat(PREF_SENSITIVITY, currentSensitivity);
        PlayerPrefs.Save();

        // DETAILED LOG: Sensitivity change
        if (Mathf.Abs(oldSensitivity - currentSensitivity) > 0.01f)
        {
            DebugViewController.AddDebugMessage($"Sensitivity: {oldSensitivity:F2}x → {currentSensitivity:F2}x");

            if (isDiscreteMode)
            {
                DebugViewController.AddDebugMessage("Note: Sensitivity ignored in Discrete Mode");
            }
            else
            {
                DebugViewController.AddDebugMessage($"Effective Accel Scale: ~{0.3f * currentSensitivity:F3}");
                DebugViewController.AddDebugMessage($"Effective Gyro Scale: ~{0.8f * currentSensitivity:F3}");
            }
        }
    }

    private void UpdateSensitivityUI()
    {
        if (sensitivityValueText != null)
        {
            sensitivityValueText.text = $"Controls Sensitivity ({currentSensitivity:F3})";
        }
    }

    /// <summary>
    /// Apply sensitivity multiplier to ALL instantiated objects
    /// </summary>
    private void ApplyGlobalSensitivity()
    {
        if (ObjectManager.Instance == null) return;

        Dictionary<string, GameObject> allObjects = ObjectManager.Instance.GetInstantiatedObjects();

        if (allObjects.Count == 0) return;

        // Apply sensitivity to ALL objects
        int successCount = 0;
        foreach (var kvp in allObjects)
        {
            GameObject obj = kvp.Value;
            if (obj != null)
            {
                ControllableObject controllable = obj.GetComponent<ControllableObject>();
                if (controllable != null)
                {
                    VirtualJoystickState virtualState = controllable.GetVirtualState();
                    if (virtualState != null)
                    {
                        virtualState.SetSensitivityMultiplier(currentSensitivity);
                        successCount++;
                    }
                }
            }
        }

        // Only log if there were objects to update
        if (successCount > 0)
        {
            DebugViewController.AddDebugMessage($"Sensitivity {currentSensitivity:F2}x applied to {successCount} objects");
        }
    }

    // ===== USER ID AND SESSION ID =====

    private void OnUserIdChanged(string newUserId)
    {
        DebugViewController.AddDebugMessage($"UserID changed: '{newUserId}'");
        PlayerPrefs.SetString(PREF_USERID, newUserId);
        PlayerPrefs.Save();

        // TODO: Future - update WebSocket connection with new UserID
    }

    private void OnSessionIdChanged(string newSessionId)
    {
        DebugViewController.AddDebugMessage($"SessionID changed: '{newSessionId}'");
        PlayerPrefs.SetString(PREF_SESSIONID, newSessionId);
        PlayerPrefs.Save();

        // TODO: Future - update WebSocket connection with new SessionID
    }

    // ===== SETTINGS PERSISTENCE =====

    private void LoadSettings()
    {
        // Load UserID
        if (PlayerPrefs.HasKey(PREF_USERID))
        {
            string savedUserId = PlayerPrefs.GetString(PREF_USERID);
            if (userIdInputField != null)
            {
                userIdInputField.text = savedUserId;
            }
            DebugViewController.AddDebugMessage($"Loaded UserID: '{savedUserId}'");
        }

        // Load SessionID
        if (PlayerPrefs.HasKey(PREF_SESSIONID))
        {
            string savedSessionId = PlayerPrefs.GetString(PREF_SESSIONID);
            if (sessionIdInputField != null)
            {
                sessionIdInputField.text = savedSessionId;
            }
            DebugViewController.AddDebugMessage($"Loaded SessionID: '{savedSessionId}'");
        }

        // Load Discrete Mode state
        if (PlayerPrefs.HasKey(PREF_DISCRETE_MODE))
        {
            isDiscreteMode = PlayerPrefs.GetInt(PREF_DISCRETE_MODE) == 1;
            DebugViewController.AddDebugMessage($"Loaded DiscreteMode: {isDiscreteMode}");
        }
        else
        {
            isDiscreteMode = false;  // Default: Streaming Mode (OFF)
            DebugViewController.AddDebugMessage("DiscreteMode: DEFAULT (Streaming)");
        }

        // Load Sensitivity
        if (PlayerPrefs.HasKey(PREF_SENSITIVITY))
        {
            currentSensitivity = PlayerPrefs.GetFloat(PREF_SENSITIVITY);
            DebugViewController.AddDebugMessage($"Loaded Sensitivity: {currentSensitivity:F2}x");
        }
        else
        {
            currentSensitivity = 1.0f;  // Default: 1.0
            DebugViewController.AddDebugMessage("Sensitivity: DEFAULT (1.0x)");
        }

        // Apply loaded sensitivity to slider
        if (sensitivitySlider != null)
        {
            sensitivitySlider.value = currentSensitivity;
        }
    }

    // ===== LOGGING UTILITIES =====

    /// <summary>
    /// Log current configuration to debug panel
    /// </summary>
    private void LogCurrentConfiguration()
    {
        DebugViewController.AddDebugMessage("--- Current Settings ---");
        DebugViewController.AddDebugMessage($"Data Mode: {(isDiscreteMode ? "Discrete Packets" : "Streaming (30Hz)")}");
        DebugViewController.AddDebugMessage($"Sensitivity: {currentSensitivity:F2}x {(isDiscreteMode ? "(ignored in Discrete)" : "(active)")}");

        string userId = userIdInputField != null ? userIdInputField.text : "";
        string sessionId = sessionIdInputField != null ? sessionIdInputField.text : "";

        if (!string.IsNullOrEmpty(userId))
        {
            DebugViewController.AddDebugMessage($"UserID: {userId}");
        }
        if (!string.IsNullOrEmpty(sessionId))
        {
            DebugViewController.AddDebugMessage($"SessionID: {sessionId}");
        }

        // Count objects
        if (ObjectManager.Instance != null)
        {
            int objectCount = ObjectManager.Instance.GetInstantiatedObjects().Count;
            DebugViewController.AddDebugMessage($"Objects in scene: {objectCount}");
        }
    }

    // ===== PUBLIC METHODS (for other scripts to query/modify settings) =====

    public bool IsDiscreteMode()
    {
        return isDiscreteMode;
    }

    public float GetSensitivity()
    {
        return currentSensitivity;
    }

    public string GetUserId()
    {
        return userIdInputField != null ? userIdInputField.text : "";
    }

    public string GetSessionId()
    {
        return sessionIdInputField != null ? sessionIdInputField.text : "";
    }

    /// <summary>
    /// Called by ObjectManager when a new object is selected or instantiated
    /// Applies current global settings to the object
    /// </summary>
    public void ApplySettingsToObject(ControllableObject controllable)
    {
        if (controllable == null) return;

        VirtualJoystickState virtualState = controllable.GetVirtualState();
        if (virtualState == null) return;

        // Apply current global mode
        VirtualJoystickState.DataFlowMode mode = isDiscreteMode
            ? VirtualJoystickState.DataFlowMode.Discrete
            : VirtualJoystickState.DataFlowMode.Streaming;

        virtualState.SetDataFlowMode(mode);
        virtualState.SetSensitivityMultiplier(currentSensitivity);

        // CRITICAL LOG: Settings applied to new object
        DebugViewController.AddDebugMessage($"✓ Settings applied to '{controllable.gameObject.name}': Mode={mode}, Sensitivity={currentSensitivity:F2}x");
    }

    // Make this accessible as a singleton for other scripts
    public static SettingsMenuController Instance { get; private set; }

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
}