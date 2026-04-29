# realtime_mediapipe_d435i

This project sends body joints and head rotation to Unity over UDP.

- Body sender: `mp_pose_sender.py`
- Body model: MediaPipe `PoseLandmarker` `pose_world_landmarks` (33 landmarks)
- Head model: MediaPipe `FaceLandmarker`
- Body UDP port: `12000`
- Head UDP port: `5006`

`mp_pose_sender.py` is the current main runtime path. The older
`romp_face_udp_integrated.py` file is kept as a previous ROMP-based reference,
but new body tracking and fitting work should be added to the MediaPipe sender.

## Unity Reference

Reference Unity scripts were copied into [unity_reference](/home/kangj/projects/realtime_mediapipe_d435i/unity_reference).

- [PoseReceiverUDP.cs](/home/kangj/projects/realtime_mediapipe_d435i/unity_reference/PoseReceiverUDP.cs)
- [JointVisualizer.cs](/home/kangj/projects/realtime_mediapipe_d435i/unity_reference/JointVisualizer.cs)
- [AvatarRetarget.cs](/home/kangj/projects/realtime_mediapipe_d435i/unity_reference/AvatarRetarget.cs)

Unity expects a JSON array shaped like:

```json
[[x,y,z],[x,y,z], ... 24 items ...]
```

The first 24 items are always the fixed SMPL-style body joints. The sender may
append one extra body-fit item after those joints:

```json
[[x,y,z], ... 24 joints ..., [screenShoulderWidth, screenTorsoLength, 0]]
```

`PoseReceiverUDP.cs` keeps the 24 joints for retargeting and passes the optional
screen-space shoulder width to `ClothesRetarget.cs`. `ClothesRetarget.cs` uses
that value to scale the clothing root, so the clothes grow when the user moves
closer to the camera and shrink when the user moves away.

## Joint Order

The outgoing array order is the SMPL-style 24-joint layout below.

| Index | Joint Name | MediaPipe Pose source |
| --- | --- | --- |
| 0 | pelvis | midpoint(left_hip, right_hip) |
| 1 | left_hip | left_hip |
| 2 | right_hip | right_hip |
| 3 | spine1 | pelvis -> chest 1/3 |
| 4 | left_knee | left_knee |
| 5 | right_knee | right_knee |
| 6 | spine2 | pelvis -> chest 2/3 |
| 7 | left_ankle | left_ankle |
| 8 | right_ankle | right_ankle |
| 9 | spine3 | midpoint(left_shoulder, right_shoulder) |
| 10 | left_foot | midpoint(left_heel, left_foot_index) |
| 11 | right_foot | midpoint(right_heel, right_foot_index) |
| 12 | neck | chest -> head 0.35 interpolation |
| 13 | left_collar | neck -> left_shoulder 0.5 interpolation |
| 14 | right_collar | neck -> right_shoulder 0.5 interpolation |
| 15 | head | average(nose, eyes, ears, mouth center) |
| 16 | left_shoulder | left_shoulder |
| 17 | right_shoulder | right_shoulder |
| 18 | left_elbow | left_elbow |
| 19 | right_elbow | right_elbow |
| 20 | left_wrist | left_wrist |
| 21 | right_wrist | right_wrist |
| 22 | left_hand | average(wrist, index, pinky, thumb) |
| 23 | right_hand | average(wrist, index, pinky, thumb) |

Notes from Unity analysis:

- `PoseReceiverUDP.cs` only validates array length and does not attach names to indices.
- `JointVisualizer.cs` treats joint `0` as pelvis and renders all joints relative to it.
- `AvatarRetarget.cs` uses indices `0, 1, 2, 12, 15, 16, 17, 18, 19, 20, 21` as the main driving joints.
- `AvatarRetarget.cs` applies arm and leg chains with left/right swapped inside the script. The sender keeps the standard SMPL-style order instead of embedding Unity-specific mirroring in the network protocol.

## Setup

Windows CMD / PowerShell usage:

```powershell
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r requirements.txt
.\.venv\Scripts\python.exe check_env.py
.\.venv\Scripts\python.exe mp_pose_sender.py
```

`setup.sh` and `run.sh` are Linux helpers and should not be used on Windows.

WSL Ubuntu usage:

```bash
chmod +x setup.sh run.sh
./setup.sh
./run.sh
```

What `setup.sh` does:

- creates `.venv`
- upgrades `pip`, `setuptools`, `wheel`
- installs packages from `requirements.txt`
- runs `check_env.py`

What `run.sh` does:

- activates `.venv`
- runs `check_env.py`
- starts `mp_pose_sender.py`

## Environment Checks

`check_env.py` verifies:

- required Python packages: `mediapipe`, `numpy`, `opencv-python`
- `models/face_landmarker.task` presence
- `models/pose_landmarker_full.task` presence
- camera availability check for the current OS
- optional RealSense helpers: `pyrealsense2`, `rs-enumerate-devices`

## Model File

Expected model path:

```text
models/face_landmarker.task
models/pose_landmarker_full.task
```

These files are required before the sender can run.

## D435i Notes

Current sender code uses a standard webcam through OpenCV. D435i-specific capture is not wired into `mp_pose_sender.py` yet.

Current machine status observed during setup authoring:

- `models/face_landmarker.task`: present
- `pyrealsense2`: not installed
- `rs-enumerate-devices`: not found
- `apt-cache search librealsense`: no package result returned

Implications:

- automatic Python setup is supported for the webcam-based sender
- D435i support still needs manual system installation on WSL Ubuntu

Manual D435i setup is likely required for:

- Intel RealSense SDK / `librealsense2`
- device permissions and USB passthrough to WSL
- optional Python binding `pyrealsense2`

If you want D435i input later, add a separate capture layer after the RealSense SDK is installed and confirmed working with `rs-enumerate-devices`.
