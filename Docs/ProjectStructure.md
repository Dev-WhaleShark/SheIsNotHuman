# 프로젝트 구조와 유지보수

현재 플레이 진입점은 `Assets/Scenes/PerspectiveCubeViewPrototype.unity`입니다. Unity 버전은 `ProjectSettings/ProjectVersion.txt`를 기준으로 하며 현재 6000.5.4f1입니다. 조작·Inspector 설정·검증 이력은 [InspectionMvp.md](InspectionMvp.md), 에이전트 작업 절차는 [AgentWorkflow.md](AgentWorkflow.md)에 있습니다.

## 직접 작성 코드

직접 작성 C#은 `Assets/InspectionMvp` 12개와 `Assets/CubeScreen/Runtime` 10개입니다. 여기에 `Assets/CubeScreen/Shaders`의 셰이더 2개가 있습니다. 설치한 패키지와 에셋의 코드는 이 목록에 포함하지 않습니다.

| 영역 | 역할과 수정 경계 |
| --- | --- |
| `InspectionMvp/Runtime/InspectionData.cs`, `InspectionNpcData.cs` | 문서·판정 자료형, UI에 의존하지 않는 검사 규칙, NPC별 대사와 문서 데이터 |
| `InspectionMvp/Runtime/InspectionFlowController.cs` | 상태·현재 NPC·판정 확정을 소유하는 유일한 진행 책임자 |
| `InspectionMvp/Runtime/InspectionPresentation.cs`, `InspectionMvpView.cs` | 진행이 기다리는 표시 작업의 계약과 구현. Text Animator 대사, DOTween 연출, 동일 원본 확대·복귀·취소 |
| `InspectionMvp/Runtime/IdentityDocumentView.cs`, `OrderDocumentView.cs` | 같은 문서 원본의 데이터 바인딩, 작은 표시와 상세 표시 전환 |
| `InspectionMvp/Runtime/DeskInspectableItem.cs` | 문서·더미의 클릭과 길게 누른 뒤 드래그, 검사대 경계 제한 |
| `InspectionMvp/Editor/InspectionMvpBuilder.cs` | 초기 씬 구성, 샘플 데이터와 한글 아틀라스 생성 도구 |
| `InspectionMvp/Editor/InspectionDeskMigration.cs`, `InspectionNavigationMigration.cs` | 기존 씬의 물품·원본 확대·시점 입력 연결을 보완하는 명시적 마이그레이션 |
| `InspectionMvp/Tests/Editor/InspectionRuleChecks.cs` | 메뉴에서 실행하는 고객 코드 규칙 15개 검사. NUnit 테스트 발견 수와 별도로 기록 |
| `CubeScreen/Runtime/PerspectiveCubeViewController.cs`, `CubeNavigationOverlay.cs` | 현재 Perspective 시점의 전환과 가장자리 방향 버튼 |
| `CubeScreen/Runtime/LensDistortionCoordinates.cs`, `DistortionCorrectedGraphicRaycaster.cs` | URP 화면 왜곡과 입력 좌표의 대응, 현재 면·전환 중 UI 입력 제한 |
| `CubeScreen/Runtime/CubeScreenController.cs`, `CubeFaceGraphicRaycaster.cs`, `CubeFaceLensDisplay.cs` | 면별 화면을 사용하는 다른 프로토타입의 표시·입력 구성 |
| `CubeScreen/Runtime/PixelPresentationViewport.cs`, `FaceDepthVisual.cs`, `FaceDepthEffectProfile.cs` | 화면 표시 영역과 깊이 표현·설정 |
| `CubeScreen/Shaders/CubeFaceLens.shader`, `FaceDepthSprite.shader` | 면 화면의 렌즈 표현과 깊이 스프라이트 렌더링. CPU 입력 보정과 서로 다른 렌즈 경로를 혼동하지 않도록 주의 |

## 데이터와 씬

`Assets/InspectionMvp/Samples`에서 NPC 대사와 문서 내용을 바꿉니다. `Prefabs/IdentityDocument.prefab`과 `OrderDocument.prefab`은 검사대에 놓고 카메라 앞으로 이동시키는 실제 원본입니다. 이름에 `Expanded`가 붙은 이전 프리팹은 현재 원본 확대에 사용하지 않지만 기존 생성·마이그레이션 경로와 함께 보존합니다.

