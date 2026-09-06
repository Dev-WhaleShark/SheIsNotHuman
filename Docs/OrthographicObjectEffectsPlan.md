# Orthographic 객체별 깊이 효과 설계

> UI와 SpriteRenderer를 한 카메라에서 합성하고 기존 면 렌즈·입력까지 연결하는 최종 구조는 `Docs/UnifiedFaceViewDesign.md`를 기준으로 한다.

## 1. 목표

`CubeScreenPrototype_Orthographic.unity`의 Front 화면에서 Orthographic 카메라는 유지하면서 객체마다 다음 효과를 독립적으로 설정한다.

- 가짜 원근감: 깊이에 따른 크기와 화면 중심 기준 위치 보정
- 객체 로컬 렌즈: 오목·볼록 왜곡과 가장자리 확대
- 픽셀 블러: `0~3px` 정수 반경의 거리 기반 흐림
- 효과 제외: 중심 인물, 클릭 UI처럼 선명해야 하는 객체

기존 `CubeFaceLens`는 완성된 면 전체의 렌즈 효과를 계속 담당한다. 새 객체 효과는 `Capture_Front`가 RenderTexture를 만들기 전에 적용한다.

## 2. 현재 구성에서 확인한 사항

- Unity `6000.5.4f1`, URP `17.6.0`, Renderer2D를 사용한다.
- `Capture_Front`는 Orthographic이며 Size `4.5`, Near/Far `0.1 / 10`이다.
- Front RenderTexture는 `640×360`, Point Filter다.
- Front 테스트 객체는 카메라 기준 Z `2.2~7.9`에 배치되어 있다.
- Renderer Feature와 Volume은 현재 없고, `Capture_Front`의 후처리와 Depth Texture도 꺼져 있다.
- 기존 `CubeFaceLens.shader`는 Front 내부 객체가 아니라 완성된 면 RenderTexture 전체를 왜곡한다.
- Renderer2D는 현재 카메라 스태킹을 지원하지 않으므로 ScreenUI 분리는 카메라 스택을 전제로 설계하지 않는다.

Orthographic에서도 카메라 방향 기준 깊이는 존재한다. 화면상 크기가 자동으로 달라지지 않을 뿐, 아래 값으로 효과 강도를 계산할 수 있다.

```csharp
viewDepth = Vector3.Dot(
    objectPosition - cameraPosition,
    cameraForward);
```

## 3. 권장 방식

### 기본: 객체 셰이더 + MaterialPropertyBlock

각 SpriteRenderer 또는 MeshRenderer는 하나의 공유 셰이더를 사용하고, 객체별 값만 `MaterialPropertyBlock`으로 전달한다.

객체별 주요 값:

- `_PixelBlurRadius`: `0, 1, 2, 3` 정수 단계
- `_LocalWarp`: 객체 내부 오목·볼록 왜곡 강도
- `_WarpCenter`: 객체 로컬 렌즈 중심
- `_EffectOpacity`: 효과 혼합 비율
- `_SpriteRect`: SpriteAtlas 샘플 영역 제한

장점:

- 별도 카메라와 RenderTexture를 객체마다 만들 필요가 없다.
- 같은 머티리얼을 공유하면서 객체마다 다른 효과를 줄 수 있다.
- `640×360` 픽셀 화면에서 블러 반경을 정확히 양자화할 수 있다.
- Renderer2D의 투명 스프라이트 정렬을 그대로 사용할 수 있다.

제약:

- 객체가 여러 SpriteRenderer로 구성되면 각 스프라이트가 개별적으로 휘어진다.
- 강한 왜곡은 원본 Sprite 영역 밖을 샘플링하므로 투명 여백이나 Atlas Padding이 필요하다.
- 보이는 그림만 휘어지므로 Collider와 UI Raycast 영역은 자동으로 변하지 않는다.

### 예외: 객체 그룹 RenderTexture 합성

캐릭터처럼 여러 스프라이트가 하나의 그림처럼 함께 휘어져야 할 때만 그룹을 임시 RenderTexture로 촬영한 뒤 하나의 Quad로 합성한다.

적용 후보:

