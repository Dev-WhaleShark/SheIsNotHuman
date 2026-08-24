# SheIsNotHuman / Cube Screen Codex 인계서

작성 기준: 2026-08-23, Windows / Unity 6000.5.4f1

이 문서는 기존 대화 기록이 없는 다른 PC의 Codex가 현재 Unity 프로토타입을 바로 이어서 작업할 수 있도록 작성했다.

## 1. 새 Codex에 처음 보낼 문장

아래 문장을 새 PC의 Codex에 그대로 전달하면 된다.

```text
이 저장소의 Docs/CODEX_HANDOFF.md와 Docs/CubeScreenPrototype.md를 먼저 끝까지 읽어.
Unity 6000.5.4f1로 Assets/Scenes/CubeScreenPrototype.unity를 열고 Unity MCP 서버를 연결해.
Assets/Scenes/TestScene.unity는 이번 작업과 무관하므로 수정하지 마.
현재 Front/Back/Top/Bottom 16:9 + Right/Left 정사각형 구조와 면별 FOV, 기존 공통 내비게이션/렌즈 구조를 유지하면서 작업을 이어가.
씬이나 코드를 수정한 뒤에는 반드시 Play Mode 화면 캡처와 Console 오류/경고를 확인해.
```

## 2. 저장소와 실행 환경

| 항목 | 현재 값 |
| --- | --- |
| Git 브랜치 | `main` |
| 원격 저장소 | `https://github.com/Dev-WhaleShark/SheIsNotHuman.git` |
| Unity | `6000.5.4f1 (d550df8bd089)` |
| 렌더 파이프라인 | URP 17.6.0 |
| 입력 | Input System 1.19.0 |
| UI | uGUI 2.5.0 + TextMeshPro |
| Tween | `Assets/Plugins/Demigiant`의 DOTween / DOTween Pro |
| Unity MCP | `com.coplaydev.unity-mcp`, Git 패키지 main 브랜치 |
| 주 작업 씬 | `Assets/Scenes/CubeScreenPrototype.unity` |
| 현재 Build Settings | `SampleScene.unity`만 등록됨. 프로토타입 씬은 아직 미등록 |

다른 PC에서는 프로젝트를 Unity 6000.5.4f1로 연 뒤 패키지 복구가 끝날 때까지 기다린다. Unity MCP 창에서 서버를 시작하고 새 Codex가 표시된 Unity 인스턴스를 선택하게 한다. Unity 인스턴스 ID/hash는 PC와 세션마다 달라지므로 이 PC의 ID를 재사용하면 안 된다.

## 3. Git 이전 전 필수 확인

현재 작업은 아직 커밋되지 않았다. 특히 핵심 프로토타입 파일 대부분이 `untracked` 상태다. 이 상태로 다른 PC에서 단순히 `git pull`하면 작업물이 전달되지 않는다.

현재 중요한 상태:

```text
 M Assets/Settings/UniversalRP.asset
 M ProjectSettings/QualitySettings.asset
 M ProjectSettings/TagManager.asset
?? Assets/CubeScreen/
?? Assets/Scenes/CubeScreenPrototype.unity
?? Docs/
```

`Assets/Scenes/TestScene.unity`와 그 `.meta`는 이번 Cube Screen 작업과 무관한 사용자 파일이다. 인계용 커밋에 포함할지 사용자가 별도로 판단해야 하며, Codex가 임의로 수정하거나 삭제하면 안 된다.

추천 스테이징 범위:

```powershell
git add -- Assets/CubeScreen Assets/CubeScreen.meta
git add -- Assets/Scenes/CubeScreenPrototype.unity Assets/Scenes/CubeScreenPrototype.unity.meta
git add -- Assets/Settings/UniversalRP.asset
git add -- ProjectSettings/QualitySettings.asset ProjectSettings/TagManager.asset
git add -- Docs
```

그 후 사용자가 변경 내용을 확인하고 직접 커밋/푸시한다.

```powershell
git status
git diff --cached
git commit -m "feat: add cube screen prototype"
git push origin main
```

주의: `.gitignore`에 `/Assets/Screenshots`가 있어 화면 캡처 PNG들은 기본 `git add`로 전달되지 않는다. 캡처가 꼭 필요하면 사용자가 선택한 파일만 `git add -f`로 추가한다. `.gitignore` 자체에는 `.idea`와 `Assets/Screenshots` 제외 규칙이 추가되어 있는데, 다른 사용자 변경과 겹칠 수 있으므로 통째로 되돌리지 않는다.

