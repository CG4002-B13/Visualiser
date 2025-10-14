using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public class CubeController2 : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private Rigidbody _rigidbody;
    [SerializeField] private FixedJoystick _axialJoystick;  // X/Y movement
    [SerializeField] private FixedJoystick _rotaryJoystick; // Pitch/Roll rotation
    [SerializeField] private float _moveSpeed = 5f;

    [Header("Z-Axis Controls")]
    [SerializeField] private Button zIncreaseButton;
    [SerializeField] private Button zDecreaseButton;
    [SerializeField] private float _depthMoveSpeed = 3f;

    [Header("Yaw Controls")]
    [SerializeField] private Button yawLeftButton;
    [SerializeField] private Button yawRightButton;
    [SerializeField] private float _rotationSpeed = 90f; // degrees per second

    // State tracking
    private bool _isMovingForward = false;
    private bool _isMovingBackward = false;
    private bool _isYawingLeft = false;
    private bool _isYawingRight = false;

    private void Awake()
    {
        SetupButtonEvents();
    }

    private void Update()
    {
        HandleTranslation();
        HandleRotation();
    }

    private void HandleTranslation()
    {
        // Axial joystick controls X (horizontal) and Y (vertical) movement
        float xMove = _axialJoystick.Horizontal * _moveSpeed;
        float yMove = _axialJoystick.Vertical * _moveSpeed;

        // Z-axis buttons control depth movement (forward/backward)
        float zMove = 0f;
        if (_isMovingForward) zMove += _depthMoveSpeed;
        if (_isMovingBackward) zMove -= _depthMoveSpeed;

        Vector3 move = new Vector3(xMove, yMove, zMove);
        Vector3 displacement = move * Time.deltaTime;
        _rigidbody.MovePosition(_rigidbody.position + displacement);
    }

    private void HandleRotation()
    {
        // Rotary joystick controls Pitch (X-axis) and Roll (Z-axis)
        float pitch = -_rotaryJoystick.Vertical * _rotationSpeed * Time.deltaTime;
        float roll = -_rotaryJoystick.Horizontal * _rotationSpeed * Time.deltaTime; // Negated for correct direction

        // Yaw buttons control Yaw (Y-axis)
        float yaw = 0f;
        if (_isYawingLeft) yaw += _rotationSpeed * Time.deltaTime;
        if (_isYawingRight) yaw -= _rotationSpeed * Time.deltaTime;

        // Apply rotation
        Vector3 eulerRotation = new Vector3(pitch, yaw, roll);
        _rigidbody.MoveRotation(_rigidbody.rotation * Quaternion.Euler(eulerRotation));
    }

    private void SetupButtonEvents()
    {
        // Z-axis buttons
        AddEventTrigger(zIncreaseButton, EventTriggerType.PointerDown, (_) => _isMovingForward = true);
        AddEventTrigger(zIncreaseButton, EventTriggerType.PointerUp, (_) => _isMovingForward = false);

        AddEventTrigger(zDecreaseButton, EventTriggerType.PointerDown, (_) => _isMovingBackward = true);
        AddEventTrigger(zDecreaseButton, EventTriggerType.PointerUp, (_) => _isMovingBackward = false);

        // Yaw buttons
        AddEventTrigger(yawLeftButton, EventTriggerType.PointerDown, (_) => _isYawingLeft = true);
        AddEventTrigger(yawLeftButton, EventTriggerType.PointerUp, (_) => _isYawingLeft = false);

        AddEventTrigger(yawRightButton, EventTriggerType.PointerDown, (_) => _isYawingRight = true);
        AddEventTrigger(yawRightButton, EventTriggerType.PointerUp, (_) => _isYawingRight = false);
    }

    private void AddEventTrigger(Button button, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> action)
    {
        EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = button.gameObject.AddComponent<EventTrigger>();

        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = type;
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }
}