`Assets/Scenes`에는 현재 MVP 외에도 `CubeScreenPrototype`, `PerspectiveCameraLensPrototype`, `UnifiedFaceViewPrototype`이 있습니다. 이들은 시점·화면 표현을 비교한 프로토타입입니다. 현재 MVP와 구조가 같다고 가정하거나, 현재 씬에 없다는 이유만으로 관련 스크립트·머티리얼·RenderTexture를 삭제하지 않습니다.

기존 MVP 씬을 실행하기 위해 Builder나 Migration을 다시 실행할 필요는 없습니다. 특히 초기 생성 도구는 개발 단계 기본값을 사용하므로 현재 저장된 NPC 3명·애니메이션 활성 설정과 구분합니다. 필요한 구조 변경이 있을 때 해당 범위의 Migration만 실행하고 씬·프리팹 변경을 확인합니다.

과거 `CODEX_HANDOFF.md`, `UnifiedFaceViewDesign.md` 등은 현재 저장소에 없습니다. 과거 작업 지침에서 이 이름을 만나도 현재 자료로 간주하거나 자동 복원하지 말고, 위의 현행 문서와 실제 씬·코드를 기준으로 판단합니다.

## 정리할 때 유지할 조건

진행 상태는 FlowController에서만 전환합니다. 판정은 연출보다 먼저 확정하여 연타를 차단하고, UI는 정답을 자동 선택하지 않습니다. 확대는 원본을 이동시키며 닫기·재시작·비활성화에서 부모, 드롭 위치, 크기와 서식을 복구합니다. 취소 시 트윈·코루틴·입력 구독의 수명을 함께 정리합니다.

시점 이동 잠금과 UI 입력 제한은 구분합니다. 확대 중에는 시점 이동을 막아도 닫기·판정 버튼이 동작해야 합니다. 현재 면 검사와 전환 중 차단은 왜곡 보정 레이캐스터가 담당하고, 클릭·드래그는 같은 좌표 변환을 사용합니다.

직렬화 필드·클래스 이름과 `.meta` GUID는 씬·프리팹 연결의 일부입니다. 사용처 검색만으로 직렬화 참조의 부재를 증명할 수 없으므로 이름 변경이나 삭제 전에 에셋 참조를 확인합니다. `Assets/Plugins`, 설치 패키지, `Library`, 생성 프로젝트 파일은 직접 작성 코드 정리 범위에서 제외합니다.

한글 아틀라스 생성 도구는 현재 C# 파일의 주석을 포함한 전체 텍스트에서 문자를 수집합니다. 주석 추가만으로 이미 저장된 아틀라스가 변경되지는 않지만, 이후 명시적으로 재생성하면 수집 문자 수가 늘어날 수 있습니다. 주석 정리를 위해 아틀라스를 다시 만들 필요는 없습니다.

## 2026-09-23 주석·구조 정리

기준 커밋은 `ebdb079ba90002558a709a294f10ff6b156806bc`이며 시작 시 작업 트리는 깨끗했습니다. 직접 작성 C# 22개와 셰이더 2개에 한국어 역할·공개 진입점·입력 잠금·수명 관리·좌표 변환 설명을 보강했습니다. 실행 코드, 렌더 수식, 공개 API, 직렬화 필드와 GUID를 변경하지 않았습니다. 복잡한 취소와 입력 경로는 동작을 보존하면서 읽을 수 있도록 이유와 불변조건을 주석으로 설명했습니다.

기존 `CubeScreenController`와 면 렌즈 컴포넌트는 `CubeScreenPrototype`·`UnifiedFaceViewPrototype`에서 실제 직렬화 참조가 확인되어 보존했습니다. 현재 씬의 데이터·프리팹·정적 한글 아틀라스와 벤더 코드는 다시 생성하거나 이동하지 않았습니다. 상세 작업 계약과 정적 검토 증거는 `tmp/agent-runs/project-cleanup-20260923/`에 있습니다.

빌드 씬 목록에 남아 있던 삭제된 `SampleScene.unity` 참조를 현재 `PerspectiveCubeViewPrototype.unity`로 교체했습니다. Unity Editor API로 적용하고 프로젝트 저장 후 디스크의 경로·GUID 두 줄만 바뀐 것을 확인했습니다. 다른 프로젝트 설정과 씬·프리팹은 변경하지 않았습니다.

프로젝트 감사에서는 중복 에셋 GUID와 직접 작성 소스의 누락 `.meta`가 없었습니다. 현재 MVP 씬은 에디터에서 누락 스크립트 0개, 직접 작성 셰이더 2개는 컴파일 오류 0개로 확인했습니다(`tmp/cleanup-project-audit.json`, `tmp/cleanup-edit-audit.json`).