## 4. 먼저 읽을 문서와 파일

1. `Docs/CODEX_HANDOFF.md` — 현재 인계서
2. `Docs/CubeScreenPrototype.md` — 전체 구조와 이전 작업 기록
3. `Assets/CubeScreen/Runtime/CubeScreenController.cs` — 화면 상태와 회전 규칙
4. `Assets/CubeScreen/Runtime/CubeNavigationOverlay.cs` — 공통 방향 버튼과 Fade
5. `Assets/CubeScreen/Runtime/CubeFaceLensDisplay.cs` — 면별 렌즈 파라미터
6. `Assets/CubeScreen/Runtime/CubeFaceGraphicRaycaster.cs` — 렌즈 면 UI 입력
7. `Assets/CubeScreen/Runtime/PixelPresentationViewport.cs` — 16:9 출력/입력 좌표 동기화
8. `Assets/CubeScreen/Shaders/CubeFaceLens.shader` — 렌즈 셰이더
9. `Assets/Scenes/CubeScreenPrototype.unity` — 실제 프로토타입 씬

## 5. 현재 구현 구조

```text
ViewerRig
├─ ViewerCamera          → PixelFrame 640×360 렌더링
└─ UIEventCamera         → World Space UI 포인터 판정

Faces
├─ Face_Front
├─ Face_Right
├─ Face_Back
├─ Face_Left
├─ Face_Top
└─ Face_Bottom

LensRenderRig
├─ Capture_Front         → FaceFront RT
├─ Capture_Right         → FaceRight RT
├─ Capture_Back          → FaceBack RT
├─ Capture_Left          → FaceLeft RT
├─ Capture_Top           → FaceTop RT
└─ Capture_Bottom        → FaceBottom RT

LensSurfaces
├─ LensSurface_Front
├─ LensSurface_Right
├─ LensSurface_Back
├─ LensSurface_Left
├─ LensSurface_Top
└─ LensSurface_Bottom

PixelPresentation
├─ Background
├─ PixelFrame
└─ NavigationOverlay
   ├─ NavigateLeft
   ├─ NavigateRight
   ├─ NavigateUp
   └─ NavigateDown
```

렌더 흐름:

```text
면별 World Space uGUI
→ 면별 직교 Capture Camera
→ 면별 RenderTexture
→ CubeFaceLens가 적용된 Quad
→ ViewerCamera
→ PixelFrame 640×360
→ PixelOutputCamera
→ Display 1
```

## 6. 현재 확정값

### 카메라와 화면

| 항목 | 값 |
| --- | ---: |
| Front/Back FOV | 95 |
| Right/Left FOV | 63 |
| Top 전용 FOV | 95 |
| Bottom 전용 FOV | 90.1 |
| 회전 시간 | 0.45초 |
| 회전 Ease | DOTween `Ease.InOutSine` |
| 드래그 최소 거리 | 80픽셀 |
| 최종 PixelFrame | 640×360 / Point |

ViewerCamera/UIEventCamera의 시작 FOV는 Front용 95다. CubeScreenController가 현재 면에 따라 Front/Back 95, Right/Left 63, Top 95, Bottom 90.1로 동시에 전환한다.

### 면 규격

| 면 | Canvas | Lens Quad | Capture RT |
| --- | --- | --- | --- |
| Front/Back/Top/Bottom | 1200×675 | 9.6×5.4 | 640×360 |
| Right/Left | 1200×1200 | 5.4×5.4 | 640×640 |

전체 공간은 X 9.6, Y 5.4, Z 5.4인 직육면체다. Front/Back/Top/Bottom은 16:9이고 Right/Left는 5.4×5.4 정사각형이므로 여섯 면이 실제 좌표에서 겹침 없이 정확히 닫힌다.

### 프로젝트 레이어

| 번호 | 이름 |
| ---: | --- |
| 8 | CubeFrontUI |
| 9 | CubeRightUI |
| 10 | CubeBottomUI |
| 11 | PixelOutput |
| 12 | CubeLeftUI |
| 13 | CubeBackUI |
| 14 | CubeTopUI |

### 렌즈 프로필

