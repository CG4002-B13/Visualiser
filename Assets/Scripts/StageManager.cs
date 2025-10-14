using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    public enum Stage
    {
        ObjectPlacement,
        ObjectDetection
    }

    [Header("Current Stage")]
    [SerializeField] private Stage currentStage = Stage.ObjectPlacement;

    [Header("Object Placement UI")]
    [SerializeField] private GameObject fixedJoystick;
    [SerializeField] private GameObject selectionPane;
    [SerializeField] private GameObject upButton;
    [SerializeField] private GameObject downButton;
    [SerializeField] private GameObject gridOutline;

    [Header("Object Detection UI")]
    [SerializeField] private GameObject boundingBoxOverlay;

    [Header("Stage Toggle Button")]
    [SerializeField] private Button objectDetectionButton;

    [Header("Managers")]
    [SerializeField] private ObjectDetectionSample objectDetectionSample;
    [SerializeField] private ObjectManager objectManager;

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
    }

    private void Start()
    {
        // Setup button listener
        if (objectDetectionButton != null)
        {
            objectDetectionButton.onClick.AddListener(ToggleStage);
        }

        // Start in Object Placement stage
        SetStage(Stage.ObjectPlacement);
    }

    public void ToggleStage()
    {
        if (currentStage == Stage.ObjectPlacement)
        {
            SetStage(Stage.ObjectDetection);
        }
        else
        {
            SetStage(Stage.ObjectPlacement);
        }
    }

    public void SetStage(Stage newStage)
    {
        currentStage = newStage;

        if (currentStage == Stage.ObjectPlacement)
        {
            ActivateObjectPlacementStage();
        }
        else
        {
            ActivateObjectDetectionStage();
        }
    }

    private void ActivateObjectPlacementStage()
    {
        // Enable Object Placement UI
        SetUIActive(fixedJoystick, true);
        SetUIActive(selectionPane, true);
        SetUIActive(upButton, true);
        SetUIActive(downButton, true);

        // GridOutline visibility is managed by UIManager based on selection
        // Just ensure it's not force-hidden

        // Disable Object Detection UI
        SetUIActive(boundingBoxOverlay, false);

        // Stop object detection
        if (objectDetectionSample != null)
        {
            objectDetectionSample.enabled = false;
        }

        // Show all placed objects
        if (objectManager != null)
        {
            objectManager.ShowAllObjects();
        }

        // Update UI Manager
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateCurrentStage("Object Placement");
        }

        Debug.Log("Switched to Object Placement Stage");
    }

    private void ActivateObjectDetectionStage()
    {
        // Disable Object Placement UI
        SetUIActive(fixedJoystick, false);
        SetUIActive(selectionPane, false);
        SetUIActive(upButton, false);
        SetUIActive(downButton, false);
        SetUIActive(gridOutline, false);

        // Enable Object Detection UI
        SetUIActive(boundingBoxOverlay, true);

        // Start object detection
        if (objectDetectionSample != null)
        {
            objectDetectionSample.enabled = true;
        }

        // Hide all placed objects (but keep their data)
        if (objectManager != null)
        {
            objectManager.HideAllObjects();
            objectManager.DeselectAll(); // Deselect current object
        }

        // Update UI Manager
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateCurrentStage("Object Detection");
            UIManager.Instance.ClearObjectInfo(); // Clear object-specific info
        }

        Debug.Log("Switched to Object Detection Stage");
    }

    private void SetUIActive(GameObject uiElement, bool active)
    {
        if (uiElement != null)
        {
            uiElement.SetActive(active);
        }
    }

    public Stage GetCurrentStage()
    {
        return currentStage;
    }

    public bool IsObjectPlacementStage()
    {
        return currentStage == Stage.ObjectPlacement;
    }

    public bool IsObjectDetectionStage()
    {
        return currentStage == Stage.ObjectDetection;
    }
}

