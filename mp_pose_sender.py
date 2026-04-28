import cv2
import time
import math
import json
import socket
import traceback
import threading
import numpy as np
import mediapipe as mp

from pathlib import Path
from typing import Dict, Optional, Tuple
from mediapipe.tasks import python
from mediapipe.tasks.python import vision

# ==========================
# 1. Common config
# ==========================
UNITY_IP = "127.0.0.1"

BODY_PORT = 12000
HEAD_PORT = 5006

CAMERA_INDEX = None
PROJECT_ROOT = Path(__file__).resolve().parent
FACE_MODEL_PATH = PROJECT_ROOT / "models" / "face_landmarker.task"
POSE_MODEL_PATH = PROJECT_ROOT / "models" / "pose_landmarker_full.task"

# ==========================
# 2. MediaPipe Pose config
# ==========================
POSE_MODEL_COMPLEXITY = 1
POSE_MIN_DETECTION_CONFIDENCE = 0.5
POSE_MIN_TRACKING_CONFIDENCE = 0.5

BODY_SCALE = 1.0
BODY_VISIBILITY_THRESHOLD = 0.5

# ==========================
# 3. Face Landmarker config
# ==========================
MAX_NUM_FACES = 1

USE_SMOOTHING = True
SMOOTHING_ALPHA = 0.8

SHOW_WINDOW = False
ENABLE_CONSOLE_LOG = False

PRINT_INTERVAL = 0.3
BODY_PRINT_INTERVAL = 1.0
HEAD_SEND_INTERVAL = 0.033
FACE_DETECT_INTERVAL = 0.05
FACE_SCALE = 0.5

INVERT_YAW = False
INVERT_PITCH = False
INVERT_ROLL = False

# ==========================
# 4. Shared state
# ==========================
latest_face_result = None
face_result_lock = threading.Lock()

smoothed_yaw = 0.0
smoothed_pitch = 0.0
smoothed_roll = 0.0


# MediaPipe Pose landmark indices
NOSE = 0
LEFT_EYE_INNER = 1
LEFT_EYE = 2
LEFT_EYE_OUTER = 3
RIGHT_EYE_INNER = 4
RIGHT_EYE = 5
RIGHT_EYE_OUTER = 6
LEFT_EAR = 7
RIGHT_EAR = 8
MOUTH_LEFT = 9
MOUTH_RIGHT = 10
LEFT_SHOULDER = 11
RIGHT_SHOULDER = 12
LEFT_ELBOW = 13
RIGHT_ELBOW = 14
LEFT_WRIST = 15
RIGHT_WRIST = 16
LEFT_PINKY = 17
RIGHT_PINKY = 18
LEFT_INDEX = 19
RIGHT_INDEX = 20
LEFT_THUMB = 21
RIGHT_THUMB = 22
LEFT_HIP = 23
RIGHT_HIP = 24
LEFT_KNEE = 25
RIGHT_KNEE = 26
LEFT_ANKLE = 27
RIGHT_ANKLE = 28
LEFT_HEEL = 29
RIGHT_HEEL = 30
LEFT_FOOT_INDEX = 31
RIGHT_FOOT_INDEX = 32

SMPL_24_JOINT_NAMES = [
    "pelvis",
    "left_hip",
    "right_hip",
    "spine1",
    "left_knee",
    "right_knee",
    "spine2",
    "left_ankle",
    "right_ankle",
    "spine3",
    "left_foot",
    "right_foot",
    "neck",
    "left_collar",
    "right_collar",
    "head",
    "left_shoulder",
    "right_shoulder",
    "left_elbow",
    "right_elbow",
    "left_wrist",
    "right_wrist",
    "left_hand",
    "right_hand",
]


def clamp_angle(angle: float) -> float:
    while angle > 180.0:
        angle -= 360.0
    while angle < -180.0:
        angle += 360.0
    return angle


def smooth_value(previous: float, current: float, alpha: float) -> float:
    return alpha * previous + (1.0 - alpha) * current


def rotation_matrix_to_euler_angles(rotation_matrix: np.ndarray) -> Tuple[float, float, float]:
    sy = math.sqrt(rotation_matrix[0, 0] ** 2 + rotation_matrix[1, 0] ** 2)
    singular = sy < 1e-6

    if not singular:
        roll = math.atan2(rotation_matrix[2, 1], rotation_matrix[2, 2])
        pitch = math.atan2(-rotation_matrix[2, 0], sy)
        yaw = math.atan2(rotation_matrix[1, 0], rotation_matrix[0, 0])
    else:
        roll = math.atan2(-rotation_matrix[1, 2], rotation_matrix[1, 1])
        pitch = math.atan2(-rotation_matrix[2, 0], sy)
        yaw = 0.0

    yaw_deg = clamp_angle(math.degrees(yaw))
    pitch_deg = clamp_angle(math.degrees(pitch))
    roll_deg = clamp_angle(math.degrees(roll))

    return yaw_deg, pitch_deg, roll_deg


