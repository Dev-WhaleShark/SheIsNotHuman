# UI·2D 월드 통합 면 화면 설계

## 1. 목표 화면

Bottom을 제외한 각 면은 하나의 카메라 화면처럼 보여야 한다.

- 배경, 행인, 중심 인물, 탁자 등은 SpriteRenderer 기반 2D 월드 객체다.
- 팝업, 선택지, 상태 정보는 uGUI Canvas다.
- UI와 2D 월드는 한 `Capture_*` 카메라에서 함께 촬영된다.
- 합쳐진 전체 화면에는 동일한 면 렌즈가 적용된다.
- 2D 월드 객체는 깊이에 따른 크기, 위치, 블러를 가진다.
- 효과값은 자동 깊이 계산뿐 아니라 아티스트가 객체별로 덮어쓸 수 있어야 한다.
- 화면 전환과 인접 면 노출 시에도 각 면의 렌즈 결과가 유지되어야 한다.

## 2. 현재 구현에서 유지할 부분

현재 Front의 렌더링 순서는 다음과 같다.

```text
Face_Front World Space Canvas
→ Capture_Front
→ FaceFront RenderTexture
→ LensSurface_Front + CubeFaceLens
→ ViewerCamera
→ PixelPresentation
```

확인된 Front 기준값:

- `Face_Front`: World Space Canvas, `1280×720`, Layer 8 `CubeFrontUI`
- `Capture_Front`: Orthographic Size `4.5`, Layer 8만 촬영
- `FaceFront.renderTexture`: 면 렌즈의 입력
- `LensSurface_Front`: `CubeFaceLensDisplay`로 RenderTexture 표시
- Front 렌즈: Distortion `-0.18`, Edge Distortion `0.05`, Zoom `0.9`
- Renderer2D에는 Volume과 Renderer Feature가 없음
- `Capture_Front`의 Depth Texture와 Post Processing은 비활성화

이 파이프라인은 최종 목표와 방향이 맞다. 렌즈 구조를 교체하지 않고 캡처 대상에 SpriteRenderer 기반 월드를 추가한다.

## 3. 최종 권장 구조

```text
FaceStage_Front                       [Capture 전용 Layer 8]
├─ WorldRoot
│  ├─ Depth_00_Background
│  │  └─ BackgroundSprites
│  ├─ Depth_10_FarActors
│  │  └─ PasserbySprites
│  ├─ Depth_20_Subject
│  │  └─ SubjectSprites
│  ├─ Depth_30_Foreground
│  │  └─ DeskSprites
│  └─ Depth_40_WorldUI
│     └─ WorldSpace Canvas 또는 Sprite UI
├─ Face_Front                        [기존 World Space Canvas]
│  └─ ScreenUI
└─ Capture_Front                     [Orthographic]

Capture_Front
→ FaceFront RT 640×360 / Point
→ LensSurface_Front / CubeFaceLens
→ ViewerCamera
→ PixelFrame
→ 공통 내비게이션
```

핵심은 SpriteRenderer를 Canvas의 자식으로 넣는 것이 아니라 `WorldRoot` 아래에 배치하되, Canvas와 같은 `Capture_Front` 카메라가 촬영하도록 만드는 것이다.

별도 World 카메라와 UI 카메라는 1차 구조에서 사용하지 않는다. 목표가 UI까지 같은 렌즈로 자연스럽게 휘어지는 것이므로 한 카메라와 하나의 면 RenderTexture가 가장 단순하고 일관적이다.

## 4. 렌더링 역할 분리

### 객체 단계

각 월드 객체는 최종 면 렌즈 전에 자신의 효과를 적용한다.

- Transform: 가짜 원근용 Scale과 화면 중심 확장
- Sprite 셰이더: 객체별 픽셀 블러, 선택적 로컬 왜곡, 색상 보정
- SortingGroup: 여러 파츠의 정렬 유지
- InteractionRoot: 시각 변형과 별도의 클릭 판정

### 면 단계

기존 `CubeFaceLens`는 합쳐진 UI와 월드 전체를 한 번에 처리한다.

- 화면 전체 배럴/핀쿠션 왜곡
- 가장자리 왜곡
- 색수차
- 비네팅
- 면별 Zoom과 Lens Center

