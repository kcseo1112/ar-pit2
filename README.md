# AR FitRoom 진행 현황

Unity 기반 AR 피팅룸 프로젝트입니다. 현재는 ROMP/UDP 포즈 흐름 위에 스마트미러 스타일 FitRoom UI와 MySQL 연동용 Flask API 초안이 추가된 상태입니다.

## 현재 구현된 내용

- Unity `FitRoomMainUI`를 고급 스마트미러/AR 피팅룸 UI로 리팩터링했습니다.
- 상의/하의 탭과 하단 캐러셀은 기존 `OutfitManager.SelectUpper(index)`, `SelectLower(index)` 선택 구조를 유지합니다.
- 중앙 `PreviewFrame`에는 버튼 없이 실제 AR/3D 피팅 화면이 보이도록 테두리 중심으로 구성했습니다.
- 씬에 직접 배치한 `FitRoomMainUI`의 `Upper Thumbnails`, `Lower Thumbnails` 배열로 카드 썸네일을 유지합니다.
- 선택한 상의/하의 썸네일은 하단 카드, 좌측 현재 착용 정보, 우측 카테고리 안내 이미지에 함께 표시됩니다.
- 모자/신발 탭은 아직 실제 의상 배열이 없으므로 Coming Soon/placeholder 상태입니다.
- 로그인/회원가입 UI는 Unity `InputField`로 입력 가능하며 Flask API 호출 코드까지 연결했습니다.
- 상단 사용자 상태는 로그인/회원가입 성공 시 `이름님 / 로그인됨`으로 갱신됩니다.
- Python Flask API 초안은 `Backend/ar_fit_api.py`에 있습니다.
- MySQL 스키마 이름은 `ar_fit` 기준이며 사용자, 카테고리, 옷, 찜 테이블 연동을 목표로 합니다.

## Python / MySQL API

백엔드 API 파일:

```text
Backend/ar_fit_api.py
```

필요 패키지:

```powershell
cd Backend
python -m pip install -r requirements.txt
```

실행 예시:

```powershell
python ar_fit_api.py
```

기본 API 주소:

```text
http://127.0.0.1:5000
```

현재 작성된 주요 API:

- `GET /api/health`: Flask/API와 MySQL 연결 확인
- `GET /api/categories`: 카테고리 목록 조회
- `GET /api/outfits?category_code=upper`: 의상 목록 조회
- `GET /api/outfits/<outfit_id>`: 의상 상세 조회
- `POST /api/auth/register`: 회원가입
- `POST /api/auth/login`: 로그인
- `GET /api/users/<user_id>/favorites`: 사용자 찜 목록 조회
- `POST /api/favorites/toggle`: 찜 토글
- `POST /api/dev/seed`: 개발용 초기 카테고리/샘플 의상 데이터 입력

Unity `FitRoomMainUI`의 기본 API 주소는 다음 값입니다.

```text
http://127.0.0.1:5000
```

## 앞으로 구현해야 할 내용

- 로그인 UI 디자인을 아직 더 다듬어야 합니다. 현재는 테스트 가능한 입력/버튼 연결이 우선입니다.
- 로그인 실패/회원가입 실패 메시지를 UI 텍스트로 표시해야 합니다. 현재는 `Debug.LogWarning` 중심입니다.
- 로그인 세션 또는 토큰 저장 방식이 필요합니다. 현재는 런타임 메모리에 사용자 ID/이름만 보관합니다.
- 찜 버튼을 MySQL `favorites` API와 완전히 연결해야 합니다. 현재 UI 찜은 기존 메모리 토글 구조가 남아 있습니다.
- DB의 의상 설명, 색상, 성별, 카테고리 정보를 Unity 우측 안내 패널과 필터/정렬 UI에 연결해야 합니다.
- 필터/정렬 버튼은 아직 UI만 있으며 실제 필터 조건 선택 UI와 API query 연결이 필요합니다.
- `thumbnail_url`을 Unity에서 원격 이미지로 로드하거나, DB 의상 데이터와 Unity Inspector 썸네일 배열의 매핑 규칙을 확정해야 합니다.
- `unity_category_code + unity_outfit_index`를 기준으로 DB 의상과 `OutfitManager` 인덱스를 안정적으로 동기화해야 합니다.
- 실제 배포 전 DB 비밀번호와 SECRET_KEY는 코드에 직접 두지 않고 환경변수로 빼야 합니다.

## 기존 AR / UDP 흐름

기존 ROMP/UDP/Retarget 흐름은 유지됩니다.

# realtime_mediapipe_d435i

This project sends body joints and head rotation to Unity over UDP.

- Body sender: `mp_pose_sender.py`
- Body model: MediaPipe `PoseLandmarker` `pose_world_landmarks` (33 landmarks)
- Head model: MediaPipe `FaceLandmarker`
- Body UDP port: `12000`
- Head UDP port: `5006`

## Unity Reference

Reference Unity scripts were copied into [unity_reference](/home/kangj/projects/realtime_mediapipe_d435i/unity_reference).

- [PoseReceiverUDP.cs](/home/kangj/projects/realtime_mediapipe_d435i/unity_reference/PoseReceiverUDP.cs)
- [JointVisualizer.cs](/home/kangj/projects/realtime_mediapipe_d435i/unity_reference/JointVisualizer.cs)
- [AvatarRetarget.cs](/home/kangj/projects/realtime_mediapipe_d435i/unity_reference/AvatarRetarget.cs)

Unity expects a JSON array shaped like:

```json
[[x,y,z],[x,y,z], ... 24 items ...]
```

`PoseReceiverUDP.cs` parses exactly 24 joints, so the Python sender keeps a fixed 24-joint output.

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
