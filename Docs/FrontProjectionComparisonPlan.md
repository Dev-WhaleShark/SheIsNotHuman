# Front Orthographic / Perspective 비교 계획

## 1. 문서 목적

Bottom을 제외한 육면체 화면에 `UI → 탁자 → 중심 인물 → 행인 → 배경` 순서의 2.5D 콘텐츠를 배치할 예정이다. 구현 전에 Front 한 면만 사용해 Orthographic과 Perspective 방식을 각각 비교하고, 최종 제작 방식을 결정한다.

이 문서는 향후 작업자가 바로 비교 씬 제작을 시작할 수 있도록 다음 내용을 정리한다.

- 비교 씬 구성과 파일명
- 공통 Front 테스트 무대 구조
- Orthographic 접근법
- Perspective 접근법
- 거리 기반 픽셀 블러와 기존 렌즈 효과의 역할 분리
- 구현·검증 순서
- 현재까지의 조사 및 작업 로그

## 2. 현재 프로젝트 기준

| 항목 | 현재 상태 |
| --- | --- |
| 기준 씬 | `Assets/Scenes/CubeScreenPrototype.unity` |
| Front 캡처 카메라 | `Capture_Front`, Orthographic |
| Front 카메라 위치 | 로컬 `(0, 0, 7)` |
| Front Canvas 위치 | 로컬 `(0, 0, 8)` |
| Front 카메라 범위 | Near `0.01`, Far `2`, Orthographic Size `4.5` |
| Front RenderTexture | `640×360`, Point Filter |
| Front 콘텐츠 레이어 | Layer 8 |
| 최종 렌즈 | `CubeFaceLensDisplay` + `CubeFaceLens.shader` |
| Bottom | 일반 2D 화면으로 유지하며 비교 대상에서 제외 |

현재 모든 `Capture_*` 카메라는 Orthographic이다. 따라서 오브젝트를 카메라에서 멀리 배치해도 화면상 크기는 자동으로 변하지 않는다. 기존 `CubeFaceLens`는 각 오브젝트가 아니라 완성된 면 전체 RenderTexture에 적용된다.

## 3. 생성할 비교 씬

원본 씬은 수정하지 않고 다음 두 복사본을 만든다.

| 씬 | 목적 |
| --- | --- |
| `Assets/Scenes/CubeScreenPrototype_Orthographic.unity` | Orthographic 기반 수동 2.5D 연출 검증 |
| `Assets/Scenes/CubeScreenPrototype_Perspective.unity` | Perspective 기반 실제 원근 연출 검증 |

복사 시점에는 두 씬이 완전히 같아야 한다. 이후 `Capture_Front`와 Front 테스트 오브젝트만 다르게 구성한다. Right, Back, Left, Top, Bottom, 화면 이동 규칙과 공통 내비게이션은 수정하지 않는다.

씬 복사는 실행 중인 Unity Editor에서 `EditorSceneManager.SaveScene(..., saveAsCopy: true)`를 사용한다. 원본 씬에 미저장 변경 사항이 있다면 그 상태까지 복사하되 원본 자체는 저장하거나 변경하지 않는다. `.unity` YAML 직접 편집은 피한다.

## 4. 공통 Front 테스트 무대

두 비교 씬에는 같은 이름과 역할을 가진 테스트 루트를 추가한다.

```text
FrontProjectionStudy
├─ Depth_00_Background
├─ Depth_10_Passerby
├─ Depth_20_Subject
├─ Depth_30_Desk
├─ Depth_40_WorldUI
└─ ScreenUI
```

### 깊이와 정렬의 역할

- 로컬 Z 또는 카메라 기준 View Depth: 원근과 블러 강도 결정
- `SortingGroup` / `Sorting Order`: 같은 깊이에서의 겹침 순서 결정
- Scale: Orthographic의 수동 원근 보정 또는 아트 크기 조정

Sorting Order만 변경해서는 거리 기반 효과가 생기지 않는다. 반대로 Z 위치만 믿으면 투명 스프라이트의 정렬이 흔들릴 수 있으므로 두 값을 함께 관리한다.

### 1차 테스트용 기준값

비교의 출발점으로 다음 값을 사용한다. 카메라는 비교 씬에서 로컬 Z `0`에 두고 Front 배경 면은 기존 Z `8` 부근을 유지한다.

