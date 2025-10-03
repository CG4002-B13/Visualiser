using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public class CubeController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private Rigidbody _rigidbody;
    [SerializeField] private FixedJoystick _joystick;
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _verticalMoveSpeed = 3f;

    [Header("Rotation Settings")]
    [SerializeField] private float _rotationSpeed = 90f; // degrees per second

    [Header("UI Controls")]
    [SerializeField] private Button upButton;
    [SerializeField] private Button downButton;
    [SerializeField] private Button modeToggle;

    private bool _isMovingUp = false;
    private bool _isMovingDown = false;
    private bool _isRotaryMode = false; // false = Axial, true = Rotary

    private void Awake()
    {
        SetupButtonEvents();
        SetupModeToggleButton();
    }

    private void Update()
    {
        if (_isRotaryMode)
        {
            HandleRotaryMode();
        }
        else
        {
            HandleAxialMode();
        }
    }

    private void HandleAxialMode()
    {
        Vector3 move = new Vector3(
            _joystick.Horizontal * _moveSpeed,
            0f,
            _joystick.Vertical * _moveSpeed
        );

        float vertical = 0f;
        if (_isMovingUp) vertical += _verticalMoveSpeed;
        if (_isMovingDown) vertical -= _verticalMoveSpeed;

        Vector3 displacement = (move + Vector3.up * vertical) * Time.deltaTime;
        _rigidbody.MovePosition(_rigidbody.position + displacement);
    }

    private void HandleRotaryMode()
    {
        // Joystick controls: Vertical = Pitch (X-axis), Horizontal = Roll (Z-axis)
        float pitch = -_joystick.Vertical * _rotationSpeed * Time.deltaTime; // Negative for intuitive control
        float roll = _joystick.Horizontal * _rotationSpeed * Time.deltaTime;

        // Buttons control Yaw (Y-axis)
        float yaw = 0f;
        if (_isMovingUp) yaw += _rotationSpeed * Time.deltaTime;
        if (_isMovingDown) yaw -= _rotationSpeed * Time.deltaTime;

        // Apply rotation
        Vector3 eulerRotation = new Vector3(pitch, yaw, roll);
        _rigidbody.MoveRotation(_rigidbody.rotation * Quaternion.Euler(eulerRotation));
    }

    private void SetupButtonEvents()
    {
        AddEventTrigger(upButton, EventTriggerType.PointerDown, (_) => _isMovingUp = true);
        AddEventTrigger(upButton, EventTriggerType.PointerUp, (_) => _isMovingUp = false);

        AddEventTrigger(downButton, EventTriggerType.PointerDown, (_) => _isMovingDown = true);
        AddEventTrigger(downButton, EventTriggerType.PointerUp, (_) => _isMovingDown = false);
    }

    private void SetupModeToggleButton()
    {
        if (modeToggle != null)
        {
            modeToggle.onClick.AddListener(ToggleMode);
            UpdateModeButtonText();
            UpdateButtonRotations();
        }
    }

    private void ToggleMode()
    {
        _isRotaryMode = !_isRotaryMode;
        UpdateModeButtonText();
        UpdateButtonRotations();
        Debug.Log($"Mode switched to: {(_isRotaryMode ? "Rotary" : "Axial")}");
    }

    private void UpdateModeButtonText()
    {
        if (modeToggle != null)
        {
            Text buttonText = modeToggle.GetComponentInChildren<Text>();
            if (buttonText != null)
            {
                buttonText.text = _isRotaryMode ? "Rotary" : "Axial";
            }
        }
    }

    private void UpdateButtonRotations()
    {
        if (_isRotaryMode)
        {
            // Rotary mode rotations
            SetButtonZRotation(upButton, 180f);
            SetButtonZRotation(downButton, 0f);
        }
        else
        {
            // Axial mode rotations
            SetButtonZRotation(upButton, 90f);
            SetButtonZRotation(downButton, -90f);
        }
    }

    private void SetButtonZRotation(Button button, float zRotation)
    {
        if (button != null)
        {
            RectTransform rectTransform = button.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                Vector3 currentRotation = rectTransform.localEulerAngles;
                rectTransform.localEulerAngles = new Vector3(currentRotation.x, currentRotation.y, zRotation);
            }
        }
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