def extract_rotation_matrix(matrix_data) -> Optional[np.ndarray]:
    try:
        mat = np.array(matrix_data, dtype=np.float64)
    except Exception:
        return None

    if mat.shape == (4, 4):
        return mat[:3, :3]

    if mat.shape == (3, 3):
        return mat

    if mat.size == 16:
        mat = mat.reshape(4, 4)
        return mat[:3, :3]

    if mat.size == 9:
        mat = mat.reshape(3, 3)
        return mat

    return None


def extract_head_angles(result) -> Optional[Tuple[float, float, float]]:
    if result is None:
        return None

    if not hasattr(result, "facial_transformation_matrixes"):
        return None

    matrices = result.facial_transformation_matrixes
    if not matrices or len(matrices) == 0:
        return None

    rotation_matrix = extract_rotation_matrix(matrices[0])
    if rotation_matrix is None:
        return None

    return rotation_matrix_to_euler_angles(rotation_matrix)


def face_result_callback(result, output_image, timestamp_ms: int):
    global latest_face_result
    with face_result_lock:
        latest_face_result = result


def landmark_to_array(landmark) -> np.ndarray:
    return np.array([landmark.x, landmark.y, landmark.z], dtype=np.float32)


def midpoint(a: np.ndarray, b: np.ndarray) -> np.ndarray:
    return (a + b) * 0.5


def lerp(a: np.ndarray, b: np.ndarray, t: float) -> np.ndarray:
    return a + (b - a) * t


def build_pose_points(world_landmarks) -> Optional[Dict[int, np.ndarray]]:
    if world_landmarks is None or len(world_landmarks) < 33:
        return None

    return {idx: landmark_to_array(world_landmarks[idx]) for idx in range(len(world_landmarks))}


def hand_center(points: Dict[int, np.ndarray], wrist_idx: int, index_idx: int, pinky_idx: int, thumb_idx: int) -> np.ndarray:
    return (
        points[wrist_idx]
        + points[index_idx]
        + points[pinky_idx]
        + points[thumb_idx]
    ) * 0.25


def head_center(points: Dict[int, np.ndarray]) -> np.ndarray:
    ear_mid = midpoint(points[LEFT_EAR], points[RIGHT_EAR])
    eye_mid = midpoint(points[LEFT_EYE], points[RIGHT_EYE])
    mouth_mid = midpoint(points[MOUTH_LEFT], points[MOUTH_RIGHT])
    return (ear_mid + eye_mid + mouth_mid + points[NOSE]) * 0.25


def convert_pose33_to_smpl24(world_landmarks) -> Optional[np.ndarray]:
    # Keep the outgoing index order aligned with the Unity side's SMPL-24-style
    # array contract: pelvis -> limbs -> spine/chest -> neck/head -> arms/hands.
    points = build_pose_points(world_landmarks)
    if points is None:
        return None

    pelvis = midpoint(points[LEFT_HIP], points[RIGHT_HIP])
    chest = midpoint(points[LEFT_SHOULDER], points[RIGHT_SHOULDER])
    neck = lerp(chest, head_center(points), 0.35)
    spine1 = lerp(pelvis, chest, 1.0 / 3.0)
    spine2 = lerp(pelvis, chest, 2.0 / 3.0)
    spine3 = chest

    left_hand = hand_center(points, LEFT_WRIST, LEFT_INDEX, LEFT_PINKY, LEFT_THUMB)
    right_hand = hand_center(points, RIGHT_WRIST, RIGHT_INDEX, RIGHT_PINKY, RIGHT_THUMB)
    left_foot = midpoint(points[LEFT_HEEL], points[LEFT_FOOT_INDEX])
    right_foot = midpoint(points[RIGHT_HEEL], points[RIGHT_FOOT_INDEX])

    smpl24 = np.array([
        pelvis,                                  # 0 pelvis
        points[LEFT_HIP],                        # 1 left_hip
        points[RIGHT_HIP],                       # 2 right_hip
        spine1,                                  # 3 spine1
        points[LEFT_KNEE],                       # 4 left_knee
        points[RIGHT_KNEE],                      # 5 right_knee
        spine2,                                  # 6 spine2
        points[LEFT_ANKLE],                      # 7 left_ankle
        points[RIGHT_ANKLE],                     # 8 right_ankle
        spine3,                                  # 9 spine3 / chest
        left_foot,                               # 10 left_foot
        right_foot,                              # 11 right_foot
        neck,                                    # 12 neck
        lerp(neck, points[LEFT_SHOULDER], 0.5),  # 13 left_collar
        lerp(neck, points[RIGHT_SHOULDER], 0.5), # 14 right_collar
        head_center(points),                     # 15 head
        points[LEFT_SHOULDER],                   # 16 left_shoulder
        points[RIGHT_SHOULDER],                  # 17 right_shoulder
        points[LEFT_ELBOW],                      # 18 left_elbow
        points[RIGHT_ELBOW],                     # 19 right_elbow
        points[LEFT_WRIST],                      # 20 left_wrist
        points[RIGHT_WRIST],                     # 21 right_wrist
        left_hand,                               # 22 left_hand
        right_hand,                              # 23 right_hand
    ], dtype=np.float32)

    smpl24 -= pelvis
    smpl24 *= BODY_SCALE
    return smpl24