| 면 | Distortion | Edge | Zoom | Chromatic | Vignette |
| --- | ---: | ---: | ---: | ---: | ---: |
| Front | -0.18 | 0.05 | 0.90 | 0.0025 | 0.08 |
| Right | 0.22 | -0.05 | 1.10 | 0.0015 | 0.04 |
| Back | 0.16 | -0.03 | 1.06 | 0.002 | 0.05 |
| Left | -0.12 | 0.03 | 0.94 | 0.002 | 0.06 |
| Top | -0.08 | 0.02 | 0.97 | 0.001 | 0.03 |
| Bottom | 0 | 0 | 1 | 0 | 0 |

Bottom도 동일한 렌즈 파이프라인을 통과하지만 모든 값을 중립으로 둬 일반 2D처럼 보이게 했다.

## 7. 화면 이동 규칙

```text
Front ↔ Right ↔ Back ↔ Left ↔ Front
Front → Top → Front
Front → Bottom → Front
```

- Front: Left/Right/Up/Down 허용
- Right/Back/Left: Left/Right만 허용
- Top: Down으로만 Front 복귀
- Bottom: Up으로만 Front 복귀
- Top에서 Up을 한 번 더 눌러도 회전하지 않음
- Bottom에서 Down을 한 번 더 눌러도 회전하지 않음
- 측면/Back에서는 Top/Bottom 진입 불가
- 방향키, WASD, 오른쪽 마우스 드래그에도 동일 규칙 적용

각 면에 있던 방향 버튼 24개는 삭제됐다. 씬의 uGUI Button은 공통 `NavigateLeft/Right/Up/Down` 네 개만 존재한다.

포인터가 화면 끝 44픽셀 영역에 들어오고 해당 방향 이동이 허용될 때만 버튼을 DOTween `DOFade`로 표시한다. Fade 시간은 0.2초, Ease는 `OutQuad`다.

## 8. 애니메이션 구현 원칙

직접 작성한 코루틴/AnimationCurve/Quaternion 수동 보간은 제거했다.

- 회전: `DORotateQuaternion`
- FOV: 같은 DOTween `Sequence`의 `DOTween.To`
- 공통 버튼: `CanvasGroup.DOFade`
- 모두 `SetUpdate(true)` 사용
- 새 Tween 전 기존 Tween을 Kill해 중첩 방지

DOTween을 제거하거나 코루틴 방식으로 되돌리지 않는다.

## 9. 완료된 작업

- 여섯 방향 World Space uGUI 면 구성
- 90도 단위 ViewerRig 회전
- Front에서만 Top/Bottom 진입하는 상태 머신
- Top/Bottom 중복 입력과 좌우 입력 차단
- 화면별 버튼 제거 및 공통 방향 버튼 네 개 추가
- 화면 가장자리 마우스 접근 Fade
- 여섯 면별 Capture Camera/RenderTexture/Lens Quad 구성
- Front/Right/Back/Left/Top 서로 다른 렌즈 값
- Bottom 중립 렌즈/일반 2D 효과
- 640×360 Point 기반 픽셀 출력
- Front/Back/Top/Bottom 1200×675 / 9.6×5.4 / 640×360
- Right/Left 1200×1200 / 5.4×5.4 / 640×640 정사각형
- Front/Back·Side·Top·Bottom 면별 FOV 분리
- Bottom 90.1°에서 화면 전체 일치
- 불투명 깊이 렌더링으로 여섯 면의 실제 접합 유지
- PixelOutputCamera로 Display 1 출력 복구
- URP HDR 비활성화, Point 업스케일, 품질 설정의 AA/Anisotropic 비활성화
- Unity MCP Play Mode 검증
- 마지막 확인 시 Unity Console 오류/경고 0건

## 10. 2026-08-24 직육면체 재설계

현재 구현은 네 개의 16:9 면과 두 개의 정사각형 측면으로 겹침 없는 직육면체를 만든다.

