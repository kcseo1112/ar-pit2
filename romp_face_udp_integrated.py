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
HAND_PORT = 5010
FRAME_PORT = 13000
FRAME_JPEG_QUALITY = 70

PROJECT_ROOT = Path(__file__).resolve().parent
FACE_MODEL_PATH = PROJECT_ROOT / "models" / "face_landmarker.task"
HAND_MODEL_PATH = PROJECT_ROOT / "models" / "hand_landmarker.task"

# Lower capture resolution can improve FPS when webcam capture, decode, or
# image preprocessing is a bottleneck. Try 640x480 first, then 424x240/320x240.
CAMERA_WIDTH = 640
CAMERA_HEIGHT = 480
CAMERA_FPS = 30
REALSENSE_FRAME_TIMEOUT_MS = 5000

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

# ==========================
# 3-1. MediaPipe Hands gesture config
# ==========================
HAND_DETECT_INTERVAL = 0.05
USE_RIGHT_HAND_ONLY = True
SWAP_HANDEDNESS = False
USE_MEDIAPIPE_FIST_PRESS = True
USE_MEDIAPIPE_PALM_SWIPE = True
USE_ROMP_HAND_TILT_GESTURE = False
USE_MEDIAPIPE_POINT_GESTURE = False

INVERT_SWIPE_X = True
INVERT_SWIPE_Y = False
SWIPE_X_THRESHOLD = 0.12
SWIPE_Y_THRESHOLD = 0.10
SWIPE_AXIS_RATIO = 1.4
SWIPE_COOLDOWN = 0.85
SWIPE_HISTORY_SECONDS = 0.35
SWIPE_MIN_DURATION = 0.08
SWIPE_MAX_DURATION = 0.75
OPEN_PALM_UP_DY_THRESHOLD = -0.055
OPEN_PALM_VERTICAL_RATIO = 1.45

INVERT_ROMP_HAND_X = False
INVERT_ROMP_HAND_Y = False
ROMP_HAND_DIRECTION_THRESHOLD = 0.045
ROMP_HAND_AXIS_RATIO = 1.25
ROMP_HAND_COMMAND_COOLDOWN = 0.55

INVERT_POINT_X = False
INVERT_POINT_Y = False
POINT_DIRECTION_THRESHOLD = 0.075
POINT_AXIS_RATIO = 1.35
POINT_HOLD_SECONDS = 0.32
POINT_COMMAND_COOLDOWN = 0.65

HAND_DEBUG_OVERLAY = True
HAND_PRESS_MOVE_INTERVAL = 0.033
INVERT_HAND_PRESS_X = True
INVERT_HAND_PRESS_Y = False

USE_MEDIAPIPE_THUMBS_UP_FAVORITE = True
THUMBS_UP_COOLDOWN = 1.15
THUMBS_UP_HOLD_SECONDS = 0.35
THUMB_UP_DY_THRESHOLD = -0.025
THUMB_VERTICAL_RATIO = 0.65
FOLDED_FINGER_DY_THRESHOLD = -0.005

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
        context = rs.context()
        if len(context.devices) == 0:
            raise RuntimeError(
                "No Intel RealSense device was detected. "
                "Connect the D435i, use a USB 3.x port, and close RealSense Viewer or any other app using it."
            )

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

        try:
            profile = self.pipeline.start(self.config)
        except RuntimeError as exc:
            raise RuntimeError(
                "Failed to start the D435i color/depth pipeline. "
                "Close any app that may be using the camera and check Windows Camera privacy permissions."
            ) from exc

        self.depth_scale = profile.get_device().first_depth_sensor().get_depth_scale()
        print(f"[D435i] RealSense started: {self.width}x{self.height} @ {self.fps} FPS")
        print("[D435i] depth_scale:", self.depth_scale)
        return self

    def read(self):
        try:
            frames = self.pipeline.wait_for_frames(REALSENSE_FRAME_TIMEOUT_MS)
        except RuntimeError as exc:
            raise RuntimeError(
                "D435i started but no frames arrived. "
                "Reconnect the camera, use a USB 3.x port, and close RealSense Viewer/Unity/other camera apps."
            ) from exc

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