def convert_to_unity_coordinates(joints_24: np.ndarray):
    unity_joints = []
    for joint in joints_24:
        x = float(joint[0])
        y = float(-joint[1])
        z = float(-joint[2])
        unity_joints.append([x, y, z])
    return unity_joints


def open_camera(index: Optional[int]):
    backends = [
        ("CAP_DSHOW", cv2.CAP_DSHOW),
        ("CAP_MSMF", cv2.CAP_MSMF),
        ("DEFAULT", None),
    ]

    indices = [index] if index is not None else list(range(6))

    for current_index in indices:
        for backend_name, backend in backends:
            cap = cv2.VideoCapture(current_index) if backend is None else cv2.VideoCapture(current_index, backend)
            if not cap.isOpened():
                cap.release()
                continue

            ok, frame = cap.read()
            if ok and frame is not None:
                print(f"Webcam started with index {current_index} backend {backend_name}")
                return cap

            cap.release()

    raise RuntimeError(
        "Failed to open a usable camera. "
        "Check Windows Camera privacy settings, device connection, or set CAMERA_INDEX explicitly."
    )


def main():
    global smoothed_yaw, smoothed_pitch, smoothed_roll

    if not FACE_MODEL_PATH.is_file():
        raise FileNotFoundError(f"Missing face landmarker model: {FACE_MODEL_PATH}")
    if not POSE_MODEL_PATH.is_file():
        raise FileNotFoundError(f"Missing pose landmarker model: {POSE_MODEL_PATH}")

    print("Initializing MediaPipe Pose...")
    pose_base_options = python.BaseOptions(model_asset_path=str(POSE_MODEL_PATH))
    pose_options = vision.PoseLandmarkerOptions(
        base_options=pose_base_options,
        running_mode=vision.RunningMode.VIDEO,
        num_poses=1,
        min_pose_detection_confidence=POSE_MIN_DETECTION_CONFIDENCE,
        min_pose_presence_confidence=POSE_MIN_DETECTION_CONFIDENCE,
        min_tracking_confidence=POSE_MIN_TRACKING_CONFIDENCE,
        output_segmentation_masks=False,
    )
    print("MediaPipe Pose initialized")

    print("Initializing Face Landmarker...")
    base_options = python.BaseOptions(model_asset_path=str(FACE_MODEL_PATH))
    face_options = vision.FaceLandmarkerOptions(
        base_options=base_options,
        running_mode=vision.RunningMode.LIVE_STREAM,
        num_faces=MAX_NUM_FACES,
        output_face_blendshapes=False,
        output_facial_transformation_matrixes=True,
        result_callback=face_result_callback,
    )

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    print(f"UDP ready -> body:{BODY_PORT}, head:{HEAD_PORT}")

    cap = open_camera(CAMERA_INDEX)

    prev_print_time = 0.0
    prev_body_print_time = 0.0
    prev_head_send_time = 0.0
    prev_face_detect_time = 0.0
    body_packet_count = 0

    try:
        with vision.PoseLandmarker.create_from_options(pose_options) as pose_landmarker, \
             vision.FaceLandmarker.create_from_options(face_options) as face_landmarker:
            print("Face Landmarker initialized")
            print("ESC key to exit")

            while True:
                ok, frame = cap.read()
                if not ok or frame is None:
                    continue

                now = time.time()
                body_frame = frame
                face_frame = cv2.flip(frame, 1)

                if FACE_SCALE != 1.0:
                    small_face_frame = cv2.resize(
                        face_frame,
                        None,
                        fx=FACE_SCALE,
                        fy=FACE_SCALE,
                        interpolation=cv2.INTER_AREA,
                    )
                else:
                    small_face_frame = face_frame

                if (now - prev_face_detect_time) >= FACE_DETECT_INTERVAL:
                    rgb_face_frame = cv2.cvtColor(small_face_frame, cv2.COLOR_BGR2RGB)
                    mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb_face_frame)
                    face_landmarker.detect_async(mp_image, int(now * 1000))
                    prev_face_detect_time = now

                try:
                    rgb_body = cv2.cvtColor(body_frame, cv2.COLOR_BGR2RGB)
                    pose_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb_body)
                    pose_result = pose_landmarker.detect_for_video(pose_image, int(now * 1000))

                    if pose_result.pose_world_landmarks:
                        smpl24 = convert_pose33_to_smpl24(pose_result.pose_world_landmarks[0])
                        if smpl24 is not None:
                            body_msg = json.dumps(convert_to_unity_coordinates(smpl24)).encode("utf-8")
                            sock.sendto(body_msg, (UNITY_IP, BODY_PORT))
                            body_packet_count += 1
                            if (now - prev_body_print_time) >= BODY_PRINT_INTERVAL:
                                print(f"[BODY] packets sent: {body_packet_count}")
                                prev_body_print_time = now
                except Exception:
                    print("[BODY] ERROR OCCURRED")
                    traceback.print_exc()

                with face_result_lock:
                    current_face_result = latest_face_result

                face_detected = False
                matrix_ok = False

                if current_face_result is not None:
                    if current_face_result.face_landmarks and len(current_face_result.face_landmarks) > 0:
                        face_detected = True

                    angles = extract_head_angles(current_face_result)
                    if angles is not None:
                        matrix_ok = True
                        yaw, pitch, roll = angles

                        if INVERT_YAW:
                            yaw = -yaw
                        if INVERT_PITCH:
                            pitch = -pitch
                        if INVERT_ROLL:
                            roll = -roll

                        if USE_SMOOTHING:
                            smoothed_yaw = smooth_value(smoothed_yaw, yaw, SMOOTHING_ALPHA)
                            smoothed_pitch = smooth_value(smoothed_pitch, pitch, SMOOTHING_ALPHA)
                            smoothed_roll = smooth_value(smoothed_roll, roll, SMOOTHING_ALPHA)
                        else:
                            smoothed_yaw = yaw
                            smoothed_pitch = pitch
                            smoothed_roll = roll

                if matrix_ok and (now - prev_head_send_time) >= HEAD_SEND_INTERVAL:
                    head_payload = {
                        "yaw": round(smoothed_yaw, 4),
                        "pitch": round(smoothed_pitch, 4),
                        "roll": round(smoothed_roll, 4),
                    }
                    head_msg = json.dumps(head_payload).encode("utf-8")
                    sock.sendto(head_msg, (UNITY_IP, HEAD_PORT))
                    prev_head_send_time = now

                if SHOW_WINDOW:
                    display = face_frame.copy()
                    y_base = 40

                    if face_detected:
                        cv2.putText(
                            display, "Face Detected", (20, y_base),
                            cv2.FONT_HERSHEY_SIMPLEX, 1.0, (0, 255, 0), 2,
                        )
                        y_base += 40

                    if matrix_ok:
                        cv2.putText(
                            display, "Matrix OK", (20, y_base),
                            cv2.FONT_HERSHEY_SIMPLEX, 0.9, (255, 0, 0), 2,
                        )
                        y_base += 40

                        cv2.putText(
                            display, f"Yaw   : {smoothed_yaw:7.2f}", (20, y_base + 20),
                            cv2.FONT_HERSHEY_SIMPLEX, 0.8, (0, 255, 255), 2,
                        )
                        cv2.putText(
                            display, f"Pitch : {smoothed_pitch:7.2f}", (20, y_base + 55),
                            cv2.FONT_HERSHEY_SIMPLEX, 0.8, (0, 255, 255), 2,
                        )
                        cv2.putText(
                            display, f"Roll  : {smoothed_roll:7.2f}", (20, y_base + 90),
                            cv2.FONT_HERSHEY_SIMPLEX, 0.8, (0, 255, 255), 2,
                        )

                    cv2.imshow("MediaPipe Pose + Face Sender", display)

                if ENABLE_CONSOLE_LOG and matrix_ok and (now - prev_print_time) >= PRINT_INTERVAL:
                    print(
                        f"[HEAD] Yaw: {smoothed_yaw:7.2f} | "
                        f"Pitch: {smoothed_pitch:7.2f} | "
                        f"Roll: {smoothed_roll:7.2f}"
                    )
                    prev_print_time = now

                key = cv2.waitKey(1) & 0xFF
                if key == 27:
                    break

    except Exception:
        print("ERROR OCCURRED IN MAIN LOOP")
        traceback.print_exc()

    finally:
        try:
            cap.release()
        except Exception:
            pass

        try:
            sock.close()
        except Exception:
            pass

        cv2.destroyAllWindows()
        print("Finished")


if __name__ == "__main__":
    main()
