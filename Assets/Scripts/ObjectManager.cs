using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ObjectManager : MonoBehaviour
{
    public static ObjectManager Instance { get; private set; }

    [Header("Control References")]
    [SerializeField] private FixedJoystick joystick;
    [SerializeField] private Button upButton;
    [SerializeField] private Button downButton;
    [SerializeField] private Button modeToggle;

    [Header("Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float verticalMoveSpeed = 3f;
    [SerializeField] private float rotationSpeed = 120f;

    [Header("Spawn Settings")]
    [SerializeField] private float spawnDistance = 2f;

    private Dictionary<string, GameObject> instantiatedObjects = new Dictionary<string, GameObject>();
    private ControllableObject currentlySelectedObject;

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

        ValidateReferences();
        SetupModeToggleButton();
    }

    private void ValidateReferences()
    {
        if (joystick == null)
            Debug.LogError("ObjectManager: Joystick reference is missing!");
        if (upButton == null)
            Debug.LogError("ObjectManager: UpButton reference is missing!");
        if (downButton == null)
            Debug.LogError("ObjectManager: DownButton reference is missing!");
        if (modeToggle == null)
            Debug.LogError("ObjectManager: ModeToggle reference is missing!");
    }

    private void SetupModeToggleButton()
    {
        if (modeToggle != null)
        {
            // Clear any existing listeners to prevent duplicates
            modeToggle.onClick.RemoveAllListeners();

            // Add a single listener that delegates to the currently selected object
            modeToggle.onClick.AddListener(OnModeTogglePressed);
        }
    }

    private void OnModeTogglePressed()
    {
        if (currentlySelectedObject != null)
        {
            currentlySelectedObject.ToggleMode();
        }
    }

    public void HandleObjectButton(string objectName, GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogError($"ObjectManager: Prefab for {objectName} is null!");
            return;
        }

        if (instantiatedObjects.ContainsKey(objectName))
        {
            // Object already exists, select it
            SelectObject(instantiatedObjects[objectName]);
            Debug.Log($"Selected existing object: {objectName}");
        }
        else
        {
            // Instantiate new object
            InstantiateObject(objectName, prefab);
        }
    }

    private void InstantiateObject(string objectName, GameObject prefab)
    {
        // Spawn in front of camera
        Vector3 spawnPosition = Camera.main.transform.position + Camera.main.transform.forward * spawnDistance;
        GameObject newObject = Instantiate(prefab, spawnPosition, Quaternion.identity);
        newObject.name = objectName;

        // Configure Rigidbody - START AS KINEMATIC to prevent physics chaos
        Rigidbody rb = newObject.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = newObject.AddComponent<Rigidbody>();
        }

        // CRITICAL: Start kinematic to prevent drift/spin on collision
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // These will be used when object is selected (non-kinematic mode)
        rb.drag = 5f;
        rb.angularDrag = 10f;

        // Ensure collider exists
        if (newObject.GetComponent<Collider>() == null)
        {
            BoxCollider collider = newObject.AddComponent<BoxCollider>();
            Debug.LogWarning($"Added BoxCollider to {objectName} - consider adding proper collider to prefab");
        }

        // Add ControllableObject component if it doesn't exist
        ControllableObject controllable = newObject.GetComponent<ControllableObject>();
        if (controllable == null)
        {
            controllable = newObject.AddComponent<ControllableObject>();
        }

        // Initialize the controllable object
        controllable.Initialize(joystick, upButton, downButton, modeToggle,
                               moveSpeed, verticalMoveSpeed, rotationSpeed);

        instantiatedObjects.Add(objectName, newObject);
        SelectObject(newObject);

        Debug.Log($"Instantiated new object: {objectName} at position {spawnPosition}");
    }

    private void SelectObject(GameObject obj)
    {
        if (obj == null)
        {
            Debug.LogWarning("ObjectManager: Attempted to select null object");
            return;
        }

        // Deselect current object
        if (currentlySelectedObject != null)
        {
            currentlySelectedObject.Deselect();
        }

        // Select new object
        currentlySelectedObject = obj.GetComponent<ControllableObject>();
        if (currentlySelectedObject != null)
        {
            currentlySelectedObject.Select(); // This will make it non-kinematic and reset to Axial mode

            // UPDATE UI with selected object info
            if (UIManager.Instance != null)
            {
                UIManager.Instance.UpdateCurrentObject(obj.name);
                UIManager.Instance.UpdateObjectMode("Axial"); // Always starts in Axial
                UIManager.Instance.UpdateObjectCoordinates(obj.transform.position);

                // SHOW GRID OUTLINE at the object's button position
                UIManager.Instance.ShowGridOutline(obj.name);
            }

            Debug.Log($"Selected: {obj.name}");
        }
        else
        {
            Debug.LogError($"ObjectManager: {obj.name} does not have ControllableObject component");
        }
    }

    public void DeselectAll()
    {
        if (currentlySelectedObject != null)
        {
            currentlySelectedObject.Deselect();
            currentlySelectedObject = null;

            // CLEAR UI when nothing is selected
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ClearObjectInfo();
            }

            Debug.Log("Deselected all objects");
        }
    }

    public ControllableObject GetCurrentlySelectedObject()
    {
        return currentlySelectedObject;
    }

    public Dictionary<string, GameObject> GetInstantiatedObjects()
    {
        return instantiatedObjects;
    }

    public void DeleteSelectedObject()
    {
        if (currentlySelectedObject != null)
        {
            string objectName = currentlySelectedObject.gameObject.name;
            GameObject objectToDelete = currentlySelectedObject.gameObject;

            // Remove from dictionary
            if (instantiatedObjects.ContainsKey(objectName))
            {
                instantiatedObjects.Remove(objectName);
            }

            // Clear reference
            currentlySelectedObject = null;

            // CLEAR UI when object is deleted
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ClearObjectInfo();
            }

            // Destroy the object
            Destroy(objectToDelete);

            Debug.Log($"Deleted object: {objectName}");
        }
    }
}
