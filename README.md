# 📱 B13 AR Furniture Layout Designer (Visualizer Repository)

**CG4002 Capstone Project - Group B13**

> ***Transform any space into a showroom. Place, manipulate, and share furniture in augmented reality with cloud-powered photo galleries.***

> ***Think IKEA Object Placement App with intelligent cloud storage and real-time data streaming.***

[![Unity](https://img.shields.io/badge/Unity-2022.3.62f1-black.svg?style=flat&logo=unity)](https://unity.com/)
[![Platform](https://img.shields.io/badge/Platform-iOS%2013%2B-blue.svg?style=flat&logo=apple)](https://www.apple.com/ios/)
[![AR Foundation](https://img.shields.io/badge/AR%20Foundation-6.0-green.svg)](https://unity.com/unity/features/arfoundation)
[![License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

---

## 🎯 Overview

B13 is an iOS AR app for placing and manipulating virtual furniture using touch controls or remote commands. It features:

- Intuitive local controls (dual joysticks + adjustment buttons).
- Secure WebSocket remote control with mTLS
- Cloud-backed screenshot gallery with AWS S3 (presigned URLs).
- AI object detection via Niantic Lightship.

---

## ✨ Key Features

### 🪑 AR Furniture Placement & Manipulation
- Dual joystick controls (axial for horizontal/vertical movement; rotary for pitch/roll).
- Depth adjustment buttons and yaw controls for fine tuning.
- Visual feedback for selection (outline/highlight).

### 🔗 Remote Command System
- Mutual TLS (mTLS) WebSocket for secure, low-latency commands.
- JSON commands use normalized values (range [-1, 1]) for movement/rotation.
- Remote inputs are fed into the same movement pipeline as local joysticks for consistent behaviour.

### 📸 Cloud Screenshot Gallery
- Capture screenshots and upload/download via S3 using presigned URLs.
- Per-user folders (screenshots/{username}/) and bidirectional sync (default safety cap: 100 files per sync).

### 🤖 AI Object Detection (optional)
- Integrates Niantic Lightship for on-device object detection and bounding boxes.
- Toggle between placement and detection modes.

---

## 🏗️ Architecture & Tech Stack

- Frontend: Unity 2022.3 LTS (2022.3.62f1)
- AR: AR Foundation + ARKit (iOS)
- Optional object detection: Niantic Lightship
- Networking: Native iOS WebSocket with mTLS (P12 certificate)
- Cloud: AWS S3 with presigned URLs
- JSON parsing: Newtonsoft.Json
- Native iOS plugins for Photos integration

Relevant code areas:
- WebSocket & command routing: `Assets/Scripts/Comms/WS_Client.cs`, `Assets/Scripts/Comms/CommandHandler.cs`
- Object lifecycle & control: `Assets/Scripts/ObjectManager.cs`, `Assets/Scripts/ControllableObject.cs`
- UI: `Assets/Scripts/UIManager.cs`
- Screenshot pipeline: `Assets/Capstone Resources/Screenshot Feature/*`

---

## 📂 Project layout (high-level)

- `Assets/Scripts/` — core logic (object management, joysticks, UI, comms)
- `Assets/Capstone Resources/Screenshot Feature` — Screenshot feature
- `Assets/Capstone Resources/Object Detection Model` — Object Detection Model feature
- `Assets/StreamingAssets/` — place P12 certificate(s) and other runtime assets
- `Plugins/` — native iOS plugins (for saving images + WebSocket mTLS for iOS)
- `Scenes/` — Unity scene(s) for the app

---

## 🚀 Getting started (developer)

### Prerequisites
- macOS with Xcode 14+
- Unity 2022.3 LTS (2022.3.62f1 recommended)
- iPhone (iOS 14+ recommended) with ARKit support
- Apple Developer account (for device testing)
- AWS account for S3 bucket (presigned URL generation)
- A WebSocket server that supports mTLS

### Quick install
1. Clone the repo.
2. Open the project in Unity Hub (Unity 2022.3.x).
3. Install required packages (Package Manager): AR Foundation, ARKit XR Plugin, Niantic Lightship (optional), Newtonsoft.Json, TextMeshPro.
4. Put your P12 certificate(s) into `Assets/StreamingAssets/`.
5. Update the default server URL (if required) in `Assets/Scripts/Comms/WS_Client.cs` or via the app settings UI.
6. Switch platform to iOS and build to device via Xcode.

---

## 🔧 Configuration & runtime settings

- Joystick sensitivity: configurable via Settings UI (recommended range: 0.1x — 5.0x). Applies to both local and remote inputs.
- Dead zone threshold: available in Settings to reduce jitter.
- Remote input timeout: virtual inputs zero out if packets stop (default ~200ms).
- Sync safeguards: gallery sync caps list size (default 100 files) to avoid memory/JSON bloat.

Where to change key values:
- WebSocket URL / connection options: `Assets/Scripts/Comms/WS_Client.cs`
- Command parsing & routing: `Assets/Scripts/Comms/CommandHandler.cs`
- Sensitivity / UI bindings: `Assets/Scripts/SettingsMenuController.cs` & `Assets/Scripts/SettingsPanelController.cs`
- Movement & input handling: `Assets/Scripts/ControllableObject.cs`

---

## 🧭 Remote command protocol (summary)

- Messages are JSON objects. See `Assets/Scripts/Comms/WebSocketMessage.cs` for data shapes.
- Movement/rotation vectors are normalized to [-1, 1]:
  - `COMMAND_MOVE`: `[x, y, z]` — x: horizontal, y: vertical, z: depth
  - `COMMAND_ROTATE`: `[x, y, z]` — x: roll, y: pitch, z: yaw
- Commands: SELECT, DELETE, MOVE, ROTATE, SCREENSHOT, S3_SYNC, etc.
- Remote inputs are converted to virtual joystick states and processed every frame (uses `Time.deltaTime`) for consistent behaviour.

---

## 🧪 Testing & debugging

- Local debug console: `DebugViewController` logs WebSocket activity and errors.
- Check `ScreenshotSyncManager` logs for S3 interactions.

---

## 🎮 How to use (end-user)

### Local AR Mode
1. Open the app and allow camera permissions.
2. Tap a furniture button to place a model.
3. Select the placed object to enable joysticks and controls.
4. Use axial/rotary joysticks, depth buttons, and yaw buttons to move/rotate the object.

### Remote Command Mode
1. Connect to a WebSocket server (Settings).
2. Remote devices can send JSON commands to control the selected object.
3. Remote and local inputs are applied via the same input pipeline for consistent results.

### Screenshot & Gallery
- Tap “Screenshot” to capture the current AR scene.
- Screenshots upload to S3 (using presigned URLs) and appear in the Gallery.
- Use Sync to download remote screenshots associated with your username.

---

## 📚 References

- Unity AR Foundation: https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@6.0/manual/index.html
- ARKit documentation: https://developer.apple.com/documentation/arkit/
- Niantic Lightship: https://lightship.dev/
- AWS S3 Presigned URLs: https://docs.aws.amazon.com/AmazonS3/latest/userguide/PresignedUrlUploadObject.html

---

## 👥 Team B13

**CG4002 Capstone Project** — National University of Singapore, Computer Engineering  
Development team:
- Davian Kho Yong Quan (Lead - Communications)
- Low Tjun Lym (Lead - Hardware)
- Ong Wei Xiang (Lead - AI)
- Gandhi Parth Sanjay (Lead — Visualiser)

---
<div align="center">

 ***Made with ❤️ and ☕ using Unity***

</div>

---
