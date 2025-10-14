using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Rigidbody))]
public class ControllableObject : MonoBehaviour
{
    private Rigidbody _rigidbody;
    private FixedJoystick _axialJoystick;
    private FixedJoystick _rotaryJoystick;
    private Button zIncreaseButton, zDecreaseButton, yawLeftButton, yawRightButton;

    private float _moveSpeed;
    private float _depthMoveSpeed;
    private float _rotationSpeed;

    private bool _isMovingForward = false;
    private bool _isMovingBackward = false;
    private bool _isYawingLeft = false;
    private bool _isYawingRight = false;
    private bool _isSelected = false;

    private Material originalMaterial;
    private Material highlightMaterial;
    private Renderer objectRenderer;

    private Vector3 lastPosition;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        objectRenderer = GetComponent<Renderer>();
        if (objectRenderer != null)
        {
            originalMaterial = objectRenderer.material;
            highlightMaterial = new Material(originalMaterial);
            highlightMaterial.color = Color.yellow;
        }
    }

    /// <summary>
    /// Assigns control references and settings, called after instantiating an object.
    /// </summary>
    public void Initialize(
        FixedJoystick axial, FixedJoystick rotary,
        Button zForward, Button zBackward,
        Button yawLeft, Button yawRight,
        float moveSpeed, float depthSpeed, float rotationSpeed)
    {
        _axialJoystick = axial;
        _rotaryJoystick = rotary;
        zIncreaseButton = zForward;
        zDecreaseButton = zBackward;
        yawLeftButton = yawLeft;
        yawRightButton = yawRight;
        _moveSpeed = moveSpeed;
        _depthMoveSpeed = depthSpeed;
        _rotationSpeed = rotationSpeed;

        SetupButtonEvents();
    }

    public void Select()
    {
        _isSelected = true;
        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = false;
            _rigidbody.constraints = RigidbodyConstraints.None;
            _rigidbody.velocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }
        if (objectRenderer != null && highlightMaterial != null)
        {
            objectRenderer.material = highlightMaterial;
        }
        lastPosition = transform.position;
        Debug.Log($"{gameObject.name} selected");
    }

    public void Deselect()
    {
        _isSelected = false;
        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = true;
            _rigidbody.velocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }
        if (objectRenderer != null && originalMaterial != null)
        {
            objectRenderer.material = originalMaterial;
        }
        Debug.Log($"{gameObject.name} deselected - now kinematic");
    }

    private void Update()
    {
        if (!_isSelected) return;

        HandleTranslation();
        HandleRotation();

        // Update UI if position changed
        if (Vector3.Distance(transform.position, lastPosition) > 0.01f)
        {
            lastPosition = transform.position;
            if (UIManager.Instance != null)
            {
                UIManager.Instance.UpdateObjectCoordinates(transform.position);
            }
        }
    }

    private void HandleTranslation()
    {
        float xMove = _axialJoystick != null ? _axialJoystick.Horizontal * _moveSpeed : 0f;
        float yMove = _axialJoystick != null ? _axialJoystick.Vertical * _moveSpeed : 0f;

        float zMove = 0f;
        if (_isMovingForward) zMove += _depthMoveSpeed;
        if (_isMovingBackward) zMove -= _depthMoveSpeed;

        Vector3 move = new Vector3(xMove, yMove, zMove);
        Vector3 displacement = move * Time.deltaTime;

        _rigidbody.MovePosition(_rigidbody.position + displacement);
    }

    private void HandleRotation()
    {
        float pitch = _rotaryJoystick != null ? -_rotaryJoystick.Vertical * _rotationSpeed * Time.deltaTime : 0f;
        float roll = _rotaryJoystick != null ? -_rotaryJoystick.Horizontal * _rotationSpeed * Time.deltaTime : 0f;

        float yaw = 0f;
        if (_isYawingLeft) yaw += _rotationSpeed * Time.deltaTime;
        if (_isYawingRight) yaw -= _rotationSpeed * Time.deltaTime;

        Vector3 eulerRotation = new Vector3(pitch, yaw, roll);
        Quaternion deltaRotation = Quaternion.Euler(eulerRotation);

        _rigidbody.MoveRotation(_rigidbody.rotation * deltaRotation);
    }

    private void SetupButtonEvents()
    {
        // Z movement
        AddEventTrigger(zIncreaseButton, EventTriggerType.PointerDown, (_) => _isMovingForward = true);
        AddEventTrigger(zIncreaseButton, EventTriggerType.PointerUp, (_) => _isMovingForward = false);

        AddEventTrigger(zDecreaseButton, EventTriggerType.PointerDown, (_) => _isMovingBackward = true);
        AddEventTrigger(zDecreaseButton, EventTriggerType.PointerUp, (_) => _isMovingBackward = false);

        // Yaw
        AddEventTrigger(yawLeftButton, EventTriggerType.PointerDown, (_) => _isYawingLeft = true);
        AddEventTrigger(yawLeftButton, EventTriggerType.PointerUp, (_) => _isYawingLeft = false);

        AddEventTrigger(yawRightButton, EventTriggerType.PointerDown, (_) => _isYawingRight = true);
        AddEventTrigger(yawRightButton, EventTriggerType.PointerUp, (_) => _isYawingRight = false);
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
