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
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float depthMoveSpeed = 2f;
    [SerializeField] private float rotationSpeed = 120f;

    [Header("Spawn Settings")]
    [SerializeField] private float spawnDistance = 5f;

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

        // Pass both joysticks and all four buttons
        controllable.Initialize(
            axialJoystick, rotaryJoystick,
            zIncreaseButton, zDecreaseButton,
            yawLeftButton, yawRightButton,
            moveSpeed, depthMoveSpeed, rotationSpeed
        );

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

            if (instantiatedObjects.ContainsKey(objectName))
            {
                instantiatedObjects.Remove(objectName);
            }

            currentlySelectedObject = null;

            if (UIManager.Instance != null)
            {
                UIManager.Instance.ClearObjectInfo();
            }

            Destroy(objectToDelete);
            Debug.Log($"Deleted object: {objectName}");
        }
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
}