목표 스케치처럼 화면 중심은 비교적 안정적이고 가장자리의 행인·배경·UI가 더 크게 휘어지는 효과는 객체마다 별도 렌즈를 만들지 않아도 현재의 방사형 면 렌즈로 자연스럽게 발생한다.

객체 로컬 렌즈는 특수 연출용 Override로만 둔다. 면 렌즈와 객체 렌즈를 모두 강하게 적용하면 스프라이트 외곽과 클릭 좌표가 과도하게 어긋난다.

## 5. Orthographic 원근감

Orthographic에서는 Z가 달라도 자동 크기 변화가 없으므로 Z를 연출 데이터로 사용한다.

```csharp
viewDepth = Vector3.Dot(
    objectPosition - captureCameraPosition,
    captureCameraForward);
```

정규화된 깊이로 다음을 결정한다.

- `Scale Curve`: 가까운 탁자와 인물은 확대, 먼 배경과 행인은 축소
- `Radial Spread Curve`: 가까운 객체를 화면 중심에서 바깥쪽으로 조금 이동
- `Blur Step Curve`: 초점 깊이에서 멀수록 `0~3px` 블러
- `Parallax Curve`: 카메라 흔들림이나 포인터 움직임에 따른 레이어 이동량
- `Color Curve`: 원경 채도·명도 조절

실제 Perspective가 아니라 아트 디렉션 중심의 2.5D지만 픽셀 크기와 구도를 안정적으로 유지할 수 있다.

## 6. 블러 설계

### 1차 구현

객체별 Sprite 셰이더에서 정수 픽셀 블러를 적용한다.

- Point Sampling 유지
- 블러 반경 `0, 1, 2, 3px`
- Alpha를 포함해 누적하고 Premultiplied Alpha 방식으로 복원
- SpriteAtlas Rect 밖 샘플 금지
- Atlas Padding과 Extrude는 최대 블러 반경보다 크게 설정

추천 시작값:

| 그룹 | 초점 관계 | 블러 |
| --- | --- | ---: |
| Background | 먼 원경 | 3px |
| FarActors | 먼 행인 | 2px |
| Subject | 기준 초점 | 0px |
| Foreground | 가까운 탁자 | 1px |
| WorldUI | 연출에 따라 | 0~1px |
| ScreenUI | 항상 선명 | 0px |

URP 기본 Depth of Field는 사용하지 않는다. 현재 설정을 추가로 켜야 하며, 투명 Sprite와 uGUI가 합쳐진 면 전체에 연속적인 블러가 적용되어 객체별 픽셀 블러와 맞지 않는다.

### 2차 후보

객체 수와 오버드로우가 많아질 때만 Renderer Feature를 검토한다.

- Effect ID 또는 깊이를 기록한 Mask Texture
- Render Graph 기반 Pixel Blur Pass
- UI 제외 마스크
- 객체 경계의 깊이 차이를 고려한 샘플 제한

현재 Renderer2D에는 Renderer Feature가 없으므로 첫 프로토타입부터 이 구조를 도입하지 않는다.

## 7. UI 구분

### ScreenUI

- 선택지, 팝업, 상태 정보
- 월드 객체 블러에서 제외
- `Capture_Front`에는 포함되므로 최종 `CubeFaceLens`는 적용
- Canvas Sorting Order를 월드 객체보다 높게 설정

### WorldUI

- 특정 인물이나 사물에 붙은 말풍선, 표식
- 해당 깊이 그룹에 배치
- 아티스트 설정에 따라 블러와 가짜 원근 허용

### 공통 내비게이션

- 화면 가장자리 상하좌우 버튼
- 면 RenderTexture와 렌즈 처리 이후 PixelPresentation 단계에 유지
- 면 렌즈와 객체 블러에서 제외

## 8. 아티스트 조정 데이터

### `FaceVisualProfile`

면 전체 설정을 보관한다.

- Lens Center
- Distortion / Edge Distortion
- Zoom
- Chromatic Aberration
- Vignette
- Near/Far Depth
- Focus Depth와 Focus Range
- Scale / Spread / Blur / Parallax Curve

