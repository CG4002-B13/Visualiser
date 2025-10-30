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
    [SerializeField] private float gyroscopeScale = 1.0f;

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
    [Tooltip("Gyroscope Y-axis thresholds for yaw buttons")]
    [SerializeField] private float yawLeftThreshold = 0.5f;
    [SerializeField] private float yawRightThreshold = -0.5f;

    // ===== VIRTUAL JOYSTICK STATE =====
    private float currentAxialHorizontal = 0f;
    private float currentAxialVertical = 0f;
    private float currentRotaryHorizontal = 0f;
    private float currentRotaryVertical = 0f;
    private float currentYaw = 0f;
    private bool currentZForward = false;
    private bool currentZBackward = false;
    private bool currentYawLeft = false;
    private bool currentYawRight = false;

    // Target values for streaming mode
    private float targetAxialHorizontal = 0f;
    private float targetAxialVertical = 0f;
    private float targetRotaryHorizontal = 0f;
    private float targetRotaryVertical = 0f;
    private float targetYaw = 0f;

    // Discrete mode impulse tracking
    private float impulseTimer = 0f;
    private float impulseAxialH = 0f;
    private float impulseAxialV = 0f;
    private float impulseRotaryH = 0f;
    private float impulseRotaryV = 0f;

    // Timeout tracking for streaming mode
    private float timeSinceLastPacket = 0f;

    // Sensitivity multiplier (controlled by Settings UI)
    private float sensitivityMultiplier = 1.0f;

    // Logging control - reduce spam
    private float logThrottleTimer = 0f;
    private const float LOG_THROTTLE_INTERVAL = 1.0f;

    // ===== PUBLIC GETTERS =====

    public bool IsRemoteControlled => isRemoteControlled;
    public DataFlowMode CurrentMode => dataMode;

    public float GetAxialHorizontal() => currentAxialHorizontal;
    public float GetAxialVertical() => currentAxialVertical;
    public float GetRotaryHorizontal() => currentRotaryHorizontal;
    public float GetRotaryVertical() => currentRotaryVertical;
    public float GetYaw() => currentYaw;
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

    public void SetSensitivityMultiplier(float multiplier)
    {
        float oldMultiplier = sensitivityMultiplier;
        sensitivityMultiplier = Mathf.Clamp(multiplier, 0f, 3f);

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

        timeSinceLastPacket = 0f;

        float rawH = accelData.x;
        float rawV = accelData.y;

        if (dataMode == DataFlowMode.Streaming)
        {
            rawH *= accelerometerScale * sensitivityMultiplier;
            rawV *= accelerometerScale * sensitivityMultiplier;
        }
        else
        {
            rawH *= discretePacketScale;
            rawV *= discretePacketScale;
        }

        rawH = ApplyDeadzone(rawH, axialDeadzone);
        rawV = ApplyDeadzone(rawV, axialDeadzone);

        // Dynamic clamp based on sensitivity
        float dynamicClamp = GetDynamicClamp();
        rawH = Mathf.Clamp(rawH, -dynamicClamp, dynamicClamp);
        rawV = Mathf.Clamp(rawV, -dynamicClamp, dynamicClamp);

        currentZForward = accelData.z > zForwardThreshold;
        currentZBackward = accelData.z < zBackwardThreshold;

        if (dataMode == DataFlowMode.Streaming)
        {
            targetAxialHorizontal = rawH;
            targetAxialVertical = rawV;

            DebugViewController.AddDebugMessage($"[STREAM] Raw:[{accelData.x:F2},{accelData.y:F2},{accelData.z:F2}] → Target:[{rawH:F2},{rawV:F2}] Clamp:±{dynamicClamp:F2}");
        }
        else
        {
            Vector3 impulseVector = new Vector3(rawH, rawV, 0f);
            float impulseMagnitude = impulseVector.magnitude;

            if (impulseMagnitude > maxDiscreteDistance)
            {
                impulseVector = impulseVector.normalized * maxDiscreteDistance;
                rawH = impulseVector.x;
                rawV = impulseVector.y;

                DebugViewController.AddDebugMessage($"[DISCRETE] Impulse clamped: {impulseMagnitude:F3} → {maxDiscreteDistance:F3}");
            }

            impulseAxialH = rawH;
            impulseAxialV = rawV;
            impulseTimer = impulseDuration;

            DebugViewController.AddDebugMessage($"[DISCRETE] Raw:[{accelData.x:F2},{accelData.y:F2},{accelData.z:F2}] → Impulse:[{rawH:F3},{rawV:F3}] for {impulseDuration}s");
        }
    }

    public void SetGyroscopeData(Vector3 gyroData)
    {
        if (!isRemoteControlled) return;

        timeSinceLastPacket = 0f;

        float rawPitch = gyroData.x;
        float rawYaw = gyroData.y;
        float rawRoll = gyroData.z;

        if (dataMode == DataFlowMode.Streaming)
        {
            rawPitch *= gyroscopeScale * sensitivityMultiplier;
            rawYaw *= gyroscopeScale * sensitivityMultiplier;
            rawRoll *= gyroscopeScale * sensitivityMultiplier;
        }
        else
        {
            rawPitch *= discretePacketScale;
            rawYaw *= discretePacketScale;
            rawRoll *= discretePacketScale;
        }

        rawPitch = ApplyDeadzone(rawPitch, rotaryDeadzone);
        rawYaw = ApplyDeadzone(rawYaw, rotaryDeadzone);
        rawRoll = ApplyDeadzone(rawRoll, rotaryDeadzone);

        // Dynamic clamp based on sensitivity
        float dynamicClamp = GetDynamicClamp();
        rawPitch = Mathf.Clamp(rawPitch, -dynamicClamp, dynamicClamp);
        rawYaw = Mathf.Clamp(rawYaw, -dynamicClamp, dynamicClamp);
        rawRoll = Mathf.Clamp(rawRoll, -dynamicClamp, dynamicClamp);

        currentYawLeft = gyroData.y > yawLeftThreshold;
        currentYawRight = gyroData.y < yawRightThreshold;

        if (dataMode == DataFlowMode.Streaming)
        {
            targetRotaryVertical = rawPitch;
            targetYaw = rawYaw;
            targetRotaryHorizontal = rawRoll;

            DebugViewController.AddDebugMessage($"[STREAM-ROT] Raw:[{gyroData.x:F2},{gyroData.y:F2},{gyroData.z:F2}] → Target:[pitch={rawPitch:F2},yaw={rawYaw:F2},roll={rawRoll:F2}] Clamp:±{dynamicClamp:F2}");
        }
        else
        {
            impulseRotaryV = rawPitch;
            impulseRotaryH = rawRoll;
            impulseTimer = impulseDuration;

            DebugViewController.AddDebugMessage($"[DISCRETE-ROT] Raw:[{gyroData.x:F2},{gyroData.y:F2},{gyroData.z:F2}] → Impulse:[pitch={rawPitch:F3},yaw={rawYaw:F3},roll={rawRoll:F3}]");
        }
    }

    // ===== UPDATE LOOP =====

    private void Update()
    {
        if (!isRemoteControlled) return;

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

        timeSinceLastPacket += deltaTime;

        if (timeSinceLastPacket > streamingTimeout)
        {
            if (targetAxialHorizontal != 0f || targetAxialVertical != 0f ||
                targetRotaryHorizontal != 0f || targetRotaryVertical != 0f || targetYaw != 0f)
            {
                DebugViewController.AddDebugMessage($"[{gameObject.name}] TIMEOUT: No packets for {timeSinceLastPacket:F2}s → Auto-releasing joystick");
            }

            targetAxialHorizontal = 0f;
            targetAxialVertical = 0f;
            targetRotaryHorizontal = 0f;
            targetRotaryVertical = 0f;
            targetYaw = 0f;
            currentZForward = false;
            currentZBackward = false;
            currentYawLeft = false;
            currentYawRight = false;
        }

        float lerpFactor = smoothingSpeed * deltaTime;

        currentAxialHorizontal = Mathf.Lerp(currentAxialHorizontal, targetAxialHorizontal, lerpFactor);
        currentAxialVertical = Mathf.Lerp(currentAxialVertical, targetAxialVertical, lerpFactor);
        currentRotaryHorizontal = Mathf.Lerp(currentRotaryHorizontal, targetRotaryHorizontal, lerpFactor);
        currentRotaryVertical = Mathf.Lerp(currentRotaryVertical, targetRotaryVertical, lerpFactor);
        currentYaw = Mathf.Lerp(currentYaw, targetYaw, lerpFactor);

        float currentSpeed = new Vector3(currentAxialHorizontal, currentAxialVertical, 0f).magnitude;
        if (currentSpeed > maxStreamingSpeed)
        {
            Vector3 clampedVector = new Vector3(currentAxialHorizontal, currentAxialVertical, 0f).normalized * maxStreamingSpeed;
            currentAxialHorizontal = clampedVector.x;
            currentAxialVertical = clampedVector.y;

            if (logThrottleTimer >= LOG_THROTTLE_INTERVAL)
            {
                DebugViewController.AddDebugMessage($"[{gameObject.name}] Speed clamped: {currentSpeed:F2} → {maxStreamingSpeed:F2}");
            }
        }

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
            impulseTimer -= deltaTime;

            float t = Mathf.Max(0f, impulseTimer / impulseDuration);
            currentAxialHorizontal = impulseAxialH * t;
            currentAxialVertical = impulseAxialV * t;
            currentRotaryHorizontal = impulseRotaryH * t;
            currentRotaryVertical = impulseRotaryV * t;

            if (impulseTimer <= 0f)
            {
                DebugViewController.AddDebugMessage($"[DISCRETE] Impulse completed → Starting decay");
            }
        }
        else
        {
            float decayFactor = decaySpeed * deltaTime;
            currentAxialHorizontal = Mathf.Lerp(currentAxialHorizontal, 0f, decayFactor);
            currentAxialVertical = Mathf.Lerp(currentAxialVertical, 0f, decayFactor);
            currentRotaryHorizontal = Mathf.Lerp(currentRotaryHorizontal, 0f, decayFactor);
            currentRotaryVertical = Mathf.Lerp(currentRotaryVertical, 0f, decayFactor);

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

    private float GetDynamicClamp()
    {
        return Mathf.Max(1.0f, sensitivityMultiplier);
    }

    public void ResetState()
    {
        currentAxialHorizontal = 0f;
        currentAxialVertical = 0f;
        currentRotaryHorizontal = 0f;
        currentRotaryVertical = 0f;
        currentYaw = 0f;
        targetAxialHorizontal = 0f;
        targetAxialVertical = 0f;
        targetRotaryHorizontal = 0f;
        targetRotaryVertical = 0f;
        targetYaw = 0f;
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
               $"Yaw: {currentYaw:F2}\n" +
               $"Timeout: {timeSinceLastPacket:F2}s\n" +
               $"Sensitivity: {sensitivityMultiplier:F2}x";
    }
}