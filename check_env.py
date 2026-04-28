from __future__ import annotations

import importlib
import platform
import shutil
import sys
from pathlib import Path


PROJECT_ROOT = Path(__file__).resolve().parent
MODEL_PATHS = {
    "face model": PROJECT_ROOT / "models" / "face_landmarker.task",
    "pose model": PROJECT_ROOT / "models" / "pose_landmarker_full.task",
}
REQUIRED_MODULES = ["cv2", "numpy", "mediapipe"]


def import_status(module_name: str) -> tuple[bool, str]:
    try:
        module = importlib.import_module(module_name)
    except Exception as exc:
        return False, f"{module_name}: missing ({exc})"

    version = getattr(module, "__version__", "unknown")
    return True, f"{module_name}: ok ({version})"


def probe_windows_camera(max_index: int = 5) -> str:
    try:
        import cv2
    except Exception as exc:
        return f"[WARN] camera check skipped: OpenCV unavailable ({exc})"

    backends = [
        ("CAP_DSHOW", cv2.CAP_DSHOW),
        ("CAP_MSMF", cv2.CAP_MSMF),
        ("DEFAULT", None),
    ]

    for index in range(max_index + 1):
        for backend_name, backend in backends:
            cap = cv2.VideoCapture(index) if backend is None else cv2.VideoCapture(index, backend)
            try:
                if not cap.isOpened():
                    continue
                ok, frame = cap.read()
                if ok and frame is not None:
                    return f"[CHECK] camera device: opened index {index} ({backend_name}, Windows)"
            finally:
                cap.release()

    return "[WARN] camera device not available on indexes 0-5 (Windows)"


def check_camera() -> str:
    system = platform.system()
    if system == "Windows":
        return probe_windows_camera()

    camera_hint = Path("/dev/video0")
    if camera_hint.exists():
        return f"[CHECK] camera device: detected ({camera_hint})"
    return f"[WARN] camera device not found at {camera_hint}"


def main() -> int:
    print(f"[INFO] Platform: {platform.platform()}")
    print(f"[INFO] Python: {sys.version.split()[0]}")
    print(f"[INFO] Project root: {PROJECT_ROOT}")

    failed = False

    for module_name in REQUIRED_MODULES:
        ok, message = import_status(module_name)
        print(f"[CHECK] {message}")
        failed = failed or not ok

    for label, model_path in MODEL_PATHS.items():
        if model_path.is_file():
            print(f"[CHECK] {label}: ok ({model_path})")
        else:
            print(f"[ERROR] {label} missing: {model_path}")
            failed = True

    print(check_camera())

    pyrealsense2_spec = importlib.util.find_spec("pyrealsense2")
    if pyrealsense2_spec is not None:
        print("[CHECK] pyrealsense2: available")
    else:
        print("[WARN] pyrealsense2: not installed")

    rs_enumerate = shutil.which("rs-enumerate-devices")
    if rs_enumerate:
        print(f"[CHECK] librealsense tool: available ({rs_enumerate})")
    else:
        print("[WARN] librealsense tool: rs-enumerate-devices not found")

    if failed:
        print("[RESULT] environment check failed")
        return 1

    print("[RESULT] environment check passed")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
