using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("UI Text References")]
    [SerializeField] private TextMeshProUGUI connectionStatusValue;
    [SerializeField] private TextMeshProUGUI currentStageValue;
    [SerializeField] private TextMeshProUGUI currentObjectValue;
    [SerializeField] private TextMeshProUGUI objectCoordinatesValue;

    [Header("Selection Outline")]
    [SerializeField] private RectTransform gridOutline;
    [SerializeField] private float outlineAnimationSpeed = 10f;

    // Hardcoded position values for each object button
    private Dictionary<string, float> buttonPositions = new Dictionary<string, float>()
    {
        { "Table", -268f },
        { "Chair", -179f },
        { "Lamp", -90f },
        { "TV Console", -1f },
        { "Bed", 88f },
        { "Plant", 177f },
        { "Sofa", 266f }
    };

    private float targetPosX;
    private bool isOutlineActive = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        InitializeUI();
    }

    private void InitializeUI()
    {
        UpdateConnectionStatus("Unconnected");
        UpdateCurrentStage("Object Placement");
        UpdateCurrentObject("");
        ClearCoordinates();

        // Hide the grid outline on start
        if (gridOutline != null)
        {
            gridOutline.gameObject.SetActive(false);
            isOutlineActive = false;
        }
    }

    private void Update()
    {
        // Animate the grid outline to target position
        if (isOutlineActive && gridOutline != null)
        {
            Vector3 currentPos = gridOutline.anchoredPosition;
            float newPosX = Mathf.Lerp(currentPos.x, targetPosX, Time.deltaTime * outlineAnimationSpeed);
            gridOutline.anchoredPosition = new Vector3(newPosX, currentPos.y, currentPos.z);
        }
    }

    public void UpdateConnectionStatus(string status)
    {
        if (connectionStatusValue != null)
        {
            connectionStatusValue.text = status;
        }
    }

    public void UpdateCurrentStage(string stage)
    {
        if (currentStageValue != null)
        {
            currentStageValue.text = stage;
        }
    }

    public void UpdateCurrentObject(string objectName)
    {
        if (currentObjectValue != null)
        {
            currentObjectValue.text = objectName;
        }
    }

    public void UpdateObjectCoordinates(Vector3 position)
    {
        if (objectCoordinatesValue != null)
        {
            objectCoordinatesValue.text = $"({position.x:F2}, {position.y:F2}, {position.z:F2})";
        }
    }

    private void ClearCoordinates()
    {
        if (objectCoordinatesValue != null)
        {
            objectCoordinatesValue.text = "";
        }
    }

    public void ClearObjectInfo()
    {
        UpdateCurrentObject("");
        ClearCoordinates();
        HideGridOutline();
    }

    // Show and move grid outline to selected object's button
    public void ShowGridOutline(string objectName)
    {
        if (gridOutline == null) return;

        // Check if this object has a defined position
        if (buttonPositions.ContainsKey(objectName))
        {
            targetPosX = buttonPositions[objectName];

            if (!isOutlineActive)
            {
                // First time showing - set position immediately without animation
                gridOutline.anchoredPosition = new Vector3(targetPosX, gridOutline.anchoredPosition.y, 0);
                gridOutline.gameObject.SetActive(true);
                isOutlineActive = true;
            }
            // Otherwise, Update() will animate to the new position
        }
        else
        {
            Debug.LogWarning($"UIManager: No position defined for object '{objectName}'");
        }
    }

    // Hide the grid outline
    public void HideGridOutline()
    {
        if (gridOutline != null)
        {
            gridOutline.gameObject.SetActive(false);
            isOutlineActive = false;
        }
    }
}