- 여러 파츠로 구성된 대형 캐릭터
- 여러 SpriteRenderer가 하나의 유리·볼록렌즈 안에 들어가는 연출
- 객체 외곽까지 강하게 굽혀야 하는 특수 이벤트

일반 객체마다 이 방식을 사용하면 카메라, RenderTexture, 합성 패스가 늘어나므로 기본 방식으로 사용하지 않는다.

## 4. 가짜 원근감

Orthographic에서는 Z에 따른 크기 변화가 없으므로 시각 루트의 Scale과 화면 중심 기준 위치를 직접 보정한다.

```text
논리 루트
├─ VisualRoot       ← 크기·위치·셰이더 효과 적용
└─ InteractionRoot  ← Collider·Raycast 기준 유지
```

깊이를 `0~1`로 정규화한다.

```csharp
depth01 = Mathf.Clamp01(
    (farDepth - viewDepth) / (farDepth - nearDepth));
```

- `0`: 먼 배경
- `1`: 카메라에 가까운 전경

추천 보정:

- `VisualRoot.localScale = baseScale * scaleCurve(depth01)`
- 화면 중심에서 떨어진 방향으로 `spreadCurve(depth01)`만큼 위치 확대
- 가까운 탁자는 아래쪽 화면 밖으로 일부 잘리도록 의도적으로 확대
- Subject는 기준 크기 `1.0`과 선명도 `0px` 유지

이 방식은 실제 Perspective가 아니라 연출용 2.5D다. 대신 픽셀 크기와 구도를 정확히 통제할 수 있다.

## 5. 객체 로컬 렌즈

객체 로컬 UV를 렌즈 중심에서 떨어진 거리로 변형한다.

```text
offset = uv - warpCenter
radius = dot(offset, offset)
warpedUv = warpCenter + offset * (1 + localWarp * radius)
```

이 효과는 카메라 렌즈의 물리적 재현이 아니라 객체별 스타일 왜곡이다.

- 약한 값: 전경이 살짝 볼록해 보이는 효과
- 음수 값: 중심으로 눌리는 오목 효과
- 강한 값: 이미지 가장자리 잘림과 Atlas Bleeding 발생 가능

객체별 렌즈는 약하게 사용하고, 화면 전체의 주된 광각 표현은 기존 `CubeFaceLens`에 맡긴다. 두 왜곡을 모두 강하게 적용하면 픽셀 외곽과 클릭 위치가 크게 어긋난다.

## 6. 픽셀 블러

일반 Gaussian Blur 대신 Point 샘플과 정수 픽셀 오프셋을 사용한다.

- 반경 `0`: 원본 유지
- 반경 `1`: 상하좌우 또는 3×3 샘플
- 반경 `2`: 성능을 고려한 고정 탭 샘플
- 반경 `3`: 배경 전용, 필요할 때만 사용

필수 처리:

- 텍스처 해상도의 역수로 정확히 한 픽셀씩 이동
- RGB와 Alpha를 함께 누적하고 Premultiplied Alpha 방식으로 복원
- SpriteAtlas 사용 시 UV를 Sprite Rect 안으로 제한
- Atlas Padding/Extrude를 최대 블러 반경보다 크게 확보
- Filter Mode는 Point 유지

URP 기본 Depth of Field는 1차 확인용으로도 우선순위가 낮다. 현재 후처리와 Depth Texture가 비활성화되어 있고, 활성화해도 면 전체에 부드러운 연속 블러를 적용하므로 픽셀 게임의 객체별 정수 블러와 맞지 않는다.

## 7. 데이터와 컴포넌트 구성안

### `OrthographicDepthEffectProfile`

ScriptableObject로 공통 연출 값을 저장한다.

- Near/Far Depth
- Scale Curve
- Radial Spread Curve
- Warp Curve
- Blur Step Curve
- 최대 블러 반경

### `OrthographicDepthObject`

객체 또는 깊이 그룹에 부착한다.

- Profile 참조
- `VisualRoot` 참조
- 효과 대상 Renderer 목록
- 자동 깊이 또는 수동 Depth Override
- Scale, Spread, Warp, Blur 개별 Override
- 렌즈·블러·원근 효과 제외 옵션
- Inspector에서 즉시 결과를 갱신하는 Odin 버튼