- Front/Back/Top/Bottom: Canvas 1200×675, Lens 9.6×5.4, Capture RT 640×360
- Right/Left: Canvas 1200×1200, Lens 5.4×5.4, Capture RT 640×640
- 내부 기준 공간: X 9.6, Y 5.4, Z 5.4
- Front/Back은 z=±2.7, Top/Bottom은 y=±2.7, Right/Left는 x=±4.8 경계에 놓여 정확히 맞물린다.
- Right/Left의 깊이 방향 길이를 9.6에서 5.4로 줄여 Front/Back 밖으로 튀어나오던 겹침을 제거했다.
- 렌즈 셰이더는 다시 불투명 Geometry, ZWrite On, ZTest LEqual로 동작하며 별도 sortingOrder 우선순위가 없다.
- Bottom 렌즈 거리는 약 2.696이고, 90.1°에서 9.6×5.4 면이 16:9 화면에 정확히 일치한다.
- Front/Back 95, Right/Left 63, Top 95에서는 중앙 면 주위로 인접 면이 보인다.

아래 내용은 이 재설계 이전의 검토 기록이다.

### 이전 미완료 설계 쟁점

사용자의 최신 방향:

1. 가로 기본 FOV는 78로 유지한다.
2. FOV를 과도하게 넓혀 Front에서 Bottom UI를 보이게 하지 않는다.
3. Top/Bottom을 다른 면처럼 직사각형/16:9 성격의 화면으로 만들 수 있는지 검토한다.
4. Bottom 직접 진입 시에는 다른 면 없이 Bottom만 화면을 채워야 한다.
5. Front에서는 Bottom 상단 UI 일부가 연결된 바닥처럼 보여야 한다.

### 이미 테스트했지만 저장하지 않은 방식

Play Mode에서 Top/Bottom을 임시로 아래와 같이 바꿨다.

```text
Canvas 1200×1200 → 1200×900
Lens Quad 9.6×9.6 → 9.6×7.2
Capture RT 640×640 → 640×480
Capture Camera Orthographic Size 4.8 → 3.6
Viewer/UIEvent FOV → 78
```

결과:

- Top/Bottom을 중앙에 두면 Front 접합부에 검은 틈이 생겼다.
- Front 모서리에 맞춰 z축으로 1.2 이동하면 Front 접합은 되지만 Bottom 정면에서 화면 하단이 잘리고 Left/Right 면이 보였다.
- 따라서 단순 RectTransform 축소만으로는 조건을 동시에 만족하지 못했다.
- 임시 변경과 디버그 캡처는 모두 제거했고, 씬에는 남아 있지 않다.

### 원인

현재 닫힌 공간의 월드 크기는 대략 다음과 같다.

```text
폭 X = 9.6
높이 Y = 7.2
깊이 Z = 9.6
```

Front/Back은 9.6×7.2이고 Left/Right도 9.6×7.2이므로 깊이와 폭이 모두 9.6이다. 이 구조에서 Top/Bottom은 앞·뒤·좌·우 모서리를 모두 닫으려면 9.6×9.6 정사각형이어야 한다. Top/Bottom만 직사각형으로 줄이면 반드시 어느 한쪽 접합부가 열린다.

또한 현재 Front/Right/Back/Left의 Canvas 1200×900은 수학적으로 16:9가 아니라 4:3이다. 사용자 표현의 “16:9”가 실제 16:9인지, 단순히 기존 세로 면과 같은 직사각형 비율을 뜻하는지 먼저 확인하는 편이 안전하다.

### 다음 구현 후보

추천 후보 A — 물리 면과 직접 보기 화면을 분리:

- 육면체 접합용 Top/Bottom 물리 면은 정사각형으로 유지
- Front에서 보이는 Bottom 상단은 물리 면/렌즈 Quad로 유지
- Top/Bottom에 직접 진입하면 별도의 16:9 Presentation 레이어로 전환
- Bottom Presentation은 중립 렌즈, Top Presentation은 기존 Top 렌즈 프로필 유지
- 공통 내비게이션은 Presentation 위에 유지
- FOV를 넓히지 않고 Bottom 단독 화면을 보장할 수 있음

후보 B — 공간 전체 비율 재설계:

- Top/Bottom을 직사각형으로 만들고 Front/Back 간 거리 및 Left/Right 물리 폭도 함께 변경
- 여섯 면 접합은 유지되지만 Left/Right의 월드 비율과 카메라 구도가 모두 바뀜
- 기존 렌즈 Quad, Capture Camera, Collider, 입력 좌표를 전부 다시 검증해야 함

후보 C — 접합 틈이나 크롭 허용:

- Top/Bottom만 단순 직사각형으로 축소
- 구현은 가장 단순하지만 기존 요구인 연속된 면과 Bottom 단독 화면 중 하나를 포기해야 하므로 비추천