class HandGestureDetector:
    PALM_CENTER_IDS = (0, 5, 9, 13, 17)

    def __init__(self):
        self.enabled = False
        self.hand_landmarker = None
        if not HAND_MODEL_PATH.is_file():
            print(f"[HAND] Disabled: missing model file: {HAND_MODEL_PATH}")
            print("[HAND] Download MediaPipe hand_landmarker.task into the models folder to enable gestures.")
        else:
            hand_base_options = python.BaseOptions(model_asset_path=str(HAND_MODEL_PATH))
            hand_options = vision.HandLandmarkerOptions(
                base_options=hand_base_options,
                running_mode=vision.RunningMode.IMAGE,
                num_hands=1,
                min_hand_detection_confidence=0.55,
                min_hand_presence_confidence=0.55,
                min_tracking_confidence=0.55,
            )
            self.hand_landmarker = vision.HandLandmarker.create_from_options(hand_options)
            self.enabled = True
            print("[HAND] Hand Landmarker initialized")

        self.position_history = []
        self.last_detect_time = 0.0
        self.last_command_time = 0.0
        self.last_swipe_time = 0.0
        self.point_candidate = None
        self.point_candidate_start_time = 0.0
        self.fist_active = False
        self.last_press_move_time = 0.0
        self.last_press_position = None
        self.last_thumbs_up_time = 0.0
        self.thumbs_up_start_time = 0.0
        self.debug = {
            "hand": "None",
            "pose": "disabled" if not self.enabled else "none",
            "gesture": "",
            "x": -1.0,
            "y": -1.0,
            "cooldown": 0.0,
        }

    def close(self):
        if self.hand_landmarker is not None:
            self.hand_landmarker.close()

    def process(self, frame, now, sock):
        if not self.enabled:
            return self.debug

        if (now - self.last_detect_time) < HAND_DETECT_INTERVAL:
            return self.debug

        self.last_detect_time = now
        rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb)
        results = self.hand_landmarker.detect(mp_image)

        self.debug["gesture"] = ""
        self.debug["cooldown"] = max(0.0, (self.last_swipe_time + SWIPE_COOLDOWN) - now)

        if not USE_MEDIAPIPE_FIST_PRESS and not USE_MEDIAPIPE_PALM_SWIPE and not USE_MEDIAPIPE_POINT_GESTURE:
            return self.debug

        if not results.hand_landmarks:
            self._release_fist(sock, now)
            self._reset_point_candidate()
            self.debug.update({"hand": "None", "pose": "none", "x": -1.0, "y": -1.0})
            return self.debug

        selected = self._select_hand(results)
        if selected is None:
            self._release_fist(sock, now)
            self._reset_point_candidate()
            self.debug.update({"hand": "Other", "pose": "ignored", "x": -1.0, "y": -1.0})
            return self.debug

        landmarks, hand_label = selected
        x, y = self._palm_center(landmarks)
        pose = self._classify_pose(landmarks)
        self.debug.update({"hand": hand_label, "pose": pose, "x": x, "y": y})

        if USE_MEDIAPIPE_THUMBS_UP_FAVORITE and self._is_thumbs_up(landmarks):
            self.position_history.clear()
            self._reset_point_candidate()
            self._release_fist(sock, now, x, y)
            self._handle_thumbs_up(sock, now, x, y)
            return self.debug
        else:
            self.thumbs_up_start_time = 0.0

        if pose == "fist":
            self.position_history.clear()
            self._reset_point_candidate()
            if USE_MEDIAPIPE_FIST_PRESS:
                self._handle_fist(sock, now, x, y)
            return self.debug

        self._release_fist(sock, now, x, y)

        if pose == "open" and USE_MEDIAPIPE_PALM_SWIPE and self._is_upright_open_palm(landmarks):
            self._update_palm_swipe(sock, now, x, y)
        else:
            if pose == "open":
                self.debug["pose"] = "open_not_upright"
            self.position_history.clear()

        if USE_MEDIAPIPE_POINT_GESTURE:
            point_direction = self._get_index_point_direction(landmarks)
            if point_direction is not None:
                self._update_point_command(sock, now, point_direction, x, y)
            else:
                self._reset_point_candidate()
        else:
            self._reset_point_candidate()

        return self.debug

    def _select_hand(self, results):
        for landmarks, handedness in zip(results.hand_landmarks, results.handedness):
            label = "Unknown"
            if handedness:
                category = handedness[0]
                label = getattr(category, "category_name", None) or getattr(category, "display_name", None) or "Unknown"

            if SWAP_HANDEDNESS:
                if label == "Right":
                    label = "Left"
                elif label == "Left":
                    label = "Right"

            if USE_RIGHT_HAND_ONLY and label != "Right":
                continue

            return landmarks, label

        return None

    def _palm_center(self, landmarks):
        xs = [landmarks[i].x for i in self.PALM_CENTER_IDS]
        ys = [landmarks[i].y for i in self.PALM_CENTER_IDS]
        return float(np.mean(xs)), float(np.mean(ys))

    def _classify_pose(self, landmarks):
        finger_pairs = ((8, 6), (12, 10), (16, 14), (20, 18))
        extended = 0
        curled = 0
        for tip, pip in finger_pairs:
            if landmarks[tip].y < landmarks[pip].y - 0.015:
                extended += 1
            if landmarks[tip].y > landmarks[pip].y + 0.015:
                curled += 1

        if extended >= 3:
            return "open"
        if curled >= 3:
            return "fist"
        return "other"

    def _update_palm_swipe(self, sock, now, x, y):
        self.position_history.append((now, x, y))
        cutoff = now - SWIPE_HISTORY_SECONDS
        self.position_history = [item for item in self.position_history if item[0] >= cutoff]

        if len(self.position_history) < 2:
            return

        if now - self.last_swipe_time < SWIPE_COOLDOWN:
            return

        start_t, start_x, start_y = self.position_history[0]
        duration = now - start_t
        if duration < SWIPE_MIN_DURATION or duration > SWIPE_MAX_DURATION:
            return

        dx = x - start_x
        dy = y - start_y
        direction = None

        if abs(dx) > SWIPE_X_THRESHOLD and abs(dx) > abs(dy) * SWIPE_AXIS_RATIO:
            direction = "right" if dx > 0 else "left"
            if INVERT_SWIPE_X:
                direction = "left" if direction == "right" else "right"
        elif abs(dy) > SWIPE_Y_THRESHOLD and abs(dy) > abs(dx) * SWIPE_AXIS_RATIO:
            direction = "down" if dy > 0 else "up"
            if INVERT_SWIPE_Y:
                direction = "up" if direction == "down" else "down"

        if direction is None:
            return

        self._send(sock, {
            "type": "swipe",
            "dir": direction,
            "hand": "right",
            "x": round(x, 4),
            "y": round(y, 4),
            "timestamp": now,
        })
        self.debug["gesture"] = "palm " + direction
        self.last_swipe_time = now
        self.position_history.clear()

    def _is_thumbs_up(self, landmarks):
        wrist = landmarks[0]
        thumb_tip = landmarks[4]
        thumb_mcp = landmarks[2]
        thumb_dx = thumb_tip.x - thumb_mcp.x
        thumb_dy = thumb_tip.y - thumb_mcp.y
        thumb_points_up = thumb_dy < THUMB_UP_DY_THRESHOLD and abs(thumb_dy) > abs(thumb_dx) * THUMB_VERTICAL_RATIO
        thumb_above_wrist = thumb_tip.y < wrist.y - 0.035

        folded = 0
        for tip_id, pip_id in ((8, 6), (12, 10), (16, 14), (20, 18)):
            if landmarks[tip_id].y > landmarks[pip_id].y + FOLDED_FINGER_DY_THRESHOLD:
                folded += 1

        return thumb_points_up and thumb_above_wrist and folded >= 3

    def _handle_thumbs_up(self, sock, now, x, y):
        self.debug["pose"] = "thumbs_up"
        if now - self.last_thumbs_up_time < THUMBS_UP_COOLDOWN:
            return

        if self.thumbs_up_start_time <= 0.0:
            self.thumbs_up_start_time = now
            return

        if now - self.thumbs_up_start_time < THUMBS_UP_HOLD_SECONDS:
            self.debug["gesture"] = "thumbs_up_hold"
            return

        self._send(sock, {
            "type": "thumbs_up_favorite",
            "hand": "right",
            "x": round(x, 4),
            "y": round(y, 4),
            "timestamp": now,
        })
        self.debug["gesture"] = "thumbs_up_favorite"
        self.last_thumbs_up_time = now
        self.thumbs_up_start_time = 0.0

    def _is_upright_open_palm(self, landmarks):
        wrist = landmarks[0]
        finger_tips = [landmarks[i] for i in (8, 12, 16, 20)]
        finger_mcps = [landmarks[i] for i in (5, 9, 13, 17)]

        vertical_count = 0
        for tip, mcp in zip(finger_tips, finger_mcps):
            dx = tip.x - mcp.x
            dy = tip.y - mcp.y
            if dy < OPEN_PALM_UP_DY_THRESHOLD and abs(dy) > abs(dx) * OPEN_PALM_VERTICAL_RATIO:
                vertical_count += 1

        fingertip_center_x = float(np.mean([tip.x for tip in finger_tips]))
        fingertip_center_y = float(np.mean([tip.y for tip in finger_tips]))
        hand_dx = fingertip_center_x - wrist.x
        hand_dy = fingertip_center_y - wrist.y
        hand_points_up = hand_dy < OPEN_PALM_UP_DY_THRESHOLD and abs(hand_dy) > abs(hand_dx) * 0.9

        return vertical_count >= 3 and hand_points_up

    def _get_index_point_direction(self, landmarks):
        index_mcp = landmarks[5]
        index_tip = landmarks[8]
        dx = index_tip.x - index_mcp.x
        dy = index_tip.y - index_mcp.y

        if abs(dx) < POINT_DIRECTION_THRESHOLD and abs(dy) < POINT_DIRECTION_THRESHOLD:
            return None

        if abs(dx) > abs(dy) * POINT_AXIS_RATIO:
            direction = "right" if dx > 0 else "left"
            if INVERT_POINT_X:
                direction = "left" if direction == "right" else "right"
            return direction

        if abs(dy) > abs(dx) * POINT_AXIS_RATIO:
            direction = "down" if dy > 0 else "up"
            if INVERT_POINT_Y:
                direction = "up" if direction == "down" else "down"
            return direction

        return None

    def _update_point_command(self, sock, now, direction, x, y):
        self.debug["pose"] = "point_" + direction

        if now - self.last_command_time < POINT_COMMAND_COOLDOWN:
            return

        if self.point_candidate != direction:
            self.point_candidate = direction
            self.point_candidate_start_time = now
            return

        if now - self.point_candidate_start_time < POINT_HOLD_SECONDS:
            return

        self._send(sock, {
            "type": "swipe",
            "dir": direction,
            "hand": "right",
            "x": round(x, 4),
            "y": round(y, 4),
            "timestamp": now,
        })
        self.debug["gesture"] = "point " + direction
        self.last_command_time = now
        self._reset_point_candidate()

    def _reset_point_candidate(self):
        self.point_candidate = None
        self.point_candidate_start_time = 0.0

    def _handle_fist(self, sock, now, x, y):
        x, y = self._map_press_position(x, y)
        payload = {
            "hand": "right",
            "x": round(x, 4),
            "y": round(y, 4),
            "timestamp": now,
        }

        if not self.fist_active:
            self.fist_active = True
            self.last_press_position = (x, y)
            self.last_press_move_time = now
            payload["type"] = "press_start"
            self._send(sock, payload)
            self.debug["gesture"] = "press_start"
            return

        if now - self.last_press_move_time >= HAND_PRESS_MOVE_INTERVAL:
            self.last_press_position = (x, y)
            self.last_press_move_time = now
            payload["type"] = "press_move"
            self._send(sock, payload)
            self.debug["gesture"] = "press_move"

    def _map_press_position(self, x, y):
        if INVERT_HAND_PRESS_X:
            x = 1.0 - x
        if INVERT_HAND_PRESS_Y:
            y = 1.0 - y
        return x, y

    def _release_fist(self, sock, now, x=None, y=None):
        if not self.fist_active:
            return

        if x is None or y is None:
            if self.last_press_position is not None:
                x, y = self.last_press_position
            else:
                x, y = 0.5, 0.5

        x, y = self._map_press_position(float(x), float(y))
        self.fist_active = False
        self.last_press_position = None
        self._send(sock, {
            "type": "press_release",
            "hand": "right",
            "x": round(float(x), 4),
            "y": round(float(y), 4),
            "timestamp": now,
        })
        self.debug["gesture"] = "press_release"

    def _send(self, sock, payload):
        message = json.dumps(payload).encode("utf-8")
        sock.sendto(message, (UNITY_IP, HAND_PORT))


