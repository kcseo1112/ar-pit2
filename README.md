# AR FitRoom 진행 정리

Unity 기반 AR 가상 피팅룸 프로젝트입니다. 기존 ROMP/UDP 포즈 수신, AvatarRetarget, OutfitManager 의상 적용 흐름은 유지하면서 FitRoom UI, MySQL/Flask API, 찜 기능, MediaPipe Hands 제스처 입력을 단계적으로 추가하고 있습니다.

## 현재 구현 상태

### Unity FitRoom UI

- `FitRoomMainUI`를 스마트미러/AR 피팅룸 느낌의 고급 UI로 개편했습니다.
- 기존 하단 가로 의상 리스트는 제거하고, 오른쪽 세로형 `VerticalOutfitCarousel` 중심 UI로 변경했습니다.
- 중앙 `PreviewFrame`은 AR/3D 피팅 화면만 보이도록 유지하고, 내부 조작 버튼은 넣지 않았습니다.
- 왼쪽 `CurrentOutfitPanel`에는 현재 착용 중인 상의/하의 정보와 썸네일이 표시됩니다.
- 오른쪽에는 전체 목록/찜 목록 전환, 카테고리 표시, 세로 의상 리스트, 찜 DropZone이 분리되어 있습니다.
- 오른쪽 전체를 감싸던 큰 회색 배경은 제거하고, 개별 카드/버튼 중심으로 정리했습니다.

### CoverFlow 세로 Carousel

- 오른쪽 의상 리스트는 5개 카드가 세로로 보이는 구조입니다.
- 가운데 카드가 항상 현재 focus 카드입니다.
- 위/아래 이동 시 카드가 CoverFlow 방식으로 부드럽게 이동합니다.
- 이동 중 중앙에 가까워지는 카드는 커지고 선명해지며, 멀어지는 카드는 작아지고 흐려집니다.
- 순환형 리스트를 유지합니다. 마지막 의상 다음은 첫 번째 의상으로 이어집니다.
- focus 이동이 끝나면 현재 focus 의상이 자동으로 착용됩니다.
- 상의는 `OutfitManager.SelectUpper(index)`, 하의는 `OutfitManager.SelectLower(index)`로 연결됩니다.

현재 Unity 배열 기준:

```text
Upper Outfits
0 NONE
1 nomal
2 BLUE
3 padding
4 IROMMAN
5 CH36

Lower Outfits
0 NONE
1 pants1
2 trible
3 CH36
```

DB 매칭은 `unity_category_code + unity_outfit_index` 기준입니다.

### 입력 방식

기존 테스트 입력은 유지되어 있습니다.

```text
W / UpArrow      focus 다음 이동
S / DownArrow    focus 이전 이동
A / LeftArrow    이전 카테고리
D / RightArrow   다음 카테고리
Enter            현재 focus 의상 착용 확정
F                현재 focus 의상 찜 토글
Tab              전체 목록 / 찜 목록 전환
마우스 드래그     의상 리스트 위/아래 이동
```

마우스 찜 동작:

- 현재 focus 카드 위에서 길게 누르면 작은 복제 카드가 생성됩니다.
- 복제 카드는 마우스를 따라 이동합니다.
- 찜 DropZone 위에서 놓으면 해당 의상이 찜에 추가됩니다.
- DropZone이 아닌 곳에서 놓으면 아무 동작 없이 복제 카드만 사라집니다.

### MediaPipe Hands 제스처 입력

`GestureReceiverUDP.cs`를 추가했습니다.

- Unity는 UDP `5010` 포트로 손 제스처 이벤트를 수신합니다.
- 수신 스레드에서는 Unity API를 직접 호출하지 않고, Queue에 넣은 뒤 `Update()`에서 처리합니다.
- `FitRoomMainUI`가 실행될 때 `GestureReceiverUDP`가 없으면 자동으로 붙도록 처리했습니다.

현재 연결된 제스처:

```text
손바닥을 편 상태에서 위/아래 이동    의상 focus 이동
손바닥을 편 상태에서 좌/우 이동      카테고리 이동
따봉 유지                           현재 focus 의상 찜 추가
주먹 press/move/release             기존 ghost card 입력 흐름과 연결
```

손바닥 스와이프는 손가락이 위로 향한 열린 손바닥 자세에서만 동작하도록 제한했습니다. 손바닥이 옆으로 기울어진 상태에서 오작동하는 문제를 줄이기 위한 처리입니다.

따봉 찜은 마우스 드래그 찜보다 단순한 제스처용 동작입니다.

- 오른손 따봉을 일정 시간 유지하면 현재 focus 의상이 찜 처리됩니다.
- 이때 focus 카드의 작은 복제본이 찜 DropZone으로 이동하는 애니메이션이 실행됩니다.
- DropZone은 핑크/레드 계열로 반짝이며 살짝 커졌다가 원래 상태로 돌아옵니다.

최근 따봉 판정은 인식이 너무 빡빡하지 않도록 완화했습니다.

