# AR FitRoom 진행 정리

Unity 기반 AR 가상 피팅룸 프로젝트입니다. 기존 ROMP/UDP 포즈 수신, AvatarRetarget, OutfitManager 기반 상의/하의 착용 구조는 유지하면서, FitRoom UI와 MySQL/Flask API 연동을 확장했습니다.

## 현재 구현된 내용

### Unity FitRoom UI

- `FitRoomMainUI`를 스마트미러/AR 피팅룸 스타일의 고급 UI로 개편했습니다.
- 기존 하단 가로 의상 리스트는 제거하고, 오른쪽 세로형 `VerticalOutfitCarousel` 중심 구조로 변경했습니다.
- 오른쪽 UI는 큰 배경 패널 없이 투명 컨테이너 기반으로 정리했습니다.
- 전체 목록 / 찜 목록 토글, 카테고리 버튼, 세로 의상 리스트, 찜 DropZone이 분리되어 보이도록 정렬했습니다.
- 중앙 `PreviewFrame`은 버튼 없이 AR/3D 피팅 화면만 보이도록 유지했습니다.
- 왼쪽 `CurrentOutfitPanel`에는 현재 착용 중인 상의/하의 썸네일과 이름이 표시됩니다.

### 세로 CoverFlow Carousel

- 오른쪽 의상 리스트는 5개 카드 구조를 유지합니다.
- 가운데 카드가 항상 focus 카드입니다.
- 상하 이동 시 카드가 CoverFlow 방식으로 부드럽게 이동합니다.
- 이동 중 중앙에 가까워지는 카드는 커지고 선명해지며, 중앙에서 멀어지는 카드는 작아지고 흐려집니다.
- 이동 방향에 따라 임시 6번째 카드를 생성해 다음 카드가 자연스럽게 들어오도록 구현했습니다.
- focus 이동이 끝나면 자동으로 해당 의상이 착용됩니다.
- 별도 클릭 없이 focus 된 의상이 바로 `OutfitManager.SelectUpper(index)` 또는 `SelectLower(index)`로 연결됩니다.

### 입력 및 제스처 연결 준비

현재 Unity 테스트 입력:

```text
W / UpArrow      focus 다음 이동
S / DownArrow    focus 이전 이동
A / LeftArrow    이전 카테고리
D / RightArrow   다음 카테고리
Enter            현재 focus 의상 착용 확정
F                현재 focus 의상 찜 토글
Tab              전체 목록 / 찜 목록 전환
마우스 위/아래 드래그  의상 리스트 이동
```

추후 MediaPipe Hands 연동을 위해 아래 public method를 유지하고 있습니다.

```text
OnGestureSwipeUp()
OnGestureSwipeDown()
OnGestureSwipeLeft()
OnGestureSwipeRight()
OnGestureFistHoldConfirmed()
OnGestureFavoritePull()
OnGestureToggleListMode()
```

### 찜 Drag & Drop 동작

- 현재 focus 카드에서 마우스를 길게 누르면 작은 복제 카드가 생성됩니다.
- 복제 카드는 마우스를 따라 이동합니다.
- 찜 DropZone 위에서 마우스를 놓으면 해당 의상이 찜에 추가됩니다.
- DropZone이 아닌 곳에서 놓으면 아무 동작 없이 복제 카드만 사라집니다.
- 이미 찜된 옷은 중복 드롭해도 찜 해제되지 않도록 처리했습니다.
- 이 동작은 나중에 손 제스처 기반 “집어서 찜 상자에 넣기” 기능으로 확장할 수 있습니다.

### OutfitManager 연동

기존 구조는 유지했습니다.

- `OutfitManager.SelectUpper(index)`
- `OutfitManager.SelectLower(index)`
- `OutfitManager.RefreshActiveOutfits()`
- `useSeparatedOutfits = true`
- `enableKeyboardSwitch = false`

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

## Backend / MySQL API

Flask API 파일:

```text
Backend/ar_fit_api.py
```

현재 사용 중인 런타임 위치 예:

```powershell
cd C:\Users\kangj\Realtime_python
.\.venv\Scripts\python.exe ar_fit_api.py
```

기본 API 주소:

```text
http://127.0.0.1:5000
```

주요 API:

- `GET /api/health`
- `GET /api/categories`
- `GET /api/outfits?category_code=upper`
- `GET /api/outfits/<outfit_id>`
- `POST /api/auth/register`
- `POST /api/auth/login`
- `GET /api/users/<user_id>`
- `POST /api/users/<user_id>/password`
- `GET /api/users/<user_id>/favorites`
- `POST /api/favorites/toggle`
- `POST /api/favorites/remove`
- `POST /api/dev/seed`

MySQL 스키마:

```text
ar_fit
```

주요 테이블:

- `users`
- `categories`
- `outfits`
- `favorites`

## ROMP 실행 참고

사용자가 현재 사용하는 ROMP 실행 방식:

```powershell
cd C:\Users\kangj\Realtime_python
.\romp39_gpu\Scripts\python.exe romp_face_udp_integrated.py
```

## 이번 UI 작업에서 건드리지 않은 부분

- `PoseReceiverUDP`
- `AvatarRetarget`
- ROMP/UDP 포즈 흐름
- 의상 Y 위치 보정
- `OutfitManager` 내부 구조
- DB 테이블 구조
- API endpoint 구조

## 앞으로 구현할 내용

- MediaPipe Hands 기반 실제 제스처 이벤트를 Unity `FitRoomMainUI` public method와 연결
- 손 위치 기반 찜 카드 드래그/DropZone 판정 고도화
- 주먹 hold, 손바닥 hold, 몸쪽 당기기 등 제스처 안정화
- 로그인/회원가입/사용자 정보 UI의 에러 메시지 표시 개선
- DB 의상 설명, 색상, 성별, 필터/정렬 조건을 Unity UI에 연결
- `thumbnail_url` 또는 DB 기반 이미지 로딩 정책 정리
- 세션/토큰 기반 로그인 유지 방식 추가
- 실제 배포용으로 DB 비밀번호와 Flask `SECRET_KEY`를 환경변수로 분리

## 검증 상태

최근 Unity 컴파일 확인:

```powershell
dotnet build Assembly-CSharp.csproj --no-restore
```

결과:

```text
경고 0개
오류 0개
```
