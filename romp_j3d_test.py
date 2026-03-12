from romp import ROMP, WebcamVideoStream
import romp
import socket
import json
import cv2
import traceback
import numpy as np

# ==========================
# 1️⃣ ROMP 설정
# ==========================
settings = romp.romp_settings()
settings.mode = 'webcam'
settings.GPU = -1          # CPU 사용
settings.onnx = True

print("Initializing ROMP...")
model = ROMP(settings)
print("ROMP initialized")

# ==========================
# 2️⃣ UDP 설정
# ==========================
UNITY_IP = "127.0.0.1"
UNITY_PORT = 5005

sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
print("ROMP → Unity UDP 송출 시작")

# ==========================
# 3️⃣ 웹캠 시작
# ==========================
webcam = WebcamVideoStream(0).start()
print("Webcam started")

try:
    while True:
        frame = webcam.read()

        if frame is None:
            print("No frame")
            continue

        outputs = model(frame)

        if outputs is None:
            print("No detection")
            continue

        print("Detected keys:", outputs.keys())

        # --------------------------
        # 4️⃣ 3D joint 우선 사용
        # --------------------------
        if 'smpl_joints' in outputs:
            joints = outputs['smpl_joints']
            print("Using smpl_joints")
        elif 'joints' in outputs:
            joints = outputs['joints']
            print("Using joints")
        else:
            print("No joint key found")
            continue

        if joints is None or len(joints) == 0:
            print("Empty joints")
            continue

        joints = np.array(joints)

        print("Joint shape:", joints.shape)

        # 첫 번째 사람, 24개 관절
        smpl_24 = joints[0][:24]

        unity_joints = []

        for joint in smpl_24:
            x = float(joint[0])
            y = float(-joint[1])
            z = float(-joint[2])   # Unity 좌표계 반전
            unity_joints.append([x, y, z])

        msg = json.dumps(unity_joints).encode("utf-8")
        sock.sendto(msg, (UNITY_IP, UNITY_PORT))

        print("UDP SENT", len(msg))

        # ESC 종료
        if cv2.waitKey(1) & 0xFF == 27:
            break

except Exception as e:
    print("ERROR OCCURRED")
    traceback.print_exc()

finally:
    webcam.stop()
    sock.close()
    print("종료")