| 그룹 | 카메라 기준 Z | 테스트 크기 X×Y | 정렬 기준 | 블러 목표 |
| --- | ---: | ---: | ---: | ---: |
| WorldUI | 2.2 | 2.6×1.1 | 400 | 0px 또는 블러 제외 |
| Desk | 3.6 | 6.5×1.2 | 300 | 1px |
| Subject | 5.2 | 1.8×4.2 | 200 | 0px, 초점 기준 |
| Passerby | 6.3 | 1.3×4.0 | 100 | 2px |
| Background | 7.9 | 15.6×8.4 | 0 | 3px |

이 값은 최종 아트 규격이 아니라 비교용 시작값이다. 처음에는 두 씬에 같은 위치와 크기를 사용해 투영 방식의 차이만 확인한다.

## 5. Orthographic 접근법

### 카메라

- `Capture_Front.orthographic = true`
- Orthographic Size는 `4.5`부터 시작
- 테스트 깊이를 수용할 수 있도록 카메라를 로컬 Z `0`으로 이동
- Near/Far는 약 `0.1 / 10`으로 확장
- Front 전용 Layer 8만 렌더링

### 표현 방식

Orthographic에서는 Z가 달라도 화면상 크기가 변하지 않는다. 따라서 다음 값을 아트 디렉션으로 직접 조정한다.

- 가까운 탁자와 인게임 UI는 Scale을 키움
- 먼 행인과 배경은 Scale을 줄임
- 가까운 레이어는 화면 중심에서 바깥으로 약간 이동시켜 광각 느낌 보강
- 최종 면 전체 휘어짐은 기존 `CubeFaceLens`가 담당
- 블러는 실제 View Depth를 기준으로 계산

### 장점

- 픽셀 크기와 실루엣을 정확하게 제어하기 쉬움
- 카메라 이동이 없을 때 결과가 안정적임
- UI 좌표와 클릭 영역을 관리하기 쉬움
- 아티스트가 의도한 오더와 크기를 그대로 유지하기 쉬움

### 단점

- 카메라 거리만으로 자연스러운 크기 변화가 생기지 않음
- 레이어마다 Scale과 위치를 수동으로 보정해야 함
- 깊이별 광각 변형이 필요하면 별도의 레이어 왜곡 또는 합성 패스가 필요함

### 권장 용도

면 내부 카메라가 고정되어 있고, 픽셀 아트의 실루엣과 화면 구도를 우선할 때 적합하다. 현재 게임 구조에는 우선순위가 높은 후보이다.

## 6. Perspective 접근법

### 카메라

- `Capture_Front.orthographic = false`
- 카메라를 로컬 Z `0`으로 이동
- Near/Far는 약 `0.1 / 10`으로 설정
- FOV는 `55~60°`부터 시작
- Front 전용 Layer 8만 렌더링

이 FOV는 육면체를 바라보는 `ViewerCamera`의 FOV와 별개다. Front 콘텐츠를 RenderTexture로 촬영하는 내부 카메라 값이다.

### 표현 방식

- 가까운 WorldUI와 Desk는 자동으로 크게 보임
- Subject를 초점 거리 기준으로 사용
- Passerby와 Background는 거리에 따라 자연스럽게 작아짐
- 기존 `CubeFaceLens`는 Perspective 렌더 결과 전체에 동일하게 적용
- 거리 블러는 Orthographic 씬과 같은 프로필을 사용

### 장점

- Z 배치만으로 자연스러운 크기 변화와 투시가 생김
- 카메라나 객체가 움직일 때 원근 변화가 자동으로 유지됨
- 근경 오브젝트의 과장된 크기를 만들기 쉬움

### 단점

- 가까운 오브젝트가 예상보다 급격히 커지거나 화면 밖으로 잘릴 수 있음
- 픽셀 스프라이트가 서브픽셀 위치와 스케일에 놓이기 쉬움
- UI와 클릭 영역을 별도로 보정해야 할 가능성이 큼
- 기존 Front 구도를 맞추기 위해 FOV, 카메라 거리, 각 레이어 Scale을 함께 조정해야 함

### 권장 용도

면 내부 카메라 또는 오브젝트가 자주 움직이고, 실제 투시 변화가 게임 연출의 일부일 때 적합하다.

## 7. 공정한 비교 방법