정적인 객체는 `Update`를 사용하지 않는다. `OnEnable`, `OnValidate`, 씬 구성 변경 시에만 계산한다. 움직이는 객체만 R3로 위치나 깊이 변화를 관찰하고 값이 달라졌을 때 갱신한다.

### `OrthographicDepthSprite.shader`

Renderer2D용 Unlit 셰이더로 다음 순서로 처리한다.

```text
Sprite UV
→ 객체 로컬 렌즈 왜곡
→ Sprite Rect 제한
→ 정수 픽셀 블러
→ Tint·Alpha 출력
```

## 8. 권장 초기 프리셋

| 그룹 | Scale | 화면 중심 확장 | 로컬 렌즈 | 블러 |
| --- | ---: | ---: | ---: | ---: |
| Background | 0.94 | 0.98 | -0.01 | 3px |
| Passerby | 0.97 | 0.99 | -0.01 | 2px |
| Subject | 1.00 | 1.00 | 0.00 | 0px |
| Desk | 1.08 | 1.04 | 0.02 | 1px |
| WorldUI | 1.02 | 1.01 | 0.00 | 0px |
| ScreenUI | 1.00 | 1.00 | 0.00 | 제외 |

이 값은 플레이스홀더 확인용 시작점이다. 실제 스프라이트가 들어오면 픽셀 크기를 기준으로 다시 조정한다.

## 9. 렌더링 순서

```text
객체별 VisualRoot 가짜 원근 보정
→ 객체 셰이더의 로컬 렌즈·픽셀 블러
→ Capture_Front RenderTexture
→ 기존 CubeFaceLens 면 전체 렌즈
→ ViewerCamera
→ PixelFrame
→ 공통 내비게이션 UI
```

`ScreenUI`는 객체 효과에서 제외한다. 다만 Front RenderTexture 안에 있다면 기존 면 전체 렌즈는 적용된다. 완전히 왜곡되지 않는 UI가 필요하면 현재 PixelPresentation 단계처럼 면 렌더링 이후에 합성한다.

## 10. Renderer Feature 방식은 2차 후보

URP 17.6에서는 `ScriptableRendererFeature`와 `RecordRenderGraph` 기반 패스를 사용할 수 있다. 하지만 현재 프로젝트에는 Renderer Feature가 하나도 없고, 투명 Sprite의 객체별 효과 강도를 한 번의 Depth Buffer만으로 구분하기 어렵다.

Renderer Feature가 적합한 시점:

- 객체 수가 많아 셰이더 샘플 비용이 커질 때
- 깊이 그룹별로 한 번씩 전체 레이어를 블러할 때
- 효과 마스크 텍스처에 객체별 Effect ID를 기록하는 구조를 도입할 때

첫 구현은 객체 셰이더로 검증하고, 프로파일링 결과가 필요할 때 `PixelDepthBlurRendererFeature`로 승격한다.

## 11. 구현 단계

1. 플레이스홀더에 `VisualRoot`와 `InteractionRoot` 구조 적용
2. `OrthographicDepthEffectProfile`과 `OrthographicDepthObject` 구현
3. Scale과 화면 중심 확장만 먼저 검증
4. Point 기반 `OrthographicDepthSprite.shader` 추가
5. 객체별 `0~3px` 블러 검증
6. 약한 객체 로컬 렌즈 적용 및 잘림 확인
7. ScreenUI와 Raycast 영역이 영향을 받지 않는지 확인
8. Frame Debugger와 Profiler로 배치·오버드로우 확인
9. 여러 스프라이트가 함께 휘어야 하는 객체만 그룹 RenderTexture 적용
10. 필요할 때만 Render Graph 기반 레이어 블러로 확장

## 12. 판단

현재 프로젝트에는 다음 조합이 가장 적합하다.

- 원근감: Transform 기반 수동 2.5D
- 객체 렌즈: 공유 객체 셰이더 + MaterialPropertyBlock
- 객체 블러: 정수 픽셀 샘플 셰이더
- 복합 객체 특수 연출: 제한적인 그룹 RenderTexture
- 면 전체 렌즈: 기존 `CubeFaceLens`

이 구조는 Orthographic의 픽셀 안정성을 유지하면서 각 객체가 서로 다른 깊이감과 렌즈 특성을 갖도록 만들 수 있다.
