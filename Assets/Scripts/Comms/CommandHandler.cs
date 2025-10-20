using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class CommandHandler : MonoBehaviour
{
    public static CommandHandler Instance { get; private set; }

    [Header("References")]
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
        }
    }

    private void Start()
    {
        if (objectManager == null)
        {
            objectManager = ObjectManager.Instance;
        }
    }

    /// <summary>
    /// Main entry point for WebSocket message handling
    /// </summary>
    public void HandleWebSocketMessage(string jsonMessage)
    {
        try
        {
            // Parse the JSON message
            JObject messageObj = JObject.Parse(jsonMessage);

            string eventType = messageObj["eventType"]?.ToString();

            if (string.IsNullOrEmpty(eventType))
            {
                Debug.LogWarning("CommandHandler: eventType is missing in message");
                return;
            }

            DebugViewController.AddDebugMessage($"Processing command: {eventType}");

            // Route to appropriate handler
            switch (eventType)
            {
                case "COMMAND_SELECT":
                    HandleSelectCommand(messageObj);
                    break;

                case "COMMAND_DELETE":
                    HandleDeleteCommand(messageObj);
                    break;

                case "COMMAND_MOVE":
                    HandleMoveCommand(messageObj);
                    break;

                case "COMMAND_ROTATE":
                    HandleRotateCommand(messageObj);
                    break;

                case "COMMAND_SCREENSHOT":
                    HandleScreenshotCommand(messageObj);
                    break;

                case "COMMAND_SPEECH":
                    HandleSpeechCommand(messageObj);
                    break;

                default:
                    Debug.LogWarning($"CommandHandler: Unknown eventType '{eventType}'");
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"CommandHandler: Error parsing message: {ex.Message}");
            DebugViewController.AddDebugMessage($"ERROR parsing command: {ex.Message}");
        }
    }

    private void HandleSelectCommand(JObject messageObj)
    {
        try
        {
            // Parse data field
            JObject dataObj = messageObj["data"] as JObject;
            if (dataObj == null)
            {
                Debug.LogWarning("CommandHandler: COMMAND_SELECT has no data field");
                return;
            }

            string objectName = dataObj["object"]?.ToString();

            if (string.IsNullOrEmpty(objectName))
            {
                Debug.LogWarning("CommandHandler: COMMAND_SELECT object name is missing");
                return;
            }

            DebugViewController.AddDebugMessage($"Select command: {objectName}");

            // Call ObjectManager to select/instantiate object
            if (objectManager != null)
            {
                objectManager.HandleRemoteSelectCommand(objectName);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"CommandHandler: Error in HandleSelectCommand: {ex.Message}");
        }
    }

    private void HandleDeleteCommand(JObject messageObj)
    {
        try
        {
            JObject dataObj = messageObj["data"] as JObject;
            if (dataObj == null)
            {
                Debug.LogWarning("CommandHandler: COMMAND_DELETE has no data field");
                return;
            }

            string objectName = dataObj["object"]?.ToString();

            if (string.IsNullOrEmpty(objectName))
            {
                Debug.LogWarning("CommandHandler: COMMAND_DELETE object name is missing");
                return;
            }

            DebugViewController.AddDebugMessage($"Delete command: {objectName}");

            // Call ObjectManager to delete object
            if (objectManager != null)
            {
                objectManager.HandleRemoteDeleteCommand(objectName);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"CommandHandler: Error in HandleDeleteCommand: {ex.Message}");
        }
    }

    private void HandleMoveCommand(JObject messageObj)
    {
        try
        {
            JArray dataArray = messageObj["data"] as JArray;
            if (dataArray == null || dataArray.Count != 3)
            {
                Debug.LogWarning("CommandHandler: COMMAND_MOVE data should be [x, y, z] array");
                return;
            }

            float x = dataArray[0].ToObject<float>();
            float y = dataArray[1].ToObject<float>();
            float z = dataArray[2].ToObject<float>();

            Vector3 moveVector = new Vector3(x, y, z);

            DebugViewController.AddDebugMessage($"Move command: ({x:F2}, {y:F2}, {z:F2})");

            // Call ObjectManager to move selected object
            if (objectManager != null)
            {
                objectManager.HandleRemoteMoveCommand(moveVector);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"CommandHandler: Error in HandleMoveCommand: {ex.Message}");
        }
    }

    private void HandleRotateCommand(JObject messageObj)
    {
        try
        {
            JArray dataArray = messageObj["data"] as JArray;
            if (dataArray == null || dataArray.Count != 3)
            {
                Debug.LogWarning("CommandHandler: COMMAND_ROTATE data should be [x, y, z] array");
                return;
            }

            float x = dataArray[0].ToObject<float>();
            float y = dataArray[1].ToObject<float>();
            float z = dataArray[2].ToObject<float>();

            Vector3 rotationVector = new Vector3(x, y, z);

            DebugViewController.AddDebugMessage($"Rotate command: ({x:F2}, {y:F2}, {z:F2})");

            // Call ObjectManager to rotate selected object
            if (objectManager != null)
            {
                objectManager.HandleRemoteRotateCommand(rotationVector);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"CommandHandler: Error in HandleRotateCommand: {ex.Message}");
        }
    }

    private void HandleScreenshotCommand(JObject messageObj)
    {
        DebugViewController.AddDebugMessage("Screenshot command received (not yet implemented)");
        // TODO: Implement screenshot functionality
    }

    private void HandleSpeechCommand(JObject messageObj)
    {
        DebugViewController.AddDebugMessage("Speech command received (not yet implemented)");
        // TODO: Implement speech/text-to-speech functionality
    }
}