처음부터 각 방식에 맞춘 보정을 넣으면 투영 방식과 아트 보정의 차이를 구분하기 어렵다. 다음 두 단계로 비교한다.

### 1차: 투영 방식만 비교

- 두 씬에 같은 오브젝트 위치와 Scale 사용
- 같은 Front 렌즈 프로필 사용
- 블러 비활성화
- Orthographic/Perspective와 카메라 파라미터만 다르게 설정

확인할 항목은 원근감, 화면 잘림, 픽셀 흔들림, 기존 UI 가독성이다.

### 2차: 각 방식별 최적화 비교

- Orthographic: 레이어별 Scale과 위치 수동 보정
- Perspective: FOV와 카메라 거리, 레이어 기본 크기 보정
- 두 씬에 같은 거리 기반 블러 프로필 적용
- 기존 Front 렌즈 파라미터는 비교가 끝날 때까지 동일하게 유지

최종 선택은 보정 이후 결과와 제작 편의성까지 포함해 판단한다.

## 8. 거리 기반 블러 설계

거리는 일반적인 `Vector3.Distance`가 아니라 카메라가 바라보는 방향의 깊이를 사용한다.

```csharp
viewDepth = Vector3.Dot(
    objectPosition - cameraPosition,
    cameraForward);
```

화면 가장자리의 오브젝트가 단순히 대각선 거리가 길다는 이유로 더 흐려지는 문제를 방지하기 위해서다.

### 프로토타입

URP 기본 Depth of Field로 초점 거리와 레이어 배치가 적절한지만 빠르게 확인할 수 있다.

### 최종 후보

픽셀 게임 최종본은 `PixelDepthBlurRendererFeature` 형태의 전용 Render Graph 패스를 권장한다.

- `640×360` 내부 해상도에서 실행
- Point 기반 정수 픽셀 오프셋 사용
- 블러 반경을 `0, 1, 2, 3px`로 양자화
- 깊이가 크게 다른 픽셀의 샘플은 제외해 외곽 번짐 방지
- 투명 스프라이트는 Alpha Clip이 포함된 DepthOnly 패스 제공
- ScreenUI는 블러 후에 합성해 선명하게 유지

현재 URP Asset은 Depth Texture가 꺼져 있다. 최종 패스 구현 시 전역 설정을 무조건 켜기보다 Front 등 효과를 사용하는 캡처 카메라 또는 Renderer Feature에서만 Depth 입력을 요청한다.

## 9. 렌즈와 UI 처리 순서

권장 렌더링 순서는 다음과 같다.

```text
Front 월드 오브젝트
→ 거리 기반 픽셀 블러
→ Front ScreenUI 합성
→ FaceFront RenderTexture
→ 기존 CubeFaceLens
→ ViewerCamera
→ PixelFrame
→ 공통 상하좌우 내비게이션
```

- `CubeFaceLens`: 면 전체의 광각 왜곡, 색수차, 비네팅 담당
- 거리 효과: 오브젝트의 View Depth에 따른 블러 담당
- ScreenUI: 블러 제외, 필요하면 최종 면 렌즈만 적용
- 공통 내비게이션: 모든 면 렌더링 이후의 최상위 UI로 유지

강한 렌즈 왜곡을 적용하면 보이는 버튼과 실제 Raycast 위치가 어긋날 수 있다. 비교 단계에서는 클릭 UI를 화면 가장자리에서 멀리 두고, 최종 단계에서 역왜곡 포인터 보정 여부를 결정한다.

## 10. 향후 코드·에셋 구성안

| 파일 또는 구성 | 역할 |
| --- | --- |
| `FaceStageController.cs` | 면 카메라와 깊이 무대 연결, Bottom 효과 제외 |
| `FaceDepthProfile.cs` | FOV, 초점 거리, 초점 범위, 블러 곡선과 최대 반경 저장 |
| `FaceDepthLayer.cs` | 레이어 종류, 깊이 앵커, Sorting Order, 효과 예외 설정 |
| `PixelDepthBlurRendererFeature.cs` | URP Render Graph 거리 기반 픽셀 블러 |
| `PixelDepthSprite.shader` | 스프라이트 색상 렌더와 Alpha Clip DepthOnly 패스 |
| `Assets/CubeScreen/Comparison` | 비교용 머티리얼, 프로필, 플레이스홀더 보관 |