현재 `CubeFaceLensDisplay`의 개별 SerializeField 값을 이 프로파일에서 읽게 만들면 면별 프리셋을 저장하고 복제하기 쉬워진다.

### `FaceDepthVisual`

월드 객체 또는 깊이 그룹에 부착한다.

- 역할 Preset: Background, FarActor, Subject, Foreground, WorldUI
- 자동 깊이 계산 / 수동 깊이 Override
- Scale / Spread / Blur / Local Warp Override
- 효과 제외 옵션
- `VisualRoot`와 `InteractionRoot` 참조
- Odin Inspector 미리보기와 즉시 갱신 버튼

정적인 오브젝트는 매 프레임 갱신하지 않는다. `OnEnable`, `OnValidate`, 프로파일 변경 시 적용한다. 움직이는 객체만 이동 시스템이 갱신을 호출하거나 R3로 실제 깊이 변화가 있을 때 반응한다.

## 9. 입력 좌표 처리

현재 `CubeFaceGraphicRaycaster`는 현재 보고 있는 면 외의 Raycast를 차단하지만, 렌즈 왜곡된 화면 좌표를 원본 Canvas 좌표로 되돌리지는 않는다.

렌즈가 강해질수록 보이는 버튼과 실제 Graphic 위치가 어긋날 수 있으므로 `FaceLensPointerMapper`가 필요하다.

```text
화면 포인터
→ ViewerCamera로 면 BoxCollider 충돌점 계산
→ 면 로컬 좌표를 출력 Surface UV로 변환
→ CubeFaceLens와 같은 수식으로 Source UV 계산
→ Source UV를 Capture_Front 픽셀 좌표로 변환
→ GraphicRaycaster 또는 2D 오브젝트 입력 판정
```

현재 셰이더는 출력 UV에서 `sampleUv`를 계산하므로 포인터도 녹색 채널 기준의 동일한 UV 함수를 공유해야 한다. 색수차는 입력 판정에서 제외한다.

SpriteRenderer 객체가 클릭 가능해야 한다면 두 가지 방법이 있다.

1. Sprite 위에 투명 uGUI Hit Proxy를 둔다.
2. Source UV로 `Capture_Front.ViewportPointToRay`를 만들고 Collider2D를 판정한다.

선택지와 조사 버튼 중심의 게임이면 1번이 단순하고 안정적이다. 자유로운 월드 객체 클릭이 많다면 2번으로 확장한다.

## 10. 캡처 전용 Layer

월드 Sprite는 `Capture_Front`에는 보여야 하지만 `ViewerCamera`에 직접 보여서는 안 된다.

- Front Canvas와 Front 월드는 동일한 Front 캡처 전용 Layer 사용
- `Capture_Front`는 해당 Layer만 포함
- `ViewerCamera`는 모든 캡처 전용 Layer 제외
- ViewerCamera는 LensSurface와 육면체 구조만 렌더링

이 규칙이 없으면 Sprite가 LensSurface 결과와 별도로 한 번 더 렌더링되거나 육면체 경계 밖으로 튀어나와 보일 수 있다.

## 11. 픽셀 안정성

- 면 RenderTexture는 `640×360`, Filter Mode Point 유지
- Sprite PPU를 프로젝트 기준값으로 통일
- Scale Curve의 결과는 가능한 한 정수 또는 제한된 단계로 양자화
- 카메라와 객체 이동은 소스 RenderTexture 픽셀 그리드에 스냅
- MSAA, TAA, 일반 Motion Blur는 비활성화 유지
- 최종 렌즈는 Point 샘플 기반의 픽셀 굴곡을 유지
- 강한 왜곡 시 가장자리 Clamp 늘어짐을 줄이도록 Overscan 또는 Zoom 안전 범위 제공

## 12. 별도 카메라가 필요한 조건

다음 요구가 생길 때만 `WorldCamera → WorldRT`, `UICamera → UIRT` 분리를 도입한다.

- UI는 면 렌즈에서도 완전히 제외해야 함
- 월드 전체에만 여러 단계의 후처리를 적용해야 함
- 월드와 UI의 내부 해상도가 달라야 함
- 여러 SpriteRenderer가 하나의 그룹처럼 함께 블러·왜곡되어야 함