새 Codex는 사용자 확인 없이 후보 B처럼 공간 전체를 대규모 변경하지 않는 것이 안전하다.

## 11. Unity MCP 검증 절차

1. Unity에서 `CubeScreenPrototype.unity`를 연다.
2. MCP 서버를 시작하고 현재 인스턴스를 선택한다.
3. Edit Mode에서 다음을 확인한다.
   - ViewerCamera/UIEventCamera 시작 FOV 95
   - Front/Back/Top/Bottom Face 1200×675, Lens 9.6×5.4, RT 640×360
   - Right/Left Face 1200×1200, Lens 5.4×5.4, RT 640×640
   - uGUI Button 4개
   - Capture Camera 6개
   - CubeFaceLensDisplay 6개
4. Play Mode에 들어간다.
5. Front → Right → Back → Left → Front 순환 확인
6. Front → Top → Front 확인
7. Front → Bottom → Front 확인
8. Top에서 두 번째 Up, Bottom에서 두 번째 Down이 무시되는지 확인
9. Right/Back/Left에서 Up/Down이 무시되는지 확인
10. 포인터를 네 가장자리로 옮겨 공통 버튼 Fade와 방향 제한 확인
11. `PixelOutputCamera`로 Game View 캡처
12. Console 오류/경고 0건 확인
13. 반드시 Play Mode를 종료한 후 씬 수정/저장

MCP `execute_code`에서 프로젝트 타입을 직접 찾지 못할 때는 `Resources.FindObjectsOfTypeAll<MonoBehaviour>()`에서 `GetType().Name`으로 찾거나 reflection을 사용한다. 이전 세션에서 새 스크립트의 정적 타입 참조가 동적 컴파일에 잡히지 않는 경우가 있었다.

## 12. 알려진 주의사항

- `Assets/Scenes/TestScene.unity`는 건드리지 않는다.
- Play Mode에서 한 임시 Transform/RenderTexture 변경을 씬에 저장하지 않는다.
- 씬 변경 전 Editor가 Play Mode인지 반드시 확인한다.
- `PixelOutputCamera`가 Display 1 직접 출력 카메라다. 이를 비활성화하면 `No cameras rendering`이 다시 나타날 수 있다.
- ViewerCamera는 화면에 직접 출력하지 않고 `PixelFrame`에 렌더링한다.
- PixelPresentation의 `GraphicRaycaster`는 공통 내비게이션에 필요하다.
- 렌즈가 적용된 World Space Canvas는 기본 GraphicRaycaster 대신 `CubeFaceGraphicRaycaster`를 사용한다.
- 새 UI 레이어를 바꾸면 ViewerCamera, UIEventCamera, Capture Camera의 Culling Mask를 함께 확인한다.
- RenderTexture는 Point, MSAA 1, Mipmap 없음 설정을 유지한다.
- 임시 캡처는 `Assets/Screenshots`에 두되 Git에 기본 포함되지 않는다는 점을 기억한다.

## 13. 현재 참고 캡처

로컬에는 다음 캡처가 있지만 `Assets/Screenshots`가 Git에서 무시되고 있어 다른 PC에는 자동 전달되지 않는다.

- `CubeScreenSharedNav_front_78_final.png` — 최종 FOV 78 Front
- `CubeScreenSharedNav_right_final.png`
- `CubeScreenSharedNav_back_final.png`
- `CubeScreenSharedNav_left_final.png`
- `CubeScreenSharedNav_top_final.png`
- `CubeScreenSharedNav_bottom_final.png`

다른 PC에서 필요하면 Unity MCP로 다시 캡처하는 편이 안전하다.

## 14. 인계 완료 조건

다른 PC에서 다음이 확인되면 인계가 성공한 것이다.

- Git에서 `Assets/CubeScreen`과 `CubeScreenPrototype.unity`가 내려옴
- Unity 6000.5.4f1에서 컴파일 오류 없음
- DOTween namespace 오류 없음
- Unity MCP 연결 가능
- 프로토타입 씬 Play Mode에서 Display 1 정상 출력
- 면별 FOV 및 공통 버튼 네 개 확인
- 여섯 렌즈 면 확인
- `Docs/CODEX_HANDOFF.md`의 미완료 쟁점부터 다음 작업을 시작할 수 있음