각 오브젝트마다 별도 카메라와 RenderTexture를 만드는 방식은 피한다. Front 캡처 카메라 한 대와 깊이 버퍼, 후처리 패스를 공유하는 것을 기본으로 한다.

## 11. 구현 순서

1. Git 상태와 원본 씬의 미저장 변경 여부 확인
2. 원본 씬을 Orthographic/Perspective 씬으로 복사
3. 두 씬의 Front에 동일한 `FrontProjectionStudy` 플레이스홀더 구성
4. 1차 투영 방식 비교 및 캡처
5. Orthographic 수동 Scale 보정
6. Perspective FOV·카메라 거리 보정
7. Subject를 기준으로 두 화면의 중심 크기를 비슷하게 맞춤
8. URP 기본 DoF로 깊이 구간 임시 검증
9. 픽셀 전용 블러 패스 구현 및 두 씬에 동일 적용
10. UI 가독성·Raycast·렌즈 왜곡 확인
11. 선택한 방식을 Front 실제 콘텐츠 구조로 승격
12. Left, Right, Back, Top 순서로 확장하고 Bottom은 제외

## 12. 검증 체크리스트

- 원본 `CubeScreenPrototype.unity`가 변경되지 않았는가
- 두 비교 씬에서 Front 외의 면이 원본과 같은가
- Bottom이 일반 2D 화면으로 유지되는가
- Orthographic에서 Z를 변경해도 크기가 변하지 않는가
- Perspective에서 가까운 객체가 자연스럽게 커지는가
- 두 씬의 Subject 화면상 크기가 비교 가능한 수준으로 맞춰졌는가
- 블러가 640×360 픽셀 단위로 적용되는가
- 캐릭터 외곽에 배경색이 번지지 않는가
- SpriteAtlas 인접 영역이 블러 샘플에 섞이지 않는가
- ScreenUI와 공통 내비게이션이 선명하고 클릭 가능한가
- 정면에서 인접 면이 보일 때 기존 면별 렌즈 효과가 유지되는가
- Console에 오류가 없는가

## 13. 결정 기준

| 기준 | Orthographic 우세 조건 | Perspective 우세 조건 |
| --- | --- | --- |
| 픽셀 안정성 | 실루엣과 픽셀 크기를 엄격히 유지해야 함 | 서브픽셀 변화가 허용됨 |
| 카메라 움직임 | 면 내부 카메라가 거의 고정 | 면 내부 카메라도 움직임 |
| 제작 방식 | 레이어별 수동 보정 선호 | 실제 Z 배치 중심 제작 선호 |
| 연출 | 정적인 일러스트·비주얼 노벨형 | 강한 전경 이동·원근 변화 |
| UI | 화면 내 상호작용이 많음 | 월드 오브젝트 상호작용이 중심 |

현재 기획처럼 면 카메라가 고정되고 픽셀 아트와 UI 비중이 높다면 Orthographic이 우선 후보지만, 실제 비교 씬 결과를 본 뒤 확정한다.

## 14. 작업 로그

### 2026-09-03

- Orthographic 방식을 우선 후보로 결정하고 객체별 원근·렌즈·픽셀 블러 구현 경로를 검토했다.
- 현재 Renderer2D에 Renderer Feature와 Volume이 없고 `Capture_Front`의 후처리·Depth Texture가 비활성화된 것을 확인했다.
- 첫 구현은 URP 기본 DoF가 아니라 Transform 기반 가짜 원근과 객체 셰이더 기반 렌즈·정수 픽셀 블러를 사용하는 방향으로 결정했다.
- 상세 설계와 단계별 구현 순서를 `Docs/OrthographicObjectEffectsPlan.md`에 작성했다.
- UI와 SpriteRenderer를 하나의 `Capture_Front`에서 촬영하고 기존 `CubeFaceLens`로 최종 합성하는 통합 구조를 `Docs/UnifiedFaceViewDesign.md`에 정리했다.
- 현재 `CubeFaceGraphicRaycaster`에는 렌즈 UV 보정이 없으므로 강한 렌즈에서도 UI 입력 위치를 맞추는 포인터 매퍼가 필요함을 기록했다.
- `UnifiedFaceViewPrototype.unity`를 생성해 색상 Sprite, Canvas 팝업, 깊이별 Scale·블러와 기존 Front 렌즈의 통합 출력을 검증했다.