현재 목표는 UI까지 같은 렌즈 안에서 어우러지는 것이므로 별도 카메라 합성은 기본 구조보다 복잡하고 이점이 적다.

## 13. 구현 순서

### 1단계: 한 카메라 통합 검증

1. `CubeScreenPrototype_Orthographic.unity`에서만 작업
2. `WorldRoot`와 깊이 그룹 추가
3. 플레이스홀더를 SpriteRenderer로 교체
4. 기존 `Face_Front` Canvas와 같은 Layer 8에 배치
5. `Capture_Front`의 위치와 Near/Far를 깊이 무대에 맞게 확장
6. UI와 Sprite가 FaceFront RT에 함께 찍히는지 확인
7. ViewerCamera에 Sprite가 직접 노출되지 않는지 확인

### 2단계: 가짜 원근

1. `FaceVisualProfile`과 `FaceDepthVisual` 구현
2. Subject를 기준 초점으로 설정
3. Scale과 Radial Spread만 적용
4. 픽셀 스냅과 SortingGroup 검증

### 3단계: 객체별 블러

1. 픽셀 블러 Sprite 셰이더 작성
2. MaterialPropertyBlock으로 객체별 반경 전달
3. Background `3px`, FarActor `2px`, Subject `0px`, Foreground `1px` 검증
4. Atlas Bleeding과 Alpha 외곽 확인

### 4단계: 면 렌즈와 입력

1. 기존 `CubeFaceLens`를 통합 결과에 그대로 적용
2. `FaceVisualProfile`로 렌즈값 이전
3. `FaceLensPointerMapper` 구현
4. 렌즈 중심·왜곡·Zoom 변경 후 UI 클릭 위치 검증

### 5단계: 아티스트 작업성

1. 역할별 Preset 제공
2. 자동 깊이값과 수동 Override 동시 지원
3. Odin 미리보기와 초기값 복원 버튼 제공
4. 실제 아트로 교체하고 프로파일 값 확정
5. Front 검증 후 Left, Right, Back, Top으로 확장
6. Bottom은 기존 일반 2D 방식 유지

## 14. 완료 기준

- 하나의 `Capture_Front`가 UI와 모든 SpriteRenderer를 함께 촬영한다.
- 합쳐진 전체 화면에 기존 면 렌즈가 동일하게 적용된다.
- Subject는 선명하고 배경·행인·전경은 서로 다른 깊이감을 가진다.
- 객체 Z 또는 아티스트 Override에 따라 Scale과 블러가 바뀐다.
- ScreenUI는 객체 블러에서 제외되지만 면 렌즈에는 포함된다.
- 렌즈가 강해져도 보이는 UI 위치와 클릭 위치가 일치한다.
- Sprite가 ViewerCamera에 직접 노출되지 않는다.
- `640×360`에서 픽셀 흔들림과 Atlas Bleeding이 없다.
- Front 외 기존 면 이동과 공통 내비게이션이 깨지지 않는다.
- Console 오류가 없다.

## 15. 최종 판단

목표 화면에는 다음 조합이 가장 적합하다.

```text
한 Orthographic Capture 카메라
+ Canvas UI
+ 깊이 배치된 SpriteRenderer 월드
+ 객체별 가짜 원근·픽셀 블러
+ 합성 후 기존 CubeFaceLens
```

현재 렌즈 파이프라인은 유지할 가치가 높다. 핵심 변경은 Canvas 렌더링을 없애는 것이 아니라, 동일한 캡처 무대에 SpriteRenderer 월드를 추가하고 객체 효과와 입력 좌표 보정을 보강하는 것이다.

## 16. 프로토타입 작업 로그

### 2026-09-03

