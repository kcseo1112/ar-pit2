import cv2
import time
import math
import json
import socket
import struct
import traceback
import threading
import numpy as np
import mediapipe as mp
import torch
import romp
import pyrealsense2 as rs

from pathlib import Path
from typing import Optional, Tuple
from romp import ROMP
from mediapipe.tasks import python
from mediapipe.tasks.python import vision

# ==========================
# 1. Common config
# ==========================
UNITY_IP = "127.0.0.1"

BODY_PORT = 12000
HEAD_PORT = 5006
FRAME_PORT = 13000
FRAME_JPEG_QUALITY = 70

PROJECT_ROOT = Path(__file__).resolve().parent
FACE_MODEL_PATH = PROJECT_ROOT / "models" / "face_landmarker.task"

# Lower capture resolution can improve FPS when webcam capture, decode, or
# image preprocessing is a bottleneck. Try 640x480 first, then 424x240/320x240.
CAMERA_WIDTH = 640
CAMERA_HEIGHT = 480
CAMERA_FPS = 30

# ==========================
# 2. ROMP config
# ==========================
settings = romp.romp_settings()
settings.mode = "webcam"
settings.GPU = 0
settings.onnx = False
settings.show = False
settings.render_mesh = False
settings.save_video = False
settings.show_largest = True

BODY_SCALE = 1.0
NORMALIZE_BODY_TO_PELVIS = True

# ==========================
# 3. Face Landmarker config
# ==========================
MAX_NUM_FACES = 1

USE_SMOOTHING = True
SMOOTHING_ALPHA = 0.8

SHOW_WINDOW = True
MIRROR_PREVIEW = True
PORTRAIT_PREVIEW = True
PREVIEW_HEIGHT = 720
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


class LatestJpegServer:
    def __init__(self, host="127.0.0.1", port=FRAME_PORT):
        self.host = host
        self.port = port
        self.latest_jpeg = None
        self.lock = threading.Lock()
        self.running = False
        self.thread = None

    def start(self):
        self.running = True
        self.thread = threading.Thread(target=self._capture_loop, daemon=True)
        self.thread.start()
        return self

    def update_frame(self, bgr_frame):
        ok, encoded = cv2.imencode(
            ".jpg",
            bgr_frame,
            [int(cv2.IMWRITE_JPEG_QUALITY), FRAME_JPEG_QUALITY],
        )
        if not ok:
            return

        with self.lock:
            self.latest_jpeg = encoded.tobytes()

    def _capture_loop(self):
        server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        server.bind((self.host, self.port))
        server.listen(1)
        server.settimeout(0.5)

        print(f"[FRAME] TCP server started on {self.host}:{self.port}")

        while self.running:
            client = None
            try:
                client, addr = server.accept()
                print(f"[FRAME] Unity connected: {addr}")

                while self.running:
                    with self.lock:
                        jpeg = self.latest_jpeg

                    if jpeg is None:
                        time.sleep(0.005)
                        continue

                    client.sendall(struct.pack("<I", len(jpeg)))
                    client.sendall(jpeg)
                    time.sleep(1.0 / 30.0)
            except socket.timeout:
                continue
            except Exception as exc:
                if self.running:
                    print("[FRAME] client disconnected:", exc)
            finally:
                try:
                    if client is not None:
                        client.close()
                except Exception:
                    pass

        server.close()

    def stop(self):
        self.running = False
        if self.thread is not None:
            self.thread.join(timeout=1.0)


class RealSenseCapture:
    def __init__(self, width=640, height=480, fps=30):
        self.width = width
        self.height = height
        self.fps = fps
        self.pipeline = rs.pipeline()
        self.config = rs.config()
        self.align = rs.align(rs.stream.color)
        self.depth_scale = None

    def start(self):
        self.config.enable_stream(
            rs.stream.color,
            self.width,
            self.height,
            rs.format.bgr8,
            self.fps,
        )
        self.config.enable_stream(
            rs.stream.depth,
            self.width,
            self.height,
            rs.format.z16,
            self.fps,
        )

        profile = self.pipeline.start(self.config)
        self.depth_scale = profile.get_device().first_depth_sensor().get_depth_scale()
        print(f"[D435i] RealSense started: {self.width}x{self.height} @ {self.fps} FPS")
        print("[D435i] depth_scale:", self.depth_scale)
        return self

    def read(self):
        frames = self.pipeline.wait_for_frames()
        aligned_frames = self.align.process(frames)

        color_frame = aligned_frames.get_color_frame()
        depth_frame = aligned_frames.get_depth_frame()

        if not color_frame or not depth_frame:
            return None, None

        color = np.asanyarray(color_frame.get_data())
        return color, depth_frame

    def stop(self):
        self.pipeline.stop()