class RompHandTiltGestureDetector:
    RIGHT_WRIST = 21
    RIGHT_FINGER_IDS = (41, 42, 43, 44)

    def __init__(self):
        self.last_command_time = 0.0
        self.debug = {
            "hand": "ROMP_R",
            "pose": "romp_none",
            "gesture": "",
            "x": -1.0,
            "y": -1.0,
            "cooldown": 0.0,
        }

    def process(self, outputs, now, sock, frame_shape):
        self.debug["gesture"] = ""
        self.debug["cooldown"] = max(0.0, (self.last_command_time + ROMP_HAND_COMMAND_COOLDOWN) - now)

        if outputs is None or "pj2d_org" not in outputs:
            self.debug.update({"pose": "romp_none", "x": -1.0, "y": -1.0})
            return self.debug

        points = np.asarray(outputs["pj2d_org"], dtype=np.float32)
        if points.ndim != 3 or points.shape[0] < 1 or points.shape[1] <= max(self.RIGHT_FINGER_IDS):
            self.debug.update({"pose": "romp_invalid", "x": -1.0, "y": -1.0})
            return self.debug

        person = points[0]
        wrist = person[self.RIGHT_WRIST]
        fingers = person[list(self.RIGHT_FINGER_IDS)]
        finger_center = np.mean(fingers, axis=0)

        height, width = frame_shape[:2]
        if width <= 0 or height <= 0:
            return self.debug

        wrist_x = float(wrist[0] / width)
        wrist_y = float(wrist[1] / height)
        finger_x = float(finger_center[0] / width)
        finger_y = float(finger_center[1] / height)
        dx = finger_x - wrist_x
        dy = finger_y - wrist_y

        self.debug.update({"x": finger_x, "y": finger_y})

        direction = self._direction_from_vector(dx, dy)
        if direction is None:
            self.debug["pose"] = "romp_neutral"
            return self.debug

        self.debug["pose"] = "romp_" + direction

        if now - self.last_command_time < ROMP_HAND_COMMAND_COOLDOWN:
            return self.debug

        self._send(sock, {
            "type": "swipe",
            "dir": direction,
            "hand": "right",
            "x": round(finger_x, 4),
            "y": round(finger_y, 4),
            "timestamp": now,
        })
        self.debug["gesture"] = "romp " + direction
        self.last_command_time = now
        return self.debug

    def _direction_from_vector(self, dx, dy):
        if abs(dx) < ROMP_HAND_DIRECTION_THRESHOLD and abs(dy) < ROMP_HAND_DIRECTION_THRESHOLD:
            return None

        if abs(dx) > abs(dy) * ROMP_HAND_AXIS_RATIO:
            direction = "right" if dx > 0 else "left"
            if INVERT_ROMP_HAND_X:
                direction = "left" if direction == "right" else "right"
            return direction

        if abs(dy) > abs(dx) * ROMP_HAND_AXIS_RATIO:
            direction = "down" if dy > 0 else "up"
            if INVERT_ROMP_HAND_Y:
                direction = "up" if direction == "down" else "down"
            return direction

        return None

    def _send(self, sock, payload):
        message = json.dumps(payload).encode("utf-8")
        sock.sendto(message, (UNITY_IP, HAND_PORT))