- 기존 `CubeScreenPrototype.unity`를 기반으로 `Assets/Scenes/UnifiedFaceViewPrototype.unity` 테스트 씬을 생성했다.
- 원본 씬은 수정하지 않고 기존 Front Canvas, `Capture_Front`, FaceFront RenderTexture, `LensSurface_Front`, `CubeFaceLens` 구성을 복사해 재사용했다.
- Front에 `UnifiedFrontStage/WorldRoot`와 Background, FarActors, Subject, Foreground, WorldUI 깊이 그룹을 추가했다.
- 임시 이미지는 투명 여백이 있는 흰색 Sprite 하나를 공유하고 SpriteRenderer Color로 역할별 색상을 구분했다.
- 각 그룹에 역할, Z 깊이, 블러 상태를 표시하는 TextMeshPro 라벨을 추가했다.
- 기존 Canvas의 전체 화면 배경 두 개를 테스트 씬에서만 비활성화하고, 단색 Screen UI 팝업과 선택지 두 개를 추가했다.
- `FaceDepthEffectProfile`로 깊이 범위, 크기 곡선, 중심 확장 곡선, 초점과 최대 블러를 저장했다.
- `FaceDepthVisual`로 객체 깊이를 Scale과 MaterialPropertyBlock 값으로 변환했다. 정적 구성은 `Update`를 사용하지 않는다.
- `FaceDepthSprite.shader`에서 객체별 로컬 왜곡과 `0~3px` Point 기반 블러를 적용했다.
- 검증 결과는 Background `0.92 / 3px`, FarActors `0.96 / 2px`, Subject `1.00 / 0px`, Foreground `1.02 / 1px`, WorldUI `1.10 / 0px`다.
- `Capture_Front` 한 대가 Canvas와 SpriteRenderer를 FaceFront RenderTexture에 함께 출력하고 기존 면 렌즈가 합성 결과 전체에 적용되는 것을 Play Mode에서 확인했다.
- ViewerCamera가 Front 캡처 전용 Layer 8을 직접 렌더링하지 않는 것을 확인했다.
- 테스트 씬 검증, C# 진단, Play Mode Console에서 오류와 경고가 없음을 확인했다.
- 로컬 캡처는 `Assets/Screenshots/UnifiedFaceView/UnifiedFaceView_Final.png`에 저장했다. `Assets/Screenshots`는 Git 제외 대상이다.

아직 구현하지 않은 항목:

- 렌즈 왜곡을 반영한 `FaceLensPointerMapper`
- 테스트 선택지의 실제 Button 이벤트
- Collider2D 기반 월드 Sprite 입력
- 실제 아트와 SpriteAtlas Padding 검증
- Front 이외 면으로 확장

## 17. Perspective 단일 카메라 대안 테스트

### 2026-09-03

- 비교용 `Assets/Scenes/PerspectiveCameraLensPrototype.unity` 씬을 새로 생성했다.
- 기존 육면체, RenderTexture, 면별 캡처와 객체별 효과 스크립트는 사용하지 않았다.
- `PerspectiveLensCamera`, `Stage`, `LensVolume` 세 개의 루트만 유지했다.
- `Stage` 아래에 World Space Canvas와 깊이가 다른 SpriteRenderer를 함께 배치했다.
- 한 Perspective 카메라가 Canvas와 SpriteRenderer를 직접 렌더링한다.
- Z 거리에 따른 실제 투영만으로 Background, FarActor, Subject, Foreground의 크기 차이를 만들었다.
- 카메라 후처리는 URP Volume의 Lens Distortion, Chromatic Aberration, Vignette만 사용했다.
- 임시 시각 요소는 공유 단색 Sprite와 TextMeshPro 라벨로만 구성했다.
- 카메라 FOV는 `46`, Lens Distortion은 `-0.42`, Chromatic Aberration은 `0.28`, Vignette는 `0.25`로 강화했다.
- 객체별 효과는 `FaceDepthSprite.shader`를 공유하고 역할별 Material Asset으로 값을 분리했다.
- Background는 `Blur 3 / Warp +0.35`, FarActor는 `Blur 2 / Warp +0.18`을 사용한다.
- Subject는 `Blur 0 / Warp -0.16`, Foreground는 `Blur 1 / Warp -0.28`을 사용한다.
- 별도 런타임 스크립트 없이 씬과 Volume 설정만으로 동작한다.
- Play Mode에서 원근 투영과 전체 화면 렌즈 왜곡이 함께 적용되는 것을 확인했다.
- 씬 검증, VolumeProfile 재로드, Console 오류와 경고 검사를 통과했다.

이 방식은 구조가 가장 단순하고 실제 원근을 바로 얻을 수 있다. 반면 World Space UI도 카메라 투영과 후처리 영향을 함께 받으므로, 가장자리 UI 배치와 입력 좌표는 렌즈 안전 영역을 기준으로 설계해야 한다.

