using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ObjectManager : MonoBehaviour
{
    public static ObjectManager Instance { get; private set; }

    [Header("Control References")]
    [SerializeField] private FixedJoystick axialJoystick;
    [SerializeField] private FixedJoystick rotaryJoystick;
    [SerializeField] private Button zIncreaseButton;
    [SerializeField] private Button zDecreaseButton;
    [SerializeField] private Button yawLeftButton;
    [SerializeField] private Button yawRightButton;

    [Header("Settings")]
    [SerializeField] private float moveSpeed = 2.0f;  // CHANGED: Reduced from 3.0
    [SerializeField] private float depthMoveSpeed = 1.5f;  // CHANGED: Reduced from 2.0
    [SerializeField] private float rotationSpeed = 90f;  // CHANGED: Reduced from 120

    [Header("Spawn Settings")]
    [SerializeField] private float spawnDistance = 5f;

    [Header("Remote Control Prefab Mappings")]
    [SerializeField] private GameObject tablePrefab;
    [SerializeField] private GameObject chairPrefab;
    [SerializeField] private GameObject lampPrefab;
    [SerializeField] private GameObject tvConsolePrefab;
    [SerializeField] private GameObject bedPrefab;
    [SerializeField] private GameObject plantPrefab;
    [SerializeField] private GameObject sofaPrefab;

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
    }

    private void ValidateReferences()
    {
        if (axialJoystick == null)
            Debug.LogError("ObjectManager: Axial Joystick reference is missing!");
        if (rotaryJoystick == null)
            Debug.LogError("ObjectManager: Rotary Joystick reference is missing!");
        if (zIncreaseButton == null)
            Debug.LogError("ObjectManager: ZIncreaseButton reference is missing!");
        if (zDecreaseButton == null)
            Debug.LogError("ObjectManager: ZDecreaseButton reference is missing!");
        if (yawLeftButton == null)
            Debug.LogError("ObjectManager: YawLeftButton reference is missing!");
        if (yawRightButton == null)
            Debug.LogError("ObjectManager: YawRightButton reference is missing!");
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
            SelectObject(instantiatedObjects[objectName]);
            Debug.Log($"Selected existing object: {objectName}");
        }
        else
        {
            InstantiateObject(objectName, prefab);
        }
    }

    private void InstantiateObject(string objectName, GameObject prefab)
    {
        Vector3 spawnPosition = Camera.main.transform.position + Camera.main.transform.forward * spawnDistance;
        GameObject newObject = Instantiate(prefab, spawnPosition, Quaternion.identity);
        newObject.name = objectName;

        Rigidbody rb = newObject.GetComponent<Rigidbody>();
        if (rb == null) rb = newObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.drag = 5f;
        rb.angularDrag = 10f;

        if (newObject.GetComponent<Collider>() == null)
        {
            BoxCollider collider = newObject.AddComponent<BoxCollider>();
            Debug.LogWarning($"Added BoxCollider to {objectName} - consider adding proper collider to prefab");
        }

        ControllableObject controllable = newObject.GetComponent<ControllableObject>();
        if (controllable == null)
        {
            controllable = newObject.AddComponent<ControllableObject>();
        }

        controllable.Initialize(
            axialJoystick, rotaryJoystick,
            zIncreaseButton, zDecreaseButton,
            yawLeftButton, yawRightButton,
            moveSpeed, depthMoveSpeed, rotationSpeed
        );

        instantiatedObjects.Add(objectName, newObject);
        SelectObject(newObject);

        if (SettingsMenuController.Instance != null)
        {
            SettingsMenuController.Instance.ApplySettingsToObject(controllable);
        }

        Debug.Log($"Instantiated new object: {objectName} at position {spawnPosition}");
    }

    private void SelectObject(GameObject obj)
    {
        if (obj == null)
        {
            Debug.LogWarning("ObjectManager: Attempted to select null object");
            return;
        }

        if (currentlySelectedObject != null)
        {
            currentlySelectedObject.Deselect();
        }

        currentlySelectedObject = obj.GetComponent<ControllableObject>();
        if (currentlySelectedObject != null)
        {
            currentlySelectedObject.Select();

            if (UIManager.Instance != null)
            {
                UIManager.Instance.UpdateCurrentObject(obj.name);
                UIManager.Instance.UpdateObjectCoordinates(obj.transform.position);
                UIManager.Instance.ShowGridOutline(obj.name);
                UIManager.Instance.ShowDeleteButton();
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

            if (UIManager.Instance != null)
            {
                UIManager.Instance.ClearObjectInfo();
                UIManager.Instance.HideDeleteButton();
            }
            Debug.Log("Deselected all objects");
        }
    }

    public void DeleteSelectedObject()
    {
        if (currentlySelectedObject == null)
        {
            Debug.LogWarning("ObjectManager: No object selected to delete");
            return;
        }

        string objectName = currentlySelectedObject.gameObject.name;
        GameObject objectToDelete = currentlySelectedObject.gameObject;

        if (instantiatedObjects.ContainsKey(objectName))
        {
            instantiatedObjects.Remove(objectName);
        }

        currentlySelectedObject = null;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ClearObjectInfo();
            UIManager.Instance.HideDeleteButton();
        }

        Destroy(objectToDelete);

        Debug.Log($"Deleted object: {objectName}");
    }

    public void HideAllObjects()
    {
        foreach (var kvp in instantiatedObjects)
        {
            if (kvp.Value != null)
            {
                Renderer[] renderers = kvp.Value.GetComponentsInChildren<Renderer>();
                foreach (Renderer renderer in renderers)
                {
                    renderer.enabled = false;
                }
            }
        }
        Debug.Log("All objects hidden");
    }

    public void ShowAllObjects()
    {
        foreach (var kvp in instantiatedObjects)
        {
            if (kvp.Value != null)
            {
                Renderer[] renderers = kvp.Value.GetComponentsInChildren<Renderer>();
                foreach (Renderer renderer in renderers)
                {
                    renderer.enabled = true;
                }
            }
        }
        Debug.Log("All objects shown");
    }

    public ControllableObject GetCurrentlySelectedObject()
    {
        return currentlySelectedObject;
    }

    public Dictionary<string, GameObject> GetInstantiatedObjects()
    {
        return instantiatedObjects;
    }

    public bool HasSelectedObject()
    {
        return currentlySelectedObject != null;
    }

    // ===== REMOTE COMMAND HANDLERS =====

    public void HandleRemoteSelectCommand(string objectName)
    {
        string normalizedName = NormalizeObjectName(objectName);

        DebugViewController.AddDebugMessage($"Remote SELECT: {normalizedName}");

        if (instantiatedObjects.ContainsKey(normalizedName))
        {
            SelectObject(instantiatedObjects[normalizedName]);

            if (currentlySelectedObject != null)
            {
                currentlySelectedObject.SetRemoteControlled(true);
            }

            DebugViewController.AddDebugMessage($"Selected existing object: {normalizedName}");
        }
        else
        {
            GameObject prefab = GetPrefabForObjectName(normalizedName);
            if (prefab != null)
            {
                InstantiateObject(normalizedName, prefab);

                if (currentlySelectedObject != null)
                {
                    currentlySelectedObject.SetRemoteControlled(true);
                }

                DebugViewController.AddDebugMessage($"Instantiated new object: {normalizedName}");
            }
            else
            {
                Debug.LogWarning($"ObjectManager: No prefab mapped for object '{normalizedName}'");
                DebugViewController.AddDebugMessage($"ERROR: No prefab for '{normalizedName}'");
            }
        }
    }

    public void HandleRemoteDeleteCommand(string objectName)
    {
        string normalizedName = NormalizeObjectName(objectName);

        DebugViewController.AddDebugMessage($"Remote DELETE: {normalizedName}");

        if (instantiatedObjects.ContainsKey(normalizedName))
        {
            GameObject objToDelete = instantiatedObjects[normalizedName];

            if (currentlySelectedObject != null && currentlySelectedObject.gameObject == objToDelete)
            {
                currentlySelectedObject = null;
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.ClearObjectInfo();
                    UIManager.Instance.HideDeleteButton();
                }
            }

            instantiatedObjects.Remove(normalizedName);
            Destroy(objToDelete);

            DebugViewController.AddDebugMessage($"Deleted object: {normalizedName}");
        }
        else
        {
            Debug.LogWarning($"ObjectManager: Cannot delete '{normalizedName}' - object not found");
            DebugViewController.AddDebugMessage($"ERROR: Object '{normalizedName}' not found");
        }
    }

    public void HandleRemoteMoveCommand(Vector3 accelerometerData)
    {
        if (currentlySelectedObject == null) return;

        VirtualJoystickState virtualState = currentlySelectedObject.GetVirtualState();
        if (virtualState != null)
        {
            virtualState.SetAccelerometerData(accelerometerData);
        }
    }

    public void HandleRemoteRotateCommand(Vector3 gyroscopeData)
    {
        if (currentlySelectedObject == null) return;

        VirtualJoystickState virtualState = currentlySelectedObject.GetVirtualState();
        if (virtualState != null)
        {
            virtualState.SetGyroscopeData(gyroscopeData);
        }
    }

    // NEW: Method to set data flow mode on currently selected object
    public void SetDataFlowMode(VirtualJoystickState.DataFlowMode mode)
    {
        if (currentlySelectedObject != null)
        {
            VirtualJoystickState virtualState = currentlySelectedObject.GetVirtualState();
            if (virtualState != null)
            {
                virtualState.SetDataFlowMode(mode);
                DebugViewController.AddDebugMessage($"Data flow mode changed to: {mode}");
            }
        }
        else
        {
            Debug.LogWarning("ObjectManager: No object selected to change mode");
        }
    }

    // ===== HELPER METHODS =====

    private string NormalizeObjectName(string rawName)
    {
        if (string.IsNullOrEmpty(rawName)) return rawName;

        string normalized = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(rawName.ToLower());

        return normalized;
    }

    private GameObject GetPrefabForObjectName(string objectName)
    {
        switch (objectName.ToLower())
        {
            case "table":
                return tablePrefab;
            case "chair":
                return chairPrefab;
            case "lamp":
                return lampPrefab;
            case "tv console":
                return tvConsolePrefab;
            case "bed":
                return bedPrefab;
            case "plant":
                return plantPrefab;
            case "sofa":
                return sofaPrefab;
            default:
                return null;
        }
    }
}
