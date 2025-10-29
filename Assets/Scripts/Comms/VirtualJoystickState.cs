using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VirtualJoystickState : MonoBehaviour
{
    [Header("Control Mode")]
    [SerializeField] private bool isRemoteControlled = false;

    [Header("Data Flow Mode")]
    [SerializeField] private DataFlowMode dataMode = DataFlowMode.Streaming;
    public enum DataFlowMode
    {
        Streaming,  // Continuous 30Hz data with smoothing
        Discrete    // Sporadic packets with impulse decay
    }

    [Header("Streaming Mode Settings")]
    [Tooltip("How fast values interpolate to target (5-15 recommended)")]
    [SerializeField] private float smoothingSpeed = 8f;
    [Tooltip("Timeout before auto-releasing joystick (seconds)")]
    [SerializeField] private float streamingTimeout = 0.2f;

    [Header("Discrete Mode Settings")]
    [Tooltip("How long each impulse lasts (0.2-0.5s recommended)")]
    [SerializeField] private float impulseDuration = 0.3f;
    [Tooltip("How fast values return to neutral (3-10 recommended)")]
    [SerializeField] private float decaySpeed = 8f;
    [Tooltip("Scale factor for discrete packets (0.01-0.1 recommended)")]
    [SerializeField] private float discretePacketScale = 0.05f;

    [Header("Sensor Scaling (Streaming Mode)")]
    [Tooltip("Scale accelerometer data to joystick range")]
    [SerializeField] private float accelerometerScale = 0.3f;
    [Tooltip("Scale gyroscope data to joystick range")]
    [SerializeField] private float gyroscopeScale = 1.0f;  // CHANGED from 0.8

    [Header("Movement Limits")]
    [Tooltip("Maximum distance per discrete packet (Unity units)")]
    [SerializeField] private float maxDiscreteDistance = 0.15f;
    [Tooltip("Maximum speed in streaming mode (units/second)")]
    [SerializeField] private float maxStreamingSpeed = 5.0f;

    [Header("Deadzones")]
    [Tooltip("Ignore sensor values below this threshold (reduces noise)")]
    [SerializeField] private float axialDeadzone = 0.1f;
    [SerializeField] private float rotaryDeadzone = 0.1f;

    [Header("Button Thresholds")]
    [Tooltip("Accelerometer Z-axis thresholds for Z-movement buttons")]
    [SerializeField] private float zForwardThreshold = 1.5f;
    [SerializeField] private float zBackwardThreshold = -1.5f;
    [Tooltip("Gyroscope Y-axis thresholds for yaw buttons (degrees/sec)")]
    [SerializeField] private float yawLeftThreshold = 0.5f;    // CHANGED from 20
    [SerializeField] private float yawRightThreshold = -0.5f;  // CHANGED from -20

    // ===== VIRTUAL JOYSTICK STATE =====
    private float currentAxialHorizontal = 0f;
    private float currentAxialVertical = 0f;
    private float currentRotaryHorizontal = 0f;
    private float currentRotaryVertical = 0f;
    private float currentYaw = 0f;  // NEW: Continuous yaw value
    private bool currentZForward = false;
    private bool currentZBackward = false;
    private bool currentYawLeft = false;
    private bool currentYawRight = false;

    // Target values for streaming mode
    private float targetAxialHorizontal = 0f;
    private float targetAxialVertical = 0f;
    private float targetRotaryHorizontal = 0f;
    private float targetRotaryVertical = 0f;
    private float targetYaw = 0f;  // NEW: Target yaw for streaming

    // Discrete mode impulse tracking
    private float impulseTimer = 0f;
    private float impulseAxialH = 0f;
    private float impulseAxialV = 0f;
    private float impulseRotaryH = 0f;
    private float impulseRotaryV = 0f;

    // Timeout tracking for streaming mode
    private float timeSinceLastPacket = 0f;

    // NEW: Sensitivity multiplier (controlled by Settings UI)
    private float sensitivityMultiplier = 1.0f;

    // Logging control - reduce spam
    private float logThrottleTimer = 0f;
    private const float LOG_THROTTLE_INTERVAL = 1.0f; // Log state max once per second

    // ===== PUBLIC GETTERS =====

    public bool IsRemoteControlled => isRemoteControlled;
    public DataFlowMode CurrentMode => dataMode;

    public float GetAxialHorizontal() => currentAxialHorizontal;
    public float GetAxialVertical() => currentAxialVertical;
    public float GetRotaryHorizontal() => currentRotaryHorizontal;
    public float GetRotaryVertical() => currentRotaryVertical;
    public float GetYaw() => currentYaw;  // NEW: Getter for continuous yaw
    public bool GetZForward() => currentZForward;
    public bool GetZBackward() => currentZBackward;
    public bool GetYawLeft() => currentYawLeft;
    public bool GetYawRight() => currentYawRight;

    // ===== CONTROL MODE MANAGEMENT =====

    public void SetRemoteControlled(bool enabled)
    {
        isRemoteControlled = enabled;
        if (enabled)
        {
            string modeStr = dataMode == DataFlowMode.Streaming ? "Streaming (30Hz)" : "Discrete Packets";
            DebugViewController.AddDebugMessage($"[{gameObject.name}] Remote control ENABLED");
            DebugViewController.AddDebugMessage($"[{gameObject.name}] Mode: {modeStr}");
            DebugViewController.AddDebugMessage($"[{gameObject.name}] Sensitivity: {sensitivityMultiplier:F2}x");
        }
        else
        {
            DebugViewController.AddDebugMessage($"[{gameObject.name}] Remote control DISABLED → Local UI");
            ResetState();
        }
    }

    public void SetDataFlowMode(DataFlowMode mode)
    {
        DataFlowMode oldMode = dataMode;
        dataMode = mode;
        ResetState();

        // CRITICAL LOG: Mode change
        DebugViewController.AddDebugMessage($"=== MODE CHANGE ===");
        DebugViewController.AddDebugMessage($"[{gameObject.name}] {oldMode} → {mode}");

        if (mode == DataFlowMode.Streaming)
        {
            DebugViewController.AddDebugMessage($"[{gameObject.name}] Config: Smoothing={smoothingSpeed}, Timeout={streamingTimeout}s");
            DebugViewController.AddDebugMessage($"[{gameObject.name}] Scaling: Accel={accelerometerScale * sensitivityMultiplier:F3}, Gyro={gyroscopeScale * sensitivityMultiplier:F3}");
        }
        else
        {
            DebugViewController.AddDebugMessage($"[{gameObject.name}] Config: Impulse={impulseDuration}s, Decay={decaySpeed}");
            DebugViewController.AddDebugMessage($"[{gameObject.name}] Scaling: Discrete={discretePacketScale:F3}, MaxDist={maxDiscreteDistance:F2}");
        }

        Debug.Log($"VirtualJoystickState [{gameObject.name}]: Mode set to {mode}");
    }

    // NEW: Sensitivity multiplier control
    public void SetSensitivityMultiplier(float multiplier)
    {
        float oldMultiplier = sensitivityMultiplier;
        sensitivityMultiplier = Mathf.Clamp(multiplier, 0f, 3f);

        // CRITICAL LOG: Sensitivity change
        if (Mathf.Abs(oldMultiplier - sensitivityMultiplier) > 0.01f)
        {
            DebugViewController.AddDebugMessage($"[{gameObject.name}] Sensitivity: {oldMultiplier:F2}x → {sensitivityMultiplier:F2}x");

            if (dataMode == DataFlowMode.Streaming)
            {
                DebugViewController.AddDebugMessage($"[{gameObject.name}] Effective Accel Scale: {accelerometerScale * sensitivityMultiplier:F3}");
                DebugViewController.AddDebugMessage($"[{gameObject.name}] Effective Gyro Scale: {gyroscopeScale * sensitivityMultiplier:F3}");
            }
            else
            {
                DebugViewController.AddDebugMessage($"[{gameObject.name}] Note: Sensitivity ignored in Discrete Mode");
            }
        }

        Debug.Log($"VirtualJoystickState [{gameObject.name}]: Sensitivity set to {sensitivityMultiplier:F2}x");
    }

    // ===== SENSOR DATA INPUT =====

    public void SetAccelerometerData(Vector3 accelData)
    {
        if (!isRemoteControlled) return;

        // Reset timeout timer (packet received)
        timeSinceLastPacket = 0f;

        // Map X/Y to axial joystick
        float rawH = accelData.x;
        float rawV = accelData.y;

        // Apply different scaling based on mode
        if (dataMode == DataFlowMode.Streaming)
        {
            // Streaming: Standard scaling WITH sensitivity multiplier
            rawH *= accelerometerScale * sensitivityMultiplier;
            rawV *= accelerometerScale * sensitivityMultiplier;
        }
        else
        {
            // Discrete: Heavy scaling (sensitivity ignored for consistency)
            rawH *= discretePacketScale;
            rawV *= discretePacketScale;
        }

        // Apply deadzone
        rawH = ApplyDeadzone(rawH, axialDeadzone);
        rawV = ApplyDeadzone(rawV, axialDeadzone);

        // Clamp to joystick range
        rawH = Mathf.Clamp(rawH, -1f, 1f);
        rawV = Mathf.Clamp(rawV, -1f, 1f);

        // Map Z-axis to forward/backward buttons
        currentZForward = accelData.z > zForwardThreshold;
        currentZBackward = accelData.z < zBackwardThreshold;

        if (dataMode == DataFlowMode.Streaming)
        {
            // Streaming: set as target for smooth interpolation
            targetAxialHorizontal = rawH;
            targetAxialVertical = rawV;

            // DETAILED LOG: Streaming packet received
            DebugViewController.AddDebugMessage($"[STREAM] Raw:[{accelData.x:F2},{accelData.y:F2},{accelData.z:F2}] → Target:[{rawH:F2},{rawV:F2}]");
        }
        else
        {
            // Discrete: set as impulse with distance clamping
            Vector3 impulseVector = new Vector3(rawH, rawV, 0f);
            float impulseMagnitude = impulseVector.magnitude;

            // Clamp to max discrete distance
            if (impulseMagnitude > maxDiscreteDistance)
            {
                impulseVector = impulseVector.normalized * maxDiscreteDistance;
                rawH = impulseVector.x;
                rawV = impulseVector.y;

                // CRITICAL LOG: Clamping occurred
                DebugViewController.AddDebugMessage($"[DISCRETE] Impulse clamped: {impulseMagnitude:F3} → {maxDiscreteDistance:F3}");
            }

            impulseAxialH = rawH;
            impulseAxialV = rawV;
            impulseTimer = impulseDuration;

            // DETAILED LOG: Discrete packet received
            DebugViewController.AddDebugMessage($"[DISCRETE] Raw:[{accelData.x:F2},{accelData.y:F2},{accelData.z:F2}] → Impulse:[{rawH:F3},{rawV:F3}] for {impulseDuration}s");
        }
    }

    public void SetGyroscopeData(Vector3 gyroData)
    {
        if (!isRemoteControlled) return;

        // Reset timeout timer (packet received)
        timeSinceLastPacket = 0f;

        // Map gyroscope axes
        float rawPitch = gyroData.x;
        float rawYaw = gyroData.y;    // NEW: Process yaw as continuous
        float rawRoll = gyroData.z;

        // Apply different scaling based on mode
        if (dataMode == DataFlowMode.Streaming)
        {
            // Streaming: Standard scaling WITH sensitivity multiplier
            rawPitch *= gyroscopeScale * sensitivityMultiplier;
            rawYaw *= gyroscopeScale * sensitivityMultiplier;    // NEW: Scale yaw
            rawRoll *= gyroscopeScale * sensitivityMultiplier;
        }
        else
        {
            // Discrete: Heavy scaling (sensitivity ignored)
            rawPitch *= discretePacketScale;
            rawYaw *= discretePacketScale;    // NEW: Scale yaw
            rawRoll *= discretePacketScale;
        }

        // Apply deadzone
        rawPitch = ApplyDeadzone(rawPitch, rotaryDeadzone);
        rawYaw = ApplyDeadzone(rawYaw, rotaryDeadzone);    // NEW: Deadzone for yaw
        rawRoll = ApplyDeadzone(rawRoll, rotaryDeadzone);

        // Clamp to joystick range
        rawPitch = Mathf.Clamp(rawPitch, -1f, 1f);
        rawYaw = Mathf.Clamp(rawYaw, -1f, 1f);    // NEW: Clamp yaw
        rawRoll = Mathf.Clamp(rawRoll, -1f, 1f);

        // Map Y-axis (yaw) to yaw buttons (keep button functionality)
        currentYawLeft = gyroData.y > yawLeftThreshold;
        currentYawRight = gyroData.y < yawRightThreshold;

        if (dataMode == DataFlowMode.Streaming)
        {
            // Streaming: set as target
            targetRotaryVertical = rawPitch;
            targetYaw = rawYaw;    // NEW: Set target yaw
            targetRotaryHorizontal = rawRoll;

            // DETAILED LOG: Streaming rotation packet
            DebugViewController.AddDebugMessage($"[STREAM-ROT] Raw:[{gyroData.x:F2},{gyroData.y:F2},{gyroData.z:F2}] → Target:[pitch={rawPitch:F2},yaw={rawYaw:F2},roll={rawRoll:F2}]");
        }
        else
        {
            // Discrete: set as impulse
            impulseRotaryV = rawPitch;
            impulseRotaryH = rawRoll;
            impulseTimer = impulseDuration;

            // DETAILED LOG: Discrete rotation packet
            DebugViewController.AddDebugMessage($"[DISCRETE-ROT] Raw:[{gyroData.x:F2},{gyroData.y:F2},{gyroData.z:F2}] → Impulse:[pitch={rawPitch:F3},yaw={rawYaw:F3},roll={rawRoll:F3}]");
        }
    }

    // ===== UPDATE LOOP =====

    private void Update()
    {
        if (!isRemoteControlled) return;

        // Throttled state logging
        logThrottleTimer += Time.deltaTime;

        if (dataMode == DataFlowMode.Streaming)
        {
            UpdateStreamingMode();
        }
        else
        {
            UpdateDiscreteMode();
        }
    }

    private void UpdateStreamingMode()
    {
        float deltaTime = Time.deltaTime;

        // Track time since last packet
        timeSinceLastPacket += deltaTime;

        // Timeout detection - auto-release joystick
        if (timeSinceLastPacket > streamingTimeout)
        {
            // CRITICAL LOG: Timeout triggered (only log once)
            if (targetAxialHorizontal != 0f || targetAxialVertical != 0f ||
                targetRotaryHorizontal != 0f || targetRotaryVertical != 0f || targetYaw != 0f)  // ADDED targetYaw
            {
                DebugViewController.AddDebugMessage($" [{gameObject.name}] TIMEOUT: No packets for {timeSinceLastPacket:F2}s → Auto-releasing joystick");
            }

            // No packets received recently - return to neutral
            targetAxialHorizontal = 0f;
            targetAxialVertical = 0f;
            targetRotaryHorizontal = 0f;
            targetRotaryVertical = 0f;
            targetYaw = 0f;  // NEW: Reset yaw target
            currentZForward = false;
            currentZBackward = false;
            currentYawLeft = false;
            currentYawRight = false;
        }

        // Lerp toward target values
        float lerpFactor = smoothingSpeed * deltaTime;

        currentAxialHorizontal = Mathf.Lerp(currentAxialHorizontal, targetAxialHorizontal, lerpFactor);
        currentAxialVertical = Mathf.Lerp(currentAxialVertical, targetAxialVertical, lerpFactor);
        currentRotaryHorizontal = Mathf.Lerp(currentRotaryHorizontal, targetRotaryHorizontal, lerpFactor);
        currentRotaryVertical = Mathf.Lerp(currentRotaryVertical, targetRotaryVertical, lerpFactor);
        currentYaw = Mathf.Lerp(currentYaw, targetYaw, lerpFactor);  // NEW: Lerp yaw

        // Apply max speed clamp for streaming mode
        float currentSpeed = new Vector3(currentAxialHorizontal, currentAxialVertical, 0f).magnitude;
        if (currentSpeed > maxStreamingSpeed)
        {
            Vector3 clampedVector = new Vector3(currentAxialHorizontal, currentAxialVertical, 0f).normalized * maxStreamingSpeed;
            currentAxialHorizontal = clampedVector.x;
            currentAxialVertical = clampedVector.y;

            // CRITICAL LOG: Speed clamping
            if (logThrottleTimer >= LOG_THROTTLE_INTERVAL)
            {
                DebugViewController.AddDebugMessage($" [{gameObject.name}] Speed clamped: {currentSpeed:F2} → {maxStreamingSpeed:F2}");
            }
        }

        // Periodic state logging
        if (logThrottleTimer >= LOG_THROTTLE_INTERVAL)
        {
            if (currentAxialHorizontal != 0f || currentAxialVertical != 0f)
            {
                DebugViewController.AddDebugMessage($"[STATE] Axial:[{currentAxialHorizontal:F2},{currentAxialVertical:F2}] Timeout:{timeSinceLastPacket:F2}s");
            }
            logThrottleTimer = 0f;
        }
    }

    private void UpdateDiscreteMode()
    {
        float deltaTime = Time.deltaTime;

        if (impulseTimer > 0f)
        {
            // Impulse is active
            impulseTimer -= deltaTime;

            // Linear decay during impulse duration
            float t = Mathf.Max(0f, impulseTimer / impulseDuration);
            currentAxialHorizontal = impulseAxialH * t;
            currentAxialVertical = impulseAxialV * t;
            currentRotaryHorizontal = impulseRotaryH * t;
            currentRotaryVertical = impulseRotaryV * t;

            // CRITICAL LOG: Impulse ending
            if (impulseTimer <= 0f)
            {
                DebugViewController.AddDebugMessage($"[DISCRETE] Impulse completed → Starting decay");
            }
        }
        else
        {
            // Impulse finished - fast decay to zero
            float decayFactor = decaySpeed * deltaTime;
            currentAxialHorizontal = Mathf.Lerp(currentAxialHorizontal, 0f, decayFactor);
            currentAxialVertical = Mathf.Lerp(currentAxialVertical, 0f, decayFactor);
            currentRotaryHorizontal = Mathf.Lerp(currentRotaryHorizontal, 0f, decayFactor);
            currentRotaryVertical = Mathf.Lerp(currentRotaryVertical, 0f, decayFactor);

            // Auto-disable buttons when values near zero
            if (Mathf.Abs(currentAxialHorizontal) < 0.01f && Mathf.Abs(currentAxialVertical) < 0.01f)
            {
                currentZForward = false;
                currentZBackward = false;
            }
            if (Mathf.Abs(currentRotaryHorizontal) < 0.01f && Mathf.Abs(currentRotaryVertical) < 0.01f)
            {
                currentYawLeft = false;
                currentYawRight = false;
            }
        }

        // Periodic state logging for discrete mode
        if (logThrottleTimer >= LOG_THROTTLE_INTERVAL)
        {
            if (impulseTimer > 0f || Mathf.Abs(currentAxialHorizontal) > 0.01f)
            {
                DebugViewController.AddDebugMessage($"[STATE] Discrete: Axial:[{currentAxialHorizontal:F3},{currentAxialVertical:F3}] Timer:{impulseTimer:F2}s");
            }
            logThrottleTimer = 0f;
        }
    }

    // ===== UTILITY METHODS =====

    private float ApplyDeadzone(float value, float deadzone)
    {
        if (Mathf.Abs(value) < deadzone)
        {
            return 0f;
        }
        return value;
    }

    public void ResetState()
    {
        currentAxialHorizontal = 0f;
        currentAxialVertical = 0f;
        currentRotaryHorizontal = 0f;
        currentRotaryVertical = 0f;
        currentYaw = 0f;  // NEW
        targetAxialHorizontal = 0f;
        targetAxialVertical = 0f;
        targetRotaryHorizontal = 0f;
        targetRotaryVertical = 0f;
        targetYaw = 0f;  // NEW
        currentZForward = false;
        currentZBackward = false;
        currentYawLeft = false;
        currentYawRight = false;
        impulseTimer = 0f;
        impulseAxialH = 0f;
        impulseAxialV = 0f;
        impulseRotaryH = 0f;
        impulseRotaryV = 0f;
        timeSinceLastPacket = 0f;

        DebugViewController.AddDebugMessage($"[{gameObject.name}] Virtual joystick state RESET");
    }

    public string GetStateInfo()
    {
        return $"Mode: {dataMode}\n" +
               $"Axial: ({currentAxialHorizontal:F2}, {currentAxialVertical:F2})\n" +
               $"Rotary: ({currentRotaryHorizontal:F2}, {currentRotaryVertical:F2})\n" +
               $"Yaw: {currentYaw:F2}\n" +  // NEW
               $"Timeout: {timeSinceLastPacket:F2}s\n" +
               $"Sensitivity: {sensitivityMultiplier:F2}x";
    }
}