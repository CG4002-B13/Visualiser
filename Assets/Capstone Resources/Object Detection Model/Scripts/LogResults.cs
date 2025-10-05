using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Niantic.Lightship.AR.ObjectDetection;


public class LogResults : MonoBehaviour
{
    [SerializeField] private ARObjectDetectionManager _objectDetectionManager;
    [SerializeField] private float _confidenceThreshold = 0.6f;


    void Start()
    {
        _objectDetectionManager.enabled = true;
        _objectDetectionManager.MetadataInitialized += OnMetadataInitialized;
    }

    private void OnMetadataInitialized(ARObjectDetectionModelEventArgs eventArgs)
    {
        _objectDetectionManager.ObjectDetectionsUpdated += OnDetectionsUpdated;
    }

    private void Oestroy()
    {
        _objectDetectionManager.MetadataInitialized -= OnMetadataInitialized;
        _objectDetectionManager.ObjectDetectionsUpdated -= OnDetectionsUpdated;
    }

    private void OnDetectionsUpdated(ARObjectDetectionsUpdatedEventArgs eventArgs)
    {
        var detections = eventArgs.Results;

        if (detections == null)
        {
            return;
        }

        string logMessage = " ";

        foreach (var detectionObject in detections)
        {
            var classifications = detectionObject.GetConfidentCategorizations();

            if (classifications.Count <= 0)
            {
                continue;
            }

            classifications.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));

            foreach (var classification in classifications)
            {
                logMessage += $"Detected: {classification.CategoryName} with confidence {classification.Confidence}\n";
            }
        }

        if (!string.IsNullOrEmpty(logMessage))
        {
            Debug.Log(logMessage);
        }
    }

    void Update()
    {

    }
}
