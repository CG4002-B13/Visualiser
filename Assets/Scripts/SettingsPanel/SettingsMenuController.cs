using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsMenuController : MonoBehaviour
{
    [Header("User Connection Settings")]
    [SerializeField] private TMP_InputField usernameInputField;

    [Header("Show Controls Indicator")]
    [SerializeField] private Button showControlsButton;
    [SerializeField] private TextMeshProUGUI showControlsButtonText;

    [Header("Discrete Packet Debugging")]
    [SerializeField] private Button discretePacketToggleButton;
    [SerializeField] private TextMeshProUGUI discretePacketButtonText;

    [Header("Controls Sensitivity")]
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private TextMeshProUGUI sensitivityValueText;

    // Global state variables
    private bool isDiscreteMode = false;
    private int currentSliderValue = 10;
    private bool hideControls = false; // true = controls hidden (remote mode), false = controls visible (local mode)

    // Saved keys for PlayerPrefs
    private const string PREF_USERNAME = "Settings_Username";
    private const string PREF_DISCRETE_MODE = "Settings_DiscreteMode";
    private const string PREF_SENSITIVITY = "Settings_Sensitivity";

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

    private void Start()
    {
        DebugViewController.AddDebugMessage("=== SETTINGS MENU INITIALIZED ===");

        LoadSettings();

        if (discretePacketToggleButton != null)
        {
            discretePacketToggleButton.onClick.AddListener(ToggleDiscretePacketMode);
        }
        else
        {
            Debug.LogError("SettingsMenuController: discretePacketToggleButton is not assigned!");
        }

        if (sensitivitySlider != null)
        {
            sensitivitySlider.onValueChanged.AddListener(OnSensitivitySliderChanged);
        }
        else
        {
            Debug.LogError("SettingsMenuController: sensitivitySlider is not assigned!");
        }

        if (usernameInputField != null)
        {
            usernameInputField.onEndEdit.AddListener(OnUsernameChanged);
        }

        // Show Controls button - no onClick listener (read-only indicator)
        if (showControlsButton == null)
        {
            Debug.LogError("SettingsMenuController: showControlsButton is not assigned!");
        }

        UpdateDiscretePacketUI();
        UpdateSensitivityUI();
        UpdateShowControlsUI();
        LogCurrentConfiguration();
    }

    public void OnTabOpened()
    {
        DebugViewController.AddDebugMessage("--- Settings Tab Opened ---");
        UpdateDiscretePacketUI();
        UpdateSensitivityUI();
        UpdateShowControlsUI();
        LogCurrentConfiguration();
    }

    // ===== SHOW CONTROLS INDICATOR =====

    public void UpdateShowControlsState(bool isConnected)
    {
        hideControls = isConnected; // true = connected = controls hidden
        UpdateShowControlsUI();

        string statusMsg = hideControls ? "Controls hidden (remote mode)" : "Controls visible (local mode)";
        DebugViewController.AddDebugMessage($"Show Controls state: {statusMsg}");
    }

    private void UpdateShowControlsUI()
    {
        if (showControlsButtonText != null)
        {
            showControlsButtonText.text = hideControls ? "ON" : "OFF";
        }

        if (showControlsButton != null)
        {
            ColorBlock colors = showControlsButton.colors;
            if (hideControls)
            {
                // ON = Controls hidden (remote mode)
                colors.normalColor = new Color(0.7f, 1.0f, 0.7f); // Light green
            }
            else
            {
                // OFF = Controls visible (local mode)
                colors.normalColor = new Color(1.0f, 1.0f, 1.0f); // White
            }
            showControlsButton.colors = colors;
        }
    }

    // ===== DISCRETE PACKET DEBUGGING TOGGLE =====

    private void ToggleDiscretePacketMode()
    {
        isDiscreteMode = !isDiscreteMode;

        DebugViewController.AddDebugMessage("=====================================");
        DebugViewController.AddDebugMessage($"MODE TOGGLE: {(isDiscreteMode ? "Streaming → Discrete" : "Discrete → Streaming")}");
        DebugViewController.AddDebugMessage("=====================================");

        UpdateDiscretePacketUI();
        ApplyGlobalDataFlowMode();

        PlayerPrefs.SetInt(PREF_DISCRETE_MODE, isDiscreteMode ? 1 : 0);
        PlayerPrefs.Save();

        DebugViewController.AddDebugMessage($"Settings saved: DiscreteMode={isDiscreteMode}");
        LogCurrentConfiguration();
    }

    private void UpdateDiscretePacketUI()
    {
        if (discretePacketButtonText != null)
        {
            discretePacketButtonText.text = isDiscreteMode ? "ON" : "OFF";
        }

        if (discretePacketToggleButton != null)
        {
            ColorBlock colors = discretePacketToggleButton.colors;
            if (isDiscreteMode)
            {
                colors.normalColor = new Color(0.7f, 1.0f, 0.7f);
            }
            else
            {
                colors.normalColor = new Color(1.0f, 1.0f, 1.0f);
            }
            discretePacketToggleButton.colors = colors;
        }
    }

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

        Dictionary<string, GameObject> allObjects = ObjectManager.Instance.GetInstantiatedObjects();

        if (allObjects.Count == 0)
        {
            DebugViewController.AddDebugMessage($"No objects instantiated yet");
            DebugViewController.AddDebugMessage($"Mode will apply to new objects: {mode}");
            return;
        }

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

        DebugViewController.AddDebugMessage($" ===Mode {mode} applied to {successCount}/{allObjects.Count} objects=== ");

        if (successCount < allObjects.Count)
        {
            DebugViewController.AddDebugMessage($"Warning: {allObjects.Count - successCount} objects failed to update");
        }
    }

    // ===== CONTROLS SENSITIVITY SLIDER =====

    private void OnSensitivitySliderChanged(float sliderValue)
    {
        int newSliderValue = Mathf.RoundToInt(sliderValue);

        if (newSliderValue == currentSliderValue) return;

        int oldSliderValue = currentSliderValue;
        currentSliderValue = newSliderValue;

        UpdateSensitivityUI();
        ApplyGlobalSensitivity();

        PlayerPrefs.SetInt(PREF_SENSITIVITY, currentSliderValue);
        PlayerPrefs.Save();

        float oldMultiplier = oldSliderValue / 10.0f;
        float newMultiplier = currentSliderValue / 10.0f;

        DebugViewController.AddDebugMessage($"Sensitivity: {oldSliderValue} ({oldMultiplier:F1}x) → {currentSliderValue} ({newMultiplier:F1}x)");

        if (isDiscreteMode)
        {
            DebugViewController.AddDebugMessage("Note: Sensitivity ignored in Discrete Mode");
        }
    }

    private void UpdateSensitivityUI()
    {
        if (sensitivityValueText != null)
        {
            float actualMultiplier = currentSliderValue / 10.0f;
            sensitivityValueText.text = $"Sensitivity: {currentSliderValue}  ({actualMultiplier:F1}x)";
        }
    }

    private void ApplyGlobalSensitivity()
    {
        if (ObjectManager.Instance == null) return;

        float actualSensitivity = currentSliderValue / 10.0f;
        Dictionary<string, GameObject> allObjects = ObjectManager.Instance.GetInstantiatedObjects();

        if (allObjects.Count == 0) return;

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
                        virtualState.SetSensitivityMultiplier(actualSensitivity);
                        successCount++;
                    }
                }
            }
        }

        if (successCount > 0)
        {
            DebugViewController.AddDebugMessage($"Sensitivity {actualSensitivity:F1}x applied to {successCount} objects");
        }
    }

    // ===== COMMAND-BASED SENSITIVITY ADJUSTMENT =====

    public void AdjustSensitivity(string direction)
    {
        int oldValue = currentSliderValue;
        float oldMultiplier = oldValue / 10.0f;

        if (direction.ToLower() == "up")
        {
            if (currentSliderValue >= 20)
            {
                return;
            }
            currentSliderValue++;
        }
        else if (direction.ToLower() == "down")
        {
            if (currentSliderValue <= 1)
            {
                return;
            }
            currentSliderValue--;
        }
        else
        {
            Debug.LogWarning($"Unknown sensitivity direction: {direction}");
            return;
        }

        float newMultiplier = currentSliderValue / 10.0f;

        if (sensitivitySlider != null)
        {
            sensitivitySlider.value = currentSliderValue;
        }

        UpdateSensitivityUI();
        ApplyGlobalSensitivity();

        PlayerPrefs.SetInt(PREF_SENSITIVITY, currentSliderValue);
        PlayerPrefs.Save();

        DebugViewController.AddDebugMessage($"[COMMAND] Sensitivity {direction.ToUpper()}: {oldValue} → {currentSliderValue} ({oldMultiplier:F1}x → {newMultiplier:F1}x)");
    }

    // ===== USERNAME =====

    private void OnUsernameChanged(string newUsername)
    {
        DebugViewController.AddDebugMessage($"Username changed: '{newUsername}'");
        PlayerPrefs.SetString(PREF_USERNAME, newUsername);
        PlayerPrefs.Save();
    }

    // ===== SETTINGS PERSISTENCE =====

    private void LoadSettings()
    {
        if (PlayerPrefs.HasKey(PREF_USERNAME))
        {
            string savedUsername = PlayerPrefs.GetString(PREF_USERNAME);
            if (usernameInputField != null)
            {
                usernameInputField.text = savedUsername;
            }
            DebugViewController.AddDebugMessage($"Loaded Username: '{savedUsername}'");
        }

        if (PlayerPrefs.HasKey(PREF_DISCRETE_MODE))
        {
            isDiscreteMode = PlayerPrefs.GetInt(PREF_DISCRETE_MODE) == 1;
            DebugViewController.AddDebugMessage($"Loaded DiscreteMode: {isDiscreteMode}");
        }
        else
        {
            isDiscreteMode = false;
            DebugViewController.AddDebugMessage("DiscreteMode: DEFAULT (Streaming)");
        }

        if (PlayerPrefs.HasKey(PREF_SENSITIVITY))
        {
            currentSliderValue = PlayerPrefs.GetInt(PREF_SENSITIVITY);
            currentSliderValue = Mathf.Clamp(currentSliderValue, 1, 20);
            DebugViewController.AddDebugMessage($"Loaded Sensitivity: {currentSliderValue} ({(currentSliderValue / 10.0f):F1}x)");
        }
        else
        {
            currentSliderValue = 10;
            DebugViewController.AddDebugMessage("Sensitivity: DEFAULT (10 = 1.0x)");
        }

        if (sensitivitySlider != null)
        {
            sensitivitySlider.value = currentSliderValue;
        }
    }

    private void LogCurrentConfiguration()
    {
        DebugViewController.AddDebugMessage("--- Current Settings ---");
        DebugViewController.AddDebugMessage($"Data Mode: {(isDiscreteMode ? "Discrete Packets" : "Streaming (30Hz)")}");
        float actualMultiplier = currentSliderValue / 10.0f;
        DebugViewController.AddDebugMessage($"Sensitivity: {currentSliderValue} ({actualMultiplier:F1}x) {(isDiscreteMode ? "(ignored in Discrete)" : "(active)")}");
        DebugViewController.AddDebugMessage($"Hide Controls: {(hideControls ? "ON (hidden)" : "OFF (visible)")}");

        string username = usernameInputField != null ? usernameInputField.text : "";

        if (!string.IsNullOrEmpty(username))
        {
            DebugViewController.AddDebugMessage($"Username: {username}");
        }
        else
        {
            DebugViewController.AddDebugMessage("Username: (not set)");
        }

        if (ObjectManager.Instance != null)
        {
            int objectCount = ObjectManager.Instance.GetInstantiatedObjects().Count;
            DebugViewController.AddDebugMessage($"Objects in scene: {objectCount}");
        }
    }

    // ===== PUBLIC METHODS =====

    public bool IsDiscreteMode()
    {
        return isDiscreteMode;
    }

    public float GetSensitivity()
    {
        return currentSliderValue / 10.0f;
    }

    public string GetUsername()
    {
        return usernameInputField != null ? usernameInputField.text : "";
    }

    public void ApplySettingsToObject(ControllableObject controllable)
    {
        if (controllable == null) return;

        VirtualJoystickState virtualState = controllable.GetVirtualState();
        if (virtualState == null) return;

        VirtualJoystickState.DataFlowMode mode = isDiscreteMode
            ? VirtualJoystickState.DataFlowMode.Discrete
            : VirtualJoystickState.DataFlowMode.Streaming;

        float actualSensitivity = currentSliderValue / 10.0f;
        virtualState.SetDataFlowMode(mode);
        virtualState.SetSensitivityMultiplier(actualSensitivity);

        DebugViewController.AddDebugMessage($" *** Settings applied to '{controllable.gameObject.name}': Mode={mode}, Sensitivity={actualSensitivity:F1}x *** ");
    }
}