### 2026-09-02

- Unity MCP 연결 상태에서 원본 `CubeScreenPrototype.unity`를 복사해 Orthographic/Perspective 비교 씬 두 개를 생성했다.
- 두 씬의 Front에 동일한 `FrontProjectionStudy` 깊이 무대를 구성하고, `Background → Passerby → Subject → Desk → WorldUI` 순서로 플레이스홀더를 배치했다.
- 비교용 URP Unlit 머티리얼 5개를 `Assets/CubeScreen/Comparison/Materials`에 생성했다.
- 두 씬 모두 `Capture_Front`를 로컬 Z `0`, Near/Far `0.1 / 10`으로 맞췄다. Orthographic 씬은 Size `4.5`, Perspective 씬은 FOV `60`으로 설정했다.
- 1차 비교 원칙에 따라 오브젝트 위치와 크기는 동일하게 유지하고, 거리 블러와 방식별 화면 보정은 아직 적용하지 않았다.
- 투영 차이를 가리던 테스트용 3D 텍스트는 제거했다. 최종 비교 캡처는 `Assets/Screenshots/ProjectionComparison`에 저장했다. 이 폴더는 `.gitignore` 대상이므로 캡처는 로컬 검증용이다.
- Orthographic에서는 깊이와 무관하게 크기가 유지되고, Perspective에서는 가까운 WorldUI와 Desk가 크게 확대·잘리는 결과를 확인했다.
- 두 비교 씬의 카메라 모드, 깊이 그룹 수, 클리핑 범위를 자동 검증했으며 Unity Console 오류가 없는지 확인했다.
- 작업 후 원본 `Assets/Scenes/CubeScreenPrototype.unity`로 돌아왔고 원본 씬은 Dirty 상태가 아님을 확인했다.

### 2026-08-30

- 첨부 스케치를 기준으로 `UI → 탁자 → 중심 인물 → 행인 → 배경` 깊이 구성을 분석했다.
- 현재 씬의 Front 캡처 카메라가 Orthographic이며, 카메라 Z `7`, Canvas Z `8`, Far Clip `2`, Orthographic Size `4.5`인 것을 확인했다.
- Front RenderTexture가 `640×360`, Point Filter이고 기존 면 전체 렌즈 셰이더가 별도로 존재함을 확인했다.
- 프로젝트 URP Asset에서 Depth Texture와 HDR이 비활성화된 상태를 확인했다. 이번 효과에는 HDR이 필수는 아니며, Depth는 효과를 사용하는 캡처 패스에만 요청하는 방향으로 정리했다.
- 원본 씬을 복사해 Orthographic/Perspective 비교 씬을 실제 생성하는 작업을 검토했다.
- 현재 Codex 세션에는 Unity MCP 도구가 노출되지 않았고 로컬 MCP 포트도 열려 있지 않았으며, Unity Pipeline CLI도 설치되어 있지 않은 것을 확인했다.
- 사용자의 요청 변경에 따라 씬과 비교용 에셋은 생성하지 않고 본 설계 문서만 작성했다.
- 기존 작업 중인 `Assets/CubeScreen/Runtime/CubeScreenController.cs` 변경은 유지했으며 이번 문서 작업에서 수정하지 않았다.

## 15. 다음 작업 재개 지점

다음 작업자는 우선 다음 순서로 시작한다.

1. `Docs/CubeScreenPrototype.md`와 이 문서를 읽는다.
2. `git status`와 Unity Console을 확인한다.
3. `CubeScreenPrototype_Orthographic.unity`와 `CubeScreenPrototype_Perspective.unity`의 Front 캡처 결과를 나란히 비교한다.
4. 두 씬에서 Subject의 화면상 크기가 비슷해지도록 2차 보정을 진행한다.
5. Orthographic은 레이어별 Scale·위치를, Perspective는 FOV·카메라 거리·기본 크기를 조정한다.
6. 보정값을 기록한 뒤 URP 기본 DoF로 초점 거리와 깊이 구간만 임시 검증한다.
7. 투영 방식을 선택한 후 픽셀 전용 거리 블러 구현을 시작한다.

1차 투영 비교 씬은 완성되었다. 다음 단계에서도 거리 블러부터 구현하지 말고, 먼저 두 방식의 화면 구도와 제작 편의성을 비교해 투영 방식을 확정한다.