남은 유지보수 항목으로 `Assets/Settings/Renderer2D.asset`의 이전 URP 필드와 `Lit2DSceneTemplate.scenetemplate`의 해석되지 않는 텍스처 의존성이 있습니다. 전자는 obsolete 필드와 현재 속성에 없는 과거 값이고, 후자는 씬 생성 템플릿의 연결입니다. 현행 MVP 실행 문제로 확인되지 않았으므로 관련 설정을 직접 삭제하거나 재직렬화하지 않았습니다. 이 프로토타입·템플릿을 다시 사용할 때 해당 범위에서 검토합니다.

### 이번 정리의 검증

코드 쓰기를 동결하고 별도 담당자가 연결된 Unity Editor에서 검사했습니다. 정적 비교와 실제 Play 결과는 구분합니다.

| 검사 | 결과·증거 |
| --- | --- |
| 정적 코드 비교 | 24개 파일의 문자열·코드 토큰이 주석/공백 제외 후 기준 HEAD와 동일. `tmp/agent-runs/project-cleanup-20260923/source-token-audit.json` |
| 공백 검사 | 직접 작성 코드·문서 `git diff --check` 통과 |
| Unity 컴파일·셰이더 | 컴파일 완료, 두 셰이더 오류 0. `tmp/cleanup-edit-audit.json` |
| 독립 규칙 | 15/15 통과. `tmp/cleanup-rules.json` |
| 애니메이션 포함 NPC 3명 | 오답 NON PASS → NON PASS → PASS, 34+34+33=101항목 통과 후 Completed. `tmp/cleanup-npc1.json`~`cleanup-npc3.json` |
| 재시작·대사·전달 | 실제 EventSystem 레이캐스트로 재시작 버튼 입력, 첫 NPC 초기화와 대사 숨김·전달 8항목 통과. `tmp/cleanup-restart.json`, `tmp/cleanup-restart-prepare.json` |
| 원본 확대와 닫기 복원 | 문서 30항목, 더미 17항목 통과. `tmp/cleanup-identity-focus.json`, `tmp/cleanup-dummy-focus.json` |
| 최종 Console·저장 상태 | 실제 Console 오류 0·경고 0, compilationFailed=false. Play 종료, 씬 clean·오브젝트 120개·누락 스크립트 0. `tmp/cleanup-console-final.json`, `tmp/cleanup-final-audit.json` |
| 빌드 목록 | 존재하는 현재 MVP 씬 1개 활성, 디스크 저장 확인. `tmp/cleanup-build-scenes.json` 및 `ProjectSettings/EditorBuildSettings.asset` diff |
| 줄바꿈 정리 후 재검증 | 비주석 토큰 24/24 동일, 혼합 줄바꿈 0개, 재컴파일·셰이더 오류 0, 규칙 15/15. `tmp/cleanup-r1-rules.json`, `tmp/cleanup-r1-final-audit.json` |
| 최종 가져오기 경고 확인 | 새 Console 항목 0, 실제 오류·경고 0, compilationFailed=false. `tmp/cleanup-r1-console.json` |

플레이 검사는 `EventSystem.RaycastAll`과 `ExecuteEvents` 기반 자동 입력으로 총 156항목이며 독립 규칙 15개는 별도입니다. Pipeline의 누적 버퍼 집계와 실제 Console 집계를 구분하여 최종 결과는 `groundTruth`로 기록했습니다. 이번에는 실제 OS 마우스 조작이나 두 해상도 전체 시각 검사를 다시 수행하지 않았고, 이전 검증의 미확인 항목을 이번 결과로 대체하지 않습니다. 씬 생성 도구·마이그레이션 재실행, 새 한글 아틀라스 생성, 독립 플레이어 빌드는 미실행입니다.

최초 가져오기 이력을 다시 확인하면서 `FaceDepthSprite.shader`의 혼합 CRLF/LF 경고를 발견했습니다. 같은 문제가 있던 CubeScreen C# 8개와 셰이더 1개의 줄바꿈을 CRLF로 통일하고 기존 BOM 여부는 보존했습니다. 이후 가져오기·컴파일·규칙 검사에서 경고 재발이 없었습니다. 156항목 Play 검사는 이 줄바꿈 정리 전에 실행한 결과이며, 정리 후에는 실행 토큰 불변을 확인하고 컴파일·규칙·에디터 감사를 재실행했습니다.
