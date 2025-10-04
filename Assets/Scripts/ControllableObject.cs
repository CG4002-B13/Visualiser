using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

[RequireComponent(typeof(Rigidbody))]
public class ControllableObject : MonoBehaviour
{
    private Rigidbody _rigidbody;
    private FixedJoystick _joystick;
    private Button upButton;
    private Button downButton;
    private Button modeToggle;

    private float _moveSpeed;
    private float _verticalMoveSpeed;
    private float _rotationSpeed;

    private bool _isMovingUp = false;
    private bool _isMovingDown = false;
    private bool _isRotaryMode = false;
    private bool _isSelected = false;

    private Material originalMaterial;
    private Material highlightMaterial;
    private Renderer objectRenderer;

    private Vector3 lastPosition; // Track position changes

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();

        // Setup highlight material for visual feedback
        objectRenderer = GetComponent<Renderer>();
        if (objectRenderer != null)
        {
            originalMaterial = objectRenderer.material;
            highlightMaterial = new Material(originalMaterial);
            highlightMaterial.color = Color.yellow;
        }
    }

    public void Initialize(FixedJoystick joystick, Button up, Button down, Button toggle,
                          float moveSpeed, float verticalSpeed, float rotationSpeed)
    {
        _joystick = joystick;
        upButton = up;
        downButton = down;
        modeToggle = toggle;
        _moveSpeed = moveSpeed;
        _verticalMoveSpeed = verticalSpeed;
        _rotationSpeed = rotationSpeed;

        SetupButtonEvents();
    }

    public void Select()
    {
        _isSelected = true;
        _isRotaryMode = false; // Always reset to Axial mode when selected

        // Make NON-KINEMATIC when selected (allows manual movement)
        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = false;
            _rigidbody.constraints = RigidbodyConstraints.FreezeRotation; // Freeze rotation for Axial mode

            // Reset any residual velocity
            _rigidbody.velocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }

        // Visual feedback
        if (objectRenderer != null && highlightMaterial != null)
        {
            objectRenderer.material = highlightMaterial;
        }

        lastPosition = transform.position;

        SyncModeButton();
        Debug.Log($"{gameObject.name} selected - Mode: Axial");
    }

    public void Deselect()
    {
        _isSelected = false;

        // Make KINEMATIC when deselected (prevents all physics interactions)
        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = true;

            // Reset velocity to stop any residual movement
            _rigidbody.velocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }

        // Remove visual feedback
        if (objectRenderer != null && originalMaterial != null)
        {
            objectRenderer.material = originalMaterial;
        }

        Debug.Log($"{gameObject.name} deselected - now kinematic");
    }

    private void Update()
    {
        if (!_isSelected) return;

        if (_isRotaryMode)
        {
            HandleRotaryMode();
        }
        else
        {
            HandleAxialMode();
        }

        // UPDATE UI if position changed
        if (Vector3.Distance(transform.position, lastPosition) > 0.01f)
        {
            lastPosition = transform.position;
            if (UIManager.Instance != null)
            {
                UIManager.Instance.UpdateObjectCoordinates(transform.position);
            }
        }
    }

    private void HandleAxialMode()
    {
        // Ensure rotation is frozen in Axial mode
        if (_rigidbody.constraints != RigidbodyConstraints.FreezeRotation)
        {
            _rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
        }

        Vector3 move = new Vector3(
            _joystick.Horizontal * _moveSpeed,
            0f,
            _joystick.Vertical * _moveSpeed
        );

        float vertical = 0f;
        if (_isMovingUp) vertical += _verticalMoveSpeed;
        if (_isMovingDown) vertical -= _verticalMoveSpeed;

        Vector3 displacement = (move + Vector3.up * vertical) * Time.deltaTime;

        // Use MovePosition for non-kinematic rigidbody
        _rigidbody.MovePosition(_rigidbody.position + displacement);
    }

    private void HandleRotaryMode()
    {
        // Unfreeze rotation constraints for manual rotation
        if (_rigidbody.constraints != RigidbodyConstraints.None)
        {
            _rigidbody.constraints = RigidbodyConstraints.None;
        }

        float pitch = -_joystick.Vertical * _rotationSpeed * Time.deltaTime;
        float roll = _joystick.Horizontal * _rotationSpeed * Time.deltaTime;

        float yaw = 0f;
        if (_isMovingUp) yaw += _rotationSpeed * Time.deltaTime;
        if (_isMovingDown) yaw -= _rotationSpeed * Time.deltaTime;

        Vector3 eulerRotation = new Vector3(pitch, yaw, roll);
        Quaternion deltaRotation = Quaternion.Euler(eulerRotation);

        // Use MoveRotation for non-kinematic rigidbody
        _rigidbody.MoveRotation(_rigidbody.rotation * deltaRotation);
    }

    private void SetupButtonEvents()
    {
        if (upButton != null)
        {
            AddEventTrigger(upButton, EventTriggerType.PointerDown, (_) => _isMovingUp = true);
            AddEventTrigger(upButton, EventTriggerType.PointerUp, (_) => _isMovingUp = false);
        }

        if (downButton != null)
        {
            AddEventTrigger(downButton, EventTriggerType.PointerDown, (_) => _isMovingDown = true);
            AddEventTrigger(downButton, EventTriggerType.PointerUp, (_) => _isMovingDown = false);
        }
    }

    public void ToggleMode()
    {
        if (!_isSelected) return;

        _isRotaryMode = !_isRotaryMode;
        SyncModeButton();

        // UPDATE UI with new mode
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateObjectMode(_isRotaryMode ? "Rotary" : "Axial");
        }

        Debug.Log($"{gameObject.name} - Mode switched to: {(_isRotaryMode ? "Rotary" : "Axial")}");
    }

    private void SyncModeButton()
    {
        if (modeToggle != null)
        {
            // UPDATED: Check for both Text and TextMeshPro
            Text legacyText = modeToggle.GetComponentInChildren<Text>();
            TextMeshProUGUI tmpText = modeToggle.GetComponentInChildren<TextMeshProUGUI>();

            string modeText = _isRotaryMode ? "Rotary" : "Axial";

            if (tmpText != null)
            {
                tmpText.text = modeText;
            }
            else if (legacyText != null)
            {
                legacyText.text = modeText;
            }

            UpdateButtonRotations();
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
        if (button == null) return;

        EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = button.gameObject.AddComponent<EventTrigger>();

        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = type;
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }
}