## 18. 단일 카메라 6면체 테스트 재개 — 2026-09-06

- 씬: `Assets/Scenes/PerspectiveCubeViewPrototype.unity`
- 루트: `ViewRig`(카메라·전환 컨트롤러), `CubeFaces`(6면), `LensVolume`.
- 면마다 Surface, SampleObject, Canvas만 둔다. 단색 면과 Sprite, 텍스트로 구분한다.
- 카메라 1대가 모든 면을 직접 렌더링한다. 면별 RenderTexture는 없다.
- 수평 FOV 80으로 기존 렌즈 확대 상태에서도 이웃 면이 보이도록 조정했다. 수직 FOV는 90이다.
- 6면 Canvas는 모두 1200×675(16:9)다. 재개 당시 Top/Bottom Surface는 12×12였고, Bottom은 아래 추가 작업에서 16:9로 변경했다.
- Canvas 정렬 순서를 20으로 올려 Sprite(10)에 가려지던 객체 라벨을 수정했다.
- `PerspectiveCubeViewController`에서 DOTween으로 회전·FOV·Volume Weight를 함께 전환한다.
- A/D·좌우 화살표: Front → Right → Back → Left 순환. 우클릭 드래그도 지원한다.
- Front에서 W/위로 Top, S/아래로 Bottom에 진입한다. Top에서는 아래만, Bottom에서는 위만 Front로 복귀한다.
- Top/Bottom의 좌우 및 추가 수직 진입, 전환 중 중복 입력을 차단한다.
- Bottom에 도착하면 Volume Weight=0, 무왜곡 Sprite 머티리얼을 사용한다. 복귀 시 Weight=1로 복원한다.
- 비활성화 시 트윈을 정리하고 회전·FOV·Volume Weight를 목표 상태로 맞춘다.
- Play Mode에서 여섯 면의 상태 전환, 한 바퀴 순환, 역방향 순환, 금지 입력, 비활성화 중단을 검증했다.
- Front/Right/Top/Bottom 출력을 캡처해 확인했다. 캡처는 Git에서 제외된 `Assets/Screenshots/PerspectiveCubeView`에 있다.

테스트 범위와 한계:

- 현재 조작은 키보드·우클릭 드래그다. 공통 마우스 가장자리 버튼과 면별 클릭 기능은 포함하지 않는다.
- 카메라 렌즈는 화면 전체에 공통 적용된다. 객체별 차이는 Sprite 머티리얼의 로컬 왜곡·블러이며, 거리 자동 연동은 없다.
- Bottom이 이웃 면으로 살짝 보일 때도 공통 카메라 렌즈는 적용된다. Bottom만 바라보는 상태에서 후처리가 꺼진다.
- 원근 및 후처리 재샘플링이 있으므로 정수배 픽셀 퍼펙트 출력을 보장하는 씬은 아니다.

### Bottom을 Front 하단에 접합 — 2026-09-06

- 기존 `CubeScreenPrototype`과 같은 배치 방식으로 Bottom Surface를 12×6.75(16:9)로 줄였다. Top은 변경하지 않았다.
- `Face_Bottom`을 (0, -3.375, 2.625)에 배치해 위쪽 모서리가 Front 하단(z=6)에 맞닿는다. Canvas와 Sprite도 함께 이동한다.
- Bottom 진입 시 ViewRig가 Front 방향으로 2.625 이동하고, 복귀 시 원위치로 돌아온다. 기존 DOTween 시퀀스에 위치 보간을 추가했다.
- FOV와 렌즈 설정은 유지한다. 16:9 출력에서 Bottom 단독 전체 화면과 Front에서 Bottom 헤더 일부 노출을 캡처로 확인했다.
- Front 복귀, Bottom 금지 입력, 전환 중 비활성화 시 목표 위치 정리를 Play Mode에서 검증했다.
- Bottom 뒤쪽(z=-0.75)과 Back(z=-6) 사이에는 바닥이 없다. 기존 씬과 같은 비대칭 구조이며, 다른 면 하단에서 열린 영역이 보일 수 있다.