```text
THUMBS_UP_HOLD_SECONDS = 0.35
THUMB_UP_DY_THRESHOLD = -0.025
THUMB_VERTICAL_RATIO = 0.65
FOLDED_FINGER_DY_THRESHOLD = -0.005
```

### Python ROMP + Face + Hands 송신

현재 실행 파일:

```text
C:\Users\kangj\Realtime_python\romp_face_udp_integrated.py
```

실행 방식:

```powershell
cd C:\Users\kangj\Realtime_python
.\romp39_gpu\Scripts\python.exe .\romp_face_udp_integrated.py
```

현재 UDP 포트:

```text
body: 12000
head: 5006
hand: 5010
```

MediaPipe Hands는 별도 카메라를 열지 않고, 기존 ROMP/Face에서 사용하는 프레임을 함께 사용합니다.

필요 모델:

```text
models/hand_landmarker.task
```

현재는 MediaPipe Tasks `HandLandmarker` 기반으로 오른손을 감지합니다. Python preview 화면에는 감지된 손, pose, gesture, palm 좌표, cooldown 상태가 표시됩니다.

### DB / API

MySQL 스키마:

```text
ar_fit
```

주요 테이블:

```text
users
categories
outfits
favorites
```

Flask API 실행 방식:

```powershell
cd C:\Users\kangj\Realtime_python
.\.venv\Scripts\python.exe ar_fit_api.py
```

현재 구현된 주요 기능:

- 회원가입
- 로그인
- 사용자 정보 조회
- 비밀번호 변경 API
- 의상 목록 조회
- 카테고리 조회
- 찜 추가/삭제/toggle
- 사용자별 찜 목록 조회

Unity에서는 로그인 사용자 기준으로 DB favorites를 불러와 하트 상태와 찜 목록 모드에 반영합니다.

## 유지해야 하는 부분

아래 구조는 현재 작업에서 건드리지 않는 것을 원칙으로 합니다.

- `PoseReceiverUDP`
- `AvatarRetarget`
- ROMP body UDP 흐름
- Face head UDP 흐름
- `OutfitManager` 기본 구조
- `OutfitManager.SelectUpper(index)`
- `OutfitManager.SelectLower(index)`
- `OutfitManager.RefreshActiveOutfits()`
- `useSeparatedOutfits = true`
- `enableKeyboardSwitch = false`
- 의상 Y 위치 보정 중복 적용 금지

## 앞으로 수정할 내용

### 1. 찜 목록 표시 방식 정리

현재 찜 목록 모드는 오른쪽 세로 carousel에서 찜한 의상만 보여주는 방향으로 구현되어 있습니다. 앞으로는 실제 사용자 사용 흐름에 맞게 찜 목록을 어떻게 보여줄지 더 정리해야 합니다.

고민 중인 방향:

- 전체 목록 / 찜 목록 전환을 오른쪽 조작 영역에서 계속 유지
- 찜 목록에서도 카테고리별 필터 유지
- 찜 목록이 비어 있을 때 안내 카드 표시 개선
- 찜 목록에서 착용/삭제를 손 제스처로 어떻게 분리할지 결정
- 별도 전체 화면 WishlistPanel을 유지할지, 세로 carousel 방식으로 통합할지 결정

### 2. 로그인 UI 개선

현재 로그인/회원가입 기능은 API와 Unity UI가 연결되어 있지만, AR 피팅룸 환경에서는 실제 입력 방식이 아직 부족합니다.

예상 방향:

- 로그인/회원가입/비밀번호 변경 시 화면 키보드 UI 표시
- 키보드 UI가 떠 있는 동안 Hands 제스처 조작 일시 정지
- 손 제스처가 키보드 입력과 의상 조작을 동시에 건드리지 않도록 입력 모드 분리
- 로그인 후 우측 상단에는 `사용자이름님` 표시
- 사용자 정보 화면에서 이름/전화번호 조회, 비밀번호 확인 후 변경 기능 정리
- 로그아웃 버튼과 세션 초기화 흐름 개선

### 3. 제스처 안정화

- 열린 손바닥 스와이프 threshold 추가 튜닝
- 따봉 인식 정확도 개선
- 오른손/왼손 mirror 옵션 현장 테스트
- Unity Console gesture log on/off 옵션 정리
- 제스처 입력 중복 방지 cooldown 값 조정

### 4. DB 기반 의상 정보 확장

- 의상 설명, 색상, 성별, 카테고리 정보를 UI에 더 자연스럽게 표시
- 필터/정렬 버튼을 실제 DB 조건과 연결
- 썸네일을 Unity 배열 방식에서 DB/API 기반 로딩 방식으로 확장 검토

## 검증 상태

최근 확인:

```powershell
dotnet build Assembly-CSharp.csproj --no-restore
python -m py_compile romp_face_udp_integrated.py
```

결과:

```text
Unity C# 빌드 오류 0개
Python 문법 오류 0개
```