def draw_hand_debug(display, debug):
    if not HAND_DEBUG_OVERLAY:
        return

    hand = debug.get("hand", "None")
    pose = debug.get("pose", "none")
    gesture = debug.get("gesture", "")
    x = debug.get("x", -1.0)
    y = debug.get("y", -1.0)
    cooldown = debug.get("cooldown", 0.0)

    cv2.putText(
        display,
        f"Hand: {hand} | Pose: {pose} | Gesture: {gesture}",
        (20, 90),
        cv2.FONT_HERSHEY_SIMPLEX,
        0.65,
        (255, 180, 80),
        2,
    )
    cv2.putText(
        display,
        f"Palm: {x:.2f}, {y:.2f} | Cooldown: {cooldown:.2f}",
        (20, 118),
        cv2.FONT_HERSHEY_SIMPLEX,
        0.65,
        (255, 180, 80),
        2,
    )

    if 0.0 <= x <= 1.0 and 0.0 <= y <= 1.0:
        height, width = display.shape[:2]
        cv2.circle(display, (int(x * width), int(y * height)), 8, (255, 80, 180), -1)


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
    print(f"UDP ready -> body:{BODY_PORT}, head:{HEAD_PORT}, hand:{HAND_PORT}")

    hand_detector = HandGestureDetector()
    romp_hand_detector = RompHandTiltGestureDetector()

    rs_capture = RealSenseCapture(CAMERA_WIDTH, CAMERA_HEIGHT, CAMERA_FPS).start()
    frame_server = LatestJpegServer(port=FRAME_PORT).start()

    prev_print_time = 0.0
    prev_body_print_time = 0.0
    prev_head_send_time = 0.0
    prev_face_detect_time = 0.0
    body_packet_count = 0
    last_fit_metric = None
    body_detected = False
    hand_debug = {"hand": "None", "pose": "none", "gesture": "", "x": -1.0, "y": -1.0, "cooldown": 0.0}

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

                hand_debug = hand_detector.process(body_frame, now, sock)

                try:
                    outputs = body_model(body_frame)
                    if USE_ROMP_HAND_TILT_GESTURE:
                        hand_debug = romp_hand_detector.process(outputs, now, sock, body_frame.shape)
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

                    draw_hand_debug(display, hand_debug)

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
            hand_detector.close()
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