def get_depth_median(depth_frame, x, y, radius=4):
    if depth_frame is None:
        return -1.0

    values = []
    width = depth_frame.get_width()
    height = depth_frame.get_height()
    cx = int(round(x))
    cy = int(round(y))

    for yy in range(cy - radius, cy + radius + 1):
        for xx in range(cx - radius, cx + radius + 1):
            if xx < 0 or yy < 0 or xx >= width or yy >= height:
                continue

            distance = depth_frame.get_distance(xx, yy)
            if 0.2 < distance < 5.0:
                values.append(distance)

    if not values:
        return -1.0

    return float(np.median(values))


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


def extract_romp_smpl24(outputs) -> Optional[np.ndarray]:
    if outputs is None:
        return None

    joints = None
    for key in ("smpl_joints", "joints"):
        if key in outputs:
            joints = outputs[key]
            break

    if joints is None:
        return None

    joints = np.asarray(joints, dtype=np.float32)

    if joints.ndim == 3:
        if joints.shape[0] < 1 or joints.shape[1] < 24 or joints.shape[2] < 3:
            return None
        smpl24 = joints[0, :24, :3]
    elif joints.ndim == 2:
        if joints.shape[0] < 24 or joints.shape[1] < 3:
            return None
        smpl24 = joints[:24, :3]
    else:
        return None

    smpl24 = smpl24.copy()
    if NORMALIZE_BODY_TO_PELVIS:
        smpl24 -= smpl24[0].copy()

    smpl24 *= BODY_SCALE
    return smpl24


def extract_root_pixel(outputs) -> Optional[Tuple[float, float]]:
    if outputs is None:
        return None

    for key in ("pj2d_org", "pj2d", "joints_2d", "verts_camed_org"):
        if key not in outputs:
            continue

        points = np.asarray(outputs[key], dtype=np.float32)
        if points.ndim == 3 and points.shape[0] >= 1 and points.shape[1] >= 1 and points.shape[2] >= 2:
            return float(points[0, 0, 0]), float(points[0, 0, 1])
        if points.ndim == 2 and points.shape[0] >= 1 and points.shape[1] >= 2:
            return float(points[0, 0]), float(points[0, 1])

    return None


def convert_to_unity_coordinates(joints_24: np.ndarray):
    unity_joints = []
    for joint in joints_24:
        x = float(joint[0])
        y = float(-joint[1])
        z = float(-joint[2])
        unity_joints.append([x, y, z])
    return unity_joints


def build_body_fit_metric(joints_24: np.ndarray, root_pixel=None, root_depth_m=-1.0) -> Optional[list]:
    if joints_24 is None or len(joints_24) < 18:
        return None

    shoulder_width = float(np.linalg.norm(joints_24[16] - joints_24[17]))
    if shoulder_width <= 0.0001:
        return None

    root_x_px = -1.0
    root_y_px = -1.0
    if root_pixel is not None:
        root_x_px = float(root_pixel[0])
        root_y_px = float(root_pixel[1])

    return [
        shoulder_width,
        root_x_px,
        root_y_px,
        float(root_depth_m),
        float(CAMERA_WIDTH),
        float(CAMERA_HEIGHT),
    ]


def make_preview_frame(frame: np.ndarray) -> np.ndarray:
    preview = frame
    if MIRROR_PREVIEW:
        preview = cv2.flip(preview, 1)

    if PORTRAIT_PREVIEW:
        height, width = preview.shape[:2]
        target_width = int(height * 9 / 16)
        if 0 < target_width < width:
            start_x = (width - target_width) // 2
            preview = preview[:, start_x:start_x + target_width]

    if PREVIEW_HEIGHT:
        height, width = preview.shape[:2]
        if height > 0:
            preview_width = max(1, int(width * (PREVIEW_HEIGHT / height)))
            preview = cv2.resize(preview, (preview_width, PREVIEW_HEIGHT), interpolation=cv2.INTER_LINEAR)

    return preview


