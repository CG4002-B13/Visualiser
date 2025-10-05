using System;
using System.Collections;
using System.Collections.Generic;
using Niantic.Lightship.AR.ObjectDetection;
using UnityEngine;

public class ObjectDetectionSample : MonoBehaviour
{
    [SerializeField] private float _probabilityThreshold = .5f;
    [SerializeField] private ARObjectDetectionManager _objectDetectionManager;

    private bool isDetectionEnabled = false;

    private Color[] _colors = new Color[]
    {
        Color.red,
        Color.blue,
        Color.green,
        Color.yellow,
        Color.magenta,
        Color.cyan,
        Color.white,
        Color.black
    };

    [SerializeField] private DrawRect _drawRect;

    private Canvas _canvas;

    private void Awake()
    {
        _canvas = FindObjectOfType<Canvas>();
    }


    // Start is called before the first frame update
    void Start()
    {
        _objectDetectionManager.enabled = false;
        _objectDetectionManager.MetadataInitialized += ObjectDetectionManagerOnMetadataInitialized;
    }

    private void ObjectDetectionManagerOnMetadataInitialized(ARObjectDetectionModelEventArgs obj)
    {
        _objectDetectionManager.ObjectDetectionsUpdated += ObjectDetectionManagerOnObjectDetectionsUpdated;


    }

    private void OnDestroy()
    {
        _objectDetectionManager.MetadataInitialized -= ObjectDetectionManagerOnMetadataInitialized;
        _objectDetectionManager.ObjectDetectionsUpdated -= ObjectDetectionManagerOnObjectDetectionsUpdated;
    }

    private void OnEnable()
    {
        if (_objectDetectionManager != null)
        {
            _objectDetectionManager.enabled = true;
            isDetectionEnabled = true;
            Debug.Log("Object Detection Enabled");
        }
    }

    private void OnDisable()
    {
        if (_objectDetectionManager != null)
        {
            _objectDetectionManager.enabled = false;
            isDetectionEnabled = false;

            // Clear any existing rectangles
            if (_drawRect != null)
            {
                _drawRect.ClearRects();
            }

            Debug.Log("Object Detection Disabled");
        }
    }

    private void ObjectDetectionManagerOnObjectDetectionsUpdated(ARObjectDetectionsUpdatedEventArgs obj)
    {
        string resultString = "";
        float _confidence = 0;
        string _name = "";
        var results = obj.Results;

        if (!isDetectionEnabled)
        {
            return;
        }

        if (results == null)
        {
            return;
        }

        _drawRect.ClearRects();

        for (int i = 0; i < results.Count; i++)
        {
            var detection = results[i];
            var categorizations = detection.GetConfidentCategorizations(_probabilityThreshold);

            if (categorizations.Count <= 0)
            {
                break;
            }

            categorizations.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));

            var categoryToDisplay = categorizations[0];

            _confidence = categoryToDisplay.Confidence;
            _name = categoryToDisplay.CategoryName;

            int h = Mathf.FloorToInt(_canvas.GetComponent<RectTransform>().rect.height);
            int w = Mathf.FloorToInt(_canvas.GetComponent<RectTransform>().rect.width);

            var rect = results[i].CalculateRect(w, h, Screen.orientation);
            resultString = $"{_name}: {_confidence:F6}\n";

            _drawRect.CreateRect(rect, _colors[i % _colors.Length], resultString);

        }
    }
}
