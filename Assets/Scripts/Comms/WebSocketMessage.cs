using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WebSocketMessage
{
    public string eventType;
    public string userId;
    public string sessionId;
    public long timestamp;
    public object data; // Can be string, object, array, etc.
}

[Serializable]
public class CommandSelectData
{
    public string command; // "SELECT"
    public string @object; // "TABLE", "CHAIR", etc.
}

[Serializable]
public class CommandDeleteData
{
    public string command; // "DELETE"
    public string @object; // "TABLE", "CHAIR", etc.
}

[Serializable]
public class CommandMoveData
{
    public float[] data; // [x, y, z]
}

[Serializable]
public class CommandRotateData
{
    public float[] data; // [x, y, z] rotation angles
}