def main():
    global smoothed_yaw, smoothed_pitch, smoothed_roll

    if not FACE_MODEL_PATH.is_file():
        raise FileNotFoundError(f"Missing face landmarker model: {FACE_MODEL_PATH}")

    print("torch.cuda.is_available():", torch.cuda.is_available())
    if torch.cuda.is_available():
        print("GPU name:", torch.cuda.get_device_name(0))
    else:
        print("WARNING: ROMP is running without CUDA. Frame rate will usually be very low.")

    print("Initializing ROMP...")
    body_model = ROMP(settings)
    if hasattr(body_model, "centermap_parser"):
        body_model.centermap_parser.max_person = 1
    print("ROMP initialized")

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

    rs_capture = RealSenseCapture(CAMERA_WIDTH, CAMERA_HEIGHT, CAMERA_FPS).start()
    frame_server = LatestJpegServer(port=FRAME_PORT).start()

    prev_print_time = 0.0
    prev_body_print_time = 0.0
    prev_head_send_time = 0.0
    prev_face_detect_time = 0.0
    body_packet_count = 0
    last_fit_metric = None
    body_detected = False

    try:
        with vision.FaceLandmarker.create_from_options(face_options) as face_landmarker:
            print("Face Landmarker initialized")
            if SHOW_WINDOW:
                print("ESC key to exit")
            else:
                print("Preview disabled. Press Ctrl+C to exit.")

            while True:
                frame, depth_frame = rs_capture.read()
                if frame is None:
                    time.sleep(0.001)
                    continue

                now = time.time()
                body_frame = frame
                face_frame = cv2.flip(frame, 1)
                body_detected = False
                frame_server.update_frame(body_frame)

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
                    outputs = body_model(body_frame)
                    smpl24 = extract_romp_smpl24(outputs)

                    if smpl24 is not None:
                        body_detected = True
                        body_payload = convert_to_unity_coordinates(smpl24)
                        root_pixel = extract_root_pixel(outputs)
                        root_depth_m = -1.0
                        if root_pixel is not None:
                            root_depth_m = get_depth_median(depth_frame, root_pixel[0], root_pixel[1])

                        fit_metric = build_body_fit_metric(smpl24, root_pixel, root_depth_m)
                        if fit_metric is not None:
                            body_payload.append(fit_metric)
                            last_fit_metric = fit_metric

                        body_msg = json.dumps(body_payload).encode("utf-8")
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
                    display = make_preview_frame(body_frame)

                    y_base = 40
                    if body_detected:
                        cv2.putText(
                            display, "ROMP Body Detected", (20, y_base),
                            cv2.FONT_HERSHEY_SIMPLEX, 0.9, (0, 255, 0), 2,
                        )

                    cv2.imshow("ROMP + Face Sender", display)

                if ENABLE_CONSOLE_LOG and matrix_ok and (now - prev_print_time) >= PRINT_INTERVAL:
                    print(
                        f"[HEAD] Yaw: {smoothed_yaw:7.2f} | "
                        f"Pitch: {smoothed_pitch:7.2f} | "
                        f"Roll: {smoothed_roll:7.2f}"
                    )
                    prev_print_time = now

                if SHOW_WINDOW:
                    key = cv2.waitKey(1) & 0xFF
                    if key == 27:
                        break

    except Exception as exc:
        print(f"ERROR OCCURRED IN MAIN LOOP: {type(exc).__name__}: {exc}")
        print(traceback.format_exc())

    finally:
        try:
            rs_capture.stop()
        except Exception:
            pass

        try:
            frame_server.stop()
        except Exception:
            pass

        try:
            sock.close()
        except Exception:
            pass

        if SHOW_WINDOW:
            cv2.destroyAllWindows()
        print("Finished")


if __name__ == "__main__":
    main()
