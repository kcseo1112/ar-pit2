import cv2
import time
import math
import json
import socket
import traceback
import threading
import numpy as np
import mediapipe as mp

from typing import Optional, Tuple
from romp import ROMP, WebcamVideoStream
import romp
from mediapipe.tasks import python
from mediapipe.tasks.python import vision

# ==========================
# 1️⃣ 공통 설정
# ==========================
UNITY_IP = "127.0.0.1"

BODY_PORT = 5005   # ROMP body joint
HEAD_PORT = 5006   # head yaw/pitch/roll

CAMERA_INDEX = 0
MODEL_PATH = "models/face_landmarker.task"

# ==========================
# 2️⃣ ROMP 설정
# ==========================
settings = romp.romp_settings()
settings.mode = 'webcam'
settings.GPU = 0
settings.onnx = False

# ==========================
# 3️⃣ Face Landmarker 설정
# ==========================
MAX_NUM_FACES = 1

USE_SMOOTHING = True
SMOOTHING_ALPHA = 0.8

PRINT_INTERVAL = 0.1
HEAD_SEND_INTERVAL = 0.02   # 약 50 FPS
SHOW_WINDOW = True

# 필요 시 Python 쪽에서 반전
INVERT_YAW = False
INVERT_PITCH = False
INVERT_ROLL = False

# ==========================
# 4️⃣ 전역 변수 (Face callback 결과 저장)
# ==========================
latest_face_result = None
face_result_lock = threading.Lock()

smoothed_yaw = 0.0
smoothed_pitch = 0.0
smoothed_roll = 0.0


# ==========================
# 5️⃣ 유틸 함수
# ==========================
def clamp_angle(angle: float) -> float:
    while angle > 180.0:
        angle -= 360.0
    while angle < -180.0:
        angle += 360.0
    return angle


def smooth_value(previous: float, current: float, alpha: float) -> float:
    return alpha * previous + (1.0 - alpha) * current


def rotation_matrix_to_euler_angles(rotation_matrix: np.ndarray) -> Tuple[float, float, float]:
    """
    3x3 회전행렬 -> yaw, pitch, roll (degree)

    yaw   : 좌우 회전
    pitch : 위아래 회전
    roll  : 고개 기울임
    """
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


# ==========================
# 6️⃣ 메인
# ==========================
def main():
    global smoothed_yaw, smoothed_pitch, smoothed_roll

    print("Initializing ROMP...")
    body_model = ROMP(settings)
    print("ROMP initialized")

    print("Initializing Face Landmarker...")
    base_options = python.BaseOptions(model_asset_path=MODEL_PATH)
    face_options = vision.FaceLandmarkerOptions(
        base_options=base_options,
        running_mode=vision.RunningMode.LIVE_STREAM,
        num_faces=MAX_NUM_FACES,
        output_face_blendshapes=False,
        output_facial_transformation_matrixes=True,
        result_callback=face_result_callback
    )

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    print(f"UDP ready -> body:{BODY_PORT}, head:{HEAD_PORT}")

    webcam = WebcamVideoStream(CAMERA_INDEX).start()
    print("Webcam started")

    prev_print_time = 0.0
    prev_head_send_time = 0.0

    try:
        with vision.FaceLandmarker.create_from_options(face_options) as face_landmarker:
            print("Face Landmarker initialized")
            print("ESC 키를 누르면 종료")

            while True:
                frame = webcam.read()

                if frame is None:
                    print("No frame")
                    continue

                # -----------------------------------
                # A. ROMP 처리용 원본 프레임
                # -----------------------------------
                body_frame = frame

                # -----------------------------------
                # B. Face 처리용 좌우반전 프레임
                # -----------------------------------
                face_frame = cv2.flip(frame, 1)
                rgb_face_frame = cv2.cvtColor(face_frame, cv2.COLOR_BGR2RGB)

                mp_image = mp.Image(
                    image_format=mp.ImageFormat.SRGB,
                    data=rgb_face_frame
                )

                timestamp_ms = int(time.time() * 1000)
                face_landmarker.detect_async(mp_image, timestamp_ms)

                # ===================================
                # 1. ROMP body 추론 및 송신
                # ===================================
                try:
                    outputs = body_model(body_frame)

                    if outputs is not None:
                        if 'smpl_joints' in outputs:
                            joints = outputs['smpl_joints']
                        elif 'joints' in outputs:
                            joints = outputs['joints']
                        else:
                            joints = None

                        if joints is not None and len(joints) > 0:
                            joints = np.array(joints)

                            if len(joints.shape) >= 2 and joints.shape[1] >= 24:
                                smpl_24 = joints[0][:24]

                                unity_joints = []
                                for joint in smpl_24:
                                    x = float(joint[0])
                                    y = float(-joint[1])
                                    z = float(-joint[2])   # Unity 좌표계 반전
                                    unity_joints.append([x, y, z])

                                body_msg = json.dumps(unity_joints).encode("utf-8")
                                sock.sendto(body_msg, (UNITY_IP, BODY_PORT))
                except Exception:
                    print("[BODY] ERROR OCCURRED")
                    traceback.print_exc()

                # ===================================
                # 2. Face head 추론 및 송신
                # ===================================
                current_face_result = None
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

                now = time.time()
                if matrix_ok and (now - prev_head_send_time) >= HEAD_SEND_INTERVAL:
                    head_payload = {
                        "yaw": round(smoothed_yaw, 4),
                        "pitch": round(smoothed_pitch, 4),
                        "roll": round(smoothed_roll, 4)
                    }

                    head_msg = json.dumps(head_payload).encode("utf-8")
                    sock.sendto(head_msg, (UNITY_IP, HEAD_PORT))
                    prev_head_send_time = now

                # ===================================
                # 3. 화면 출력
                # ===================================
                if SHOW_WINDOW:
                    display = face_frame.copy()
                    y_base = 40

                    if face_detected:
                        cv2.putText(
                            display, "Face Detected", (20, y_base),
                            cv2.FONT_HERSHEY_SIMPLEX, 1.0, (0, 255, 0), 2
                        )
                        y_base += 40

                    if matrix_ok:
                        cv2.putText(
                            display, "Matrix OK", (20, y_base),
                            cv2.FONT_HERSHEY_SIMPLEX, 0.9, (255, 0, 0), 2
                        )
                        y_base += 40

                        cv2.putText(
                            display, f"Yaw   : {smoothed_yaw:7.2f}", (20, y_base + 20),
                            cv2.FONT_HERSHEY_SIMPLEX, 0.8, (0, 255, 255), 2
                        )
                        cv2.putText(
                            display, f"Pitch : {smoothed_pitch:7.2f}", (20, y_base + 55),
                            cv2.FONT_HERSHEY_SIMPLEX, 0.8, (0, 255, 255), 2
                        )
                        cv2.putText(
                            display, f"Roll  : {smoothed_roll:7.2f}", (20, y_base + 90),
                            cv2.FONT_HERSHEY_SIMPLEX, 0.8, (0, 255, 255), 2
                        )

                    cv2.imshow("ROMP + Face Integrated Sender", display)

                # ===================================
                # 4. 콘솔 출력
                # ===================================
                if matrix_ok and (now - prev_print_time) >= PRINT_INTERVAL:
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
            webcam.stop()
        except Exception:
            pass

        try:
            sock.close()
        except Exception:
            pass

        cv2.destroyAllWindows()
        print("종료")


if __name__ == "__main__":
    main()