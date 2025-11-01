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

    public void HandleWebSocketMessage(string jsonMessage)
    {
        try
        {
            JObject messageObj = JObject.Parse(jsonMessage);

            string eventType = messageObj["eventType"]?.ToString();

            if (string.IsNullOrEmpty(eventType))
            {
                Debug.LogWarning("CommandHandler: eventType is missing in message");
                return;
            }

            DebugViewController.AddDebugMessage($"Processing command: {eventType}");

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

                case "COMMAND_SET":
                    HandleSetCommand(messageObj);
                    break;

                case "S3_UPLOAD_RESPONSE":
                    HandleS3UploadResponse(messageObj);
                    break;

                case "S3_DELETE_RESPONSE":
                    HandleS3DeleteResponse(messageObj);
                    break;

                case "S3_ERROR":
                    HandleS3Error(messageObj);
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
            JObject dataObj = messageObj["data"] as JObject;
            if (dataObj == null)
            {
                Debug.LogWarning("CommandHandler: COMMAND_SELECT has no data field");
                return;
            }

            string objectName = dataObj["result"]?.ToString();

            if (string.IsNullOrEmpty(objectName))
            {
                Debug.LogWarning("CommandHandler: COMMAND_SELECT result name is missing");
                return;
            }

            DebugViewController.AddDebugMessage($"Select command: {objectName}");

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

            string objectName = dataObj["result"]?.ToString();

            if (string.IsNullOrEmpty(objectName))
            {
                Debug.LogWarning("CommandHandler: COMMAND_DELETE result name is missing");
                return;
            }

            DebugViewController.AddDebugMessage($"Delete command: {objectName}");

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
        try
        {
            DebugViewController.AddDebugMessage("Screenshot command received");

            // TODO: Implement screenshot functionality
            Debug.Log("CommandHandler: Screenshot functionality not yet implemented");
        }
        catch (Exception ex)
        {
            Debug.LogError($"CommandHandler: Error in HandleScreenshotCommand: {ex.Message}");
        }
    }

    private void HandleSetCommand(JObject messageObj)
    {
        try
        {
            JObject dataObj = messageObj["data"] as JObject;
            if (dataObj == null)
            {
                Debug.LogWarning("CommandHandler: COMMAND_SET has no data field");
                return;
            }

            string command = dataObj["command"]?.ToString();
            string result = dataObj["result"]?.ToString();

            if (string.IsNullOrEmpty(command) || string.IsNullOrEmpty(result))
            {
                Debug.LogWarning("CommandHandler: COMMAND_SET missing command or result");
                return;
            }

            DebugViewController.AddDebugMessage($"Set command: {command} → {result}");

            if (result.ToUpper() == "UP")
            {
                if (SettingsMenuController.Instance != null)
                {
                    SettingsMenuController.Instance.AdjustSensitivity("up");
                }
                else
                {
                    Debug.LogError("CommandHandler: SettingsMenuController.Instance is null");
                }
            }
            else if (result.ToUpper() == "DOWN")
            {
                if (SettingsMenuController.Instance != null)
                {
                    SettingsMenuController.Instance.AdjustSensitivity("down");
                }
                else
                {
                    Debug.LogError("CommandHandler: SettingsMenuController.Instance is null");
                }
            }
            else if (result.ToUpper() == "ODM")
            {
                DebugViewController.AddDebugMessage("ODM toggle command received");

                if (StageManager.Instance != null)
                {
                    StageManager.Stage beforeStage = StageManager.Instance.GetCurrentStage();
                    StageManager.Instance.ToggleStage();
                    StageManager.Stage afterStage = StageManager.Instance.GetCurrentStage();

                    DebugViewController.AddDebugMessage($"[COMMAND] ODM: {beforeStage} → {afterStage}");
                }
                else
                {
                    Debug.LogError("CommandHandler: StageManager.Instance is null");
                }
            }
            else if (result.ToUpper() == "TOGGLE")
            {
                DebugViewController.AddDebugMessage("TOGGLE command received (not yet implemented)");
                Debug.Log("CommandHandler: TOGGLE functionality not yet implemented");
            }
            else
            {
                Debug.LogWarning($"CommandHandler: Unknown SET result '{result}'");
                DebugViewController.AddDebugMessage($"Unknown SET result: {result}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"CommandHandler: Error in HandleSetCommand: {ex.Message}");
        }
    }

    // ===== S3 UPLOAD HANDLERS =====

    private void HandleS3UploadResponse(JObject messageObj)
    {
        try
        {
            string presignedUrl = messageObj["data"]?.ToString();

            if (string.IsNullOrEmpty(presignedUrl))
            {
                Debug.LogWarning("CommandHandler: S3_UPLOAD_RESPONSE has empty data field");
                return;
            }

            // Route to ScreenshotUploadManager
            if (ScreenshotUploadManager.Instance != null)
            {
                ScreenshotUploadManager.Instance.OnUploadResponseReceived(presignedUrl);
            }
            else
            {
                Debug.LogError("CommandHandler: ScreenshotUploadManager.Instance is null");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"CommandHandler: Error in HandleS3UploadResponse: {ex.Message}");
        }
    }

    // ===== S3 DELETE HANDLERS =====

    private void HandleS3DeleteResponse(JObject messageObj)
    {
        try
        {
            string presignedUrl = messageObj["data"]?.ToString();

            if (string.IsNullOrEmpty(presignedUrl))
            {
                Debug.LogWarning("CommandHandler: S3_DELETE_RESPONSE has empty data field");
                return;
            }

            // Route to ScreenshotDeleteManager
            if (ScreenshotDeleteManager.Instance != null)
            {
                ScreenshotDeleteManager.Instance.OnDeleteResponseReceived(presignedUrl);
            }
            else
            {
                Debug.LogError("CommandHandler: ScreenshotDeleteManager.Instance is null");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"CommandHandler: Error in HandleS3DeleteResponse: {ex.Message}");
        }
    }

    // ===== S3 ERROR HANDLER =====

    private void HandleS3Error(JObject messageObj)
    {
        try
        {
            string errorMessage = messageObj["data"]?.ToString();

            if (string.IsNullOrEmpty(errorMessage))
            {
                errorMessage = "Unknown S3 error";
            }

            // Determine which operation failed based on pending state
            bool uploadPending = ScreenshotUploadManager.Instance != null && ScreenshotUploadManager.Instance.IsUploadPending;
            bool deletePending = ScreenshotDeleteManager.Instance != null && ScreenshotDeleteManager.Instance.IsDeletePending;

            if (deletePending)
            {
                // Route to ScreenshotDeleteManager
                if (ScreenshotDeleteManager.Instance != null)
                {
                    ScreenshotDeleteManager.Instance.OnDeleteErrorReceived(errorMessage);
                }
            }
            else if (uploadPending)
            {
                // Route to ScreenshotUploadManager
                if (ScreenshotUploadManager.Instance != null)
                {
                    ScreenshotUploadManager.Instance.OnUploadErrorReceived(errorMessage);
                }
            }
            else
            {
                // Unknown operation
                Debug.LogWarning($"CommandHandler: S3_ERROR received but no operation pending: {errorMessage}");
                DebugViewController.AddDebugMessage($"S3 Error: {errorMessage}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"CommandHandler: Error in HandleS3Error: {ex.Message}");
        }
    }
}