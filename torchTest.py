from romp import ROMP, WebcamVideoStream
import romp
import cv2
import torch
import traceback


def main():
    print("torch version:", torch.__version__)
    print("cuda available:", torch.cuda.is_available())

    if not torch.cuda.is_available():
        raise RuntimeError("CUDA를 사용할 수 없습니다. romp39_gpu 가상환경에서 실행하세요.")

    print("gpu name:", torch.cuda.get_device_name(0))

    # ROMP 설정
    settings = romp.romp_settings()
    settings.mode = "webcam"
    settings.GPU = 0
    settings.onnx = False
    settings.show = True   # 결과창 표시

    print("Initializing ROMP...")
    model = ROMP(settings)
    print("ROMP initialized")

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
            else:
                print("Detected keys:", outputs.keys())

            # ESC 종료
            if cv2.waitKey(1) & 0xFF == 27:
                break

    except Exception:
        print("ERROR OCCURRED")
        traceback.print_exc()

    finally:
        webcam.stop()
        cv2.destroyAllWindows()
        print("종료")


if __name__ == "__main__":
    main()