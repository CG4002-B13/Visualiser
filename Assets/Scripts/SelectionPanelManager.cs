using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class ObjectButtonMapping
{
    public string objectName;
    public Button button;
    public GameObject prefab;
}

public class SelectionPanelManager : MonoBehaviour
{
    [SerializeField] private ObjectButtonMapping[] objectMappings;

    private void Start()
    {
        SetupButtons();
    }

    private void SetupButtons()
    {
        foreach (var mapping in objectMappings)
        {
            if (mapping.button != null && mapping.prefab != null)
            {
                string objectName = mapping.objectName;
                GameObject prefab = mapping.prefab;

                mapping.button.onClick.AddListener(() =>
                {
                    ObjectManager.Instance.HandleObjectButton(objectName, prefab);
                });
            }
        }
    }
}

