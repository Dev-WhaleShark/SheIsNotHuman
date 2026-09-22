# 검사형 MVP

대상 씬은 `Assets/Scenes/PerspectiveCubeViewPrototype.unity`입니다. 기존 육면체 화면에서 Front는 NPC, Bottom은 대사와 문서 검사에 사용합니다.

## 범위와 규칙

NPC 응대 → 대사 → 신분증·주문서 전달 → 두 문서 원본 동시 확대 → 플레이어 판정 → 반응·퇴장 → 다음 NPC를 구현합니다. 세 번째 NPC 뒤에는 완료 안내와 다시 시작을 제공합니다. 기본 화면에는 판정 버튼을 두지 않습니다.

| NPC | 신분증 고객 코드 | 주문서 고객 코드 | 기대 판정 |
| --- | --- | --- | --- |
| NPC_01 | C-101 | C-101 | PASS |
| NPC_02 | C-202 | C-209 | NON PASS |
| NPC_03 | C-303 | C-303 | PASS |

플레이어는 기대 판정과 관계없이 두 버튼 중 하나를 선택할 수 있습니다. 판정 기록은 `npcId`, `idCustomerCode`, `orderCustomerCode`, `playerDecision`, `expectedDecision`, `isCorrect`를 포함하며 오답으로 진행을 중단하지 않습니다.

## 조작

저장된 `PerspectiveCubeViewPrototype` 씬을 열고 Play를 누르면 시작합니다. 완성 씬의 설정은 `animateTransitions = true`, `npcLimit = 3`입니다. Builder의 새 생성 기본값은 단계별 개발 확인용 `false / 1`이며, 기존 완성 씬이 있으면 생성 도구를 다시 실행할 필요가 없습니다.

1. 대사창을 클릭합니다. 타이핑 중에는 현재 문장만 완성하며, 완성된 문장에서 클릭하면 다음 문장으로 진행합니다.
2. 마지막 문장을 마치고 클릭하면 대사창이 사라진 뒤 문서를 전달합니다.
3. 신분증 또는 주문서를 클릭하면 검사대의 두 문서 원본이 카메라 앞으로 이동합니다. 두 고객 코드를 비교합니다.
4. 닫으면 확대 직전 놓아둔 위치로 복귀합니다. 확대 중 하단의 PASS / NON PASS로 판정할 수도 있습니다.
5. 세 명을 처리한 뒤 다시 시작을 누르면 첫 NPC부터 초기화됩니다.

WASD 또는 방향키로 육면체 시점을 전환합니다. 마우스를 화면 가장자리 기본 96px 안으로 옮기면 이동 가능한 방향 버튼이 나타나며, 버튼을 클릭하면 시점이 전환됩니다. `CubeNavigationOverlay` Inspector의 감지 거리에서 수정할 수 있습니다. 가장자리에 머무르는 것만으로 자동 회전하지 않습니다. 확대, 아이템을 누르거나 끄는 동안, NPC 등장·전달·판정·퇴장 중에는 시점 입력을 잠급니다. 현재 면에 속한 UI만 입력을 받고 회전 중에는 면 UI 입력을 차단합니다.

Bottom의 신분증·주문서와 더미 물품 3개는 같은 입력 컴포넌트를 사용합니다. 클릭하면 별도의 복제물 대신 같은 원본 오브젝트를 카메라 가까이 옮겨 확대합니다. 문서는 두 원본을 나란히, 더미는 선택한 원본 하나를 표시합니다. 누른 채 기본 0.18초 이상 기다리고 8px 이상 움직이면 드래그하며, 놓은 위치에 물품을 둡니다. 물품 전체가 검사대 경계를 벗어나지 않도록 제한합니다. 움직이지 않은 느린 클릭도 확대할 수 있습니다. 확대를 닫으면 직전 드롭 위치와 크기·문서 서식으로 복귀하고, 문서는 NPC 교체 시, 모든 물품은 다시 시작 시 최초 위치로 돌아갑니다.

## 개발 설정

NPC 데이터와 검사 규칙, 진행 컨트롤러, 프레젠테이션을 `Assets/InspectionMvp`에 분리합니다.

- `Runtime/InspectionNpcData.cs`: NPC별 이름, 코드, 주문서, 대사 2줄과 PASS/NON PASS 반응. `Samples/`의 에셋에서 수정합니다.
- `Runtime/InspectionData.cs`: 문서 자료형과 UI에 의존하지 않는 `InspectionRule.ExpectedDecision`.
- `Runtime/InspectionFlowController.cs`: 유일한 진행 상태 소유자. Inspector의 `animateTransitions`, `npcLimit`으로 단계별 시험을 할 수 있습니다.
- `Runtime/InspectionMvpView.cs`: Text Animator 타이핑과 DOTween 연출, 버튼 입력. `motionSeconds`(기본 0.35), `modalSeconds`(0.2), `reactionHoldSeconds`(1.2), `typingSpeed`(1)를 조정합니다.
- `Editor/InspectionMvpBuilder.cs`: `Tools > Inspection MVP > Builder`에서 기존 Perspective 씬에 필요한 오브젝트와 참조를 구성하는 Odin 도구입니다. 기존 MVP가 있으면 다시 생성하지 않습니다.
- `Tests/Editor/InspectionRuleChecks.cs`: `Tools > She Is Not Human > Inspection MVP > Check Customer Code Rule`에서 15개 독립 규칙 검사를 실행합니다. Unity Test Framework 테스트 개수와 별도로 기록합니다.
- `Runtime/IdentityDocumentView.cs`, `OrderDocumentView.cs`: 각 문서의 표시·초기화와 확대 여부를 담당합니다. NPC의 같은 데이터 원본을 작은 문서와 확대 문서에 바인딩합니다.
- `Runtime/DeskInspectableItem.cs`: 문서와 더미의 공용 클릭·드래그 입력입니다. Inspector에서 `holdSeconds`, `movePixels`, `deskBounds`, 더미 이름·색상을 조정합니다.
- `Prefabs/IdentityDocument.prefab`, `OrderDocument.prefab`: 실제 검사대와 확대에 사용하는 같은 문서 원본입니다. 내용은 NPC 데이터, 상세 서식은 각 문서 컴포넌트에서 수정합니다. 이전 `IdentityDocumentExpanded.prefab`, `OrderDocumentExpanded.prefab` 에셋은 보존하지만 현재 확대 표시에는 사용하지 않습니다.
- `Editor/InspectionDeskMigration.cs`: `Tools > Inspection MVP > Migrate desk interactions`에서 기존 MVP를 재생성하지 않고 문서 프리팹 연결·더미·공용 확대창을 구성합니다.
- `Editor/InspectionNavigationMigration.cs`: `Tools > Inspection MVP > Navigation Migration`에서 기존 Perspective 씬에 가장자리 방향 버튼과 왜곡 보정 레이캐스터를 연결합니다.
- `Assets/CubeScreen/Runtime/LensDistortionCoordinates.cs`, `DistortionCorrectedGraphicRaycaster.cs`: 설치된 URP Lens Distortion의 화면 변형을 반영한 클릭·드래그 좌표 변환입니다. 후처리 효과 자체는 변경하지 않습니다.
- `Editor/InspectionDeskMigration.ApplyFocus()`: `Tools > Inspection MVP > Migrate original object focus`에서 현재 씬의 원본 이동 확대와 대사 숨김을 연결합니다. 이전 별도 팝업 표현을 비활성화하고 원본·최소 판정 버튼·차단막을 사용하는 WorldSpace `FocusCanvas`를 구성합니다.
- `InspectionMvpView`의 `dialogueHideSeconds`(기본 0.22), `dialogueHideOffset`(0,18), `focusDepthRatio`(0.7): 대사 사라짐 시간·이동량과 확대 시 카메라 깊이 비율입니다. 반응 대사도 읽기 시간이 지난 뒤 같은 연출로 사라집니다.

대사 표시에는 설치된 Text Animator for Unity와 TMP만 사용합니다. 한글은 설치된 Windows 맑은 고딕으로 SDF 정적 아틀라스를 생성하며 원본 OS 폰트 파일은 프로젝트에 복사하지 않습니다. 새 문구의 글자가 아틀라스에 없다면 `Tools > Inspection MVP > Rebuild Korean Atlas`를 실행하고 씬을 저장합니다. 이 생성 기능에는 해당 OS 폰트가 필요하며 이미 생성한 아틀라스의 플레이에는 원본 폰트가 필요하지 않습니다.

## 최초 MVP 검증 기록

아래는 최초 MVP 완료 시점의 기록입니다. 이후 입력·문서 프리팹·드래그 수정의 최종 검증은 별도 항목으로 구분합니다.

검증은 연결된 Unity 6000.5.4f1 Editor의 실제 Play Mode에서 수행했습니다. 자동 플레이 검사는 `EventSystem.RaycastAll`로 버튼의 실제 hit를 확인한 뒤 `ExecuteEvents`로 입력을 전달합니다. 별도로 Orca computer를 통한 실제 OS 마우스 클릭으로 다시 시작, 대사, 신분증 검사, 오답 NON PASS 선택을 확인했습니다. 아래 자동 입력 검증을 사람이 모든 단계를 수동 플레이한 것으로 표현하지 않습니다. 작업 조율과 원본 증거 인덱스는 `tmp/agent-runs/inspection-mvp-20260922/tasks.md`입니다.

| 분류 | 검증 내용 | 결과 |
| --- | --- | --- |
| 컴파일 | 실제 Unity Editor 컴파일 | 최종 통과, compilationFailed=false; obsolete API 경고 4 수정 완료 |
| Console | 깨끗한 Play 시작 후 추가 3명 전체 루프 | 오류 0 / 경고 0, buffered 및 실제 Console 집계 모두 확인 |
| 단계 1 | 애니메이션 없는 NPC 한 명 전체 사이클 | 통과, 실제 EventSystem raycast 버튼 입력 33항목 확인 |
| 단계 2 | NPC 세 명 반복 진행 | 통과, PASS → 오답 PASS → 오답 NON PASS로 3명 완료 및 실제 다시 시작 버튼 초기화 확인 |
| 단계 3 | DOTween 연출 포함 흐름 | 최종 코드에서 3명씩 전체 루프 2회 통과 |
| 자동 검사 | 고객 코드 일치·불일치 독립 규칙 | 15/15 통과 (`tmp/mvp-rules.json`), Unity Test Framework 발견 테스트는 0개 |
| 실제 플레이 | 대사 스킵·마지막 문장·전달 중 입력 차단 | 통과 |
| 실제 플레이 | 두 문서 진입·현재 데이터·반복 열기/닫기·모달 차단 | 통과 |
| 실제 플레이 | 양쪽 판정·오답·연타·NPC 정리 | 통과, 40개 연속 판정 의도에도 한 번만 확정 |
| 실제 플레이 | 완료·재시작·전체 루프 2회 | 통과, 첫 루프 정답 순서와 두 번째 루프 오답 순서 모두 진행 |
| 화면 | 1920×1080, 1280×720 가독성/잘림 | 통과, 검사창 텍스트 overflow/화면 이탈/누락 글자 0 및 스크린샷 육안 확인 |

### 필수 16개 시나리오

| 번호 | 확인 내용 | 결과 |
| --- | --- | --- |
| 1 | 첫 NPC 한 번만 등장 | 통과 |
| 2 | 타이핑 중 클릭은 현재 문장만 완성 | 통과 |
| 3 | 마지막 대사 완료 후에만 전달 | 통과 |
| 4 | 전달 중 검사·판정 입력 차단 | 통과 |
| 5 | 신분증과 주문서 각각 클릭하여 검사 | 통과 |
| 6 | 현재 NPC의 두 문서 표시 | 통과 |
| 7 | 반복 열기/닫기 후 데이터·상태 유지 | 통과, 각 방문자에서 재열기 반복 |
| 8 | 모달 뒤 대사·문서 클릭 차단 | 통과, 실제 raycast 차단 확인 |
| 9 | PASS/NON PASS 모두 다음 NPC로 진행 | 통과 |
| 10 | 오답이어도 진행 | 통과, 두 번째 전체 루프에서 오답 선택 |
| 11 | 연속 판정 클릭으로 중복 처리 없음 | 통과, 즉시 Resolving 및 40개 중복 의도 검사 |
| 12 | 다음 NPC에게 이전 문서·대사 잔존 없음 | 통과 |
| 13 | 세 명 뒤 완료 안내 | 통과 |
| 14 | 실제 다시 시작 버튼으로 인덱스·UI 초기화 | 통과, OS 마우스 클릭 포함 |
| 15 | 전체 두 루프에서 오브젝트·이벤트 중복 없음 | 통과, 추가 재시작 스트레스 검사에서 객체 95→95 |
| 16 | 두 해상도 문서·버튼 잘림 없음 | 통과, 1280×720 / 1920×1080 검사창 캡처와 layout 검사 |

등장·전달·검사창 닫기 애니메이션 도중 재시작과 지연 콜백 정리를 추가로 확인했습니다(`tmp/mvp-final-restart-stress.json`, 5항목 통과). 판정 연출은 최초 구현의 순서 불일치를 수정하여 **검사창 닫기 → 문서 정리 → 결과 표시와 반응** 순서이며, 최종 두 번째 루프에서 이 순서를 검사했습니다.

### 증거

- 최초 무애니메이션 한 명: `tmp/mvp-stage1-visit.json` (33항목).
- 무애니메이션 세 명: `tmp/mvp-stage2-npc1.json`, `mvp-stage2-npc2-wrong.json`, `mvp-stage2-npc3-wrong.json`, `mvp-stage2-restarted.json`.
- 최종 연출 2회: `tmp/mvp-final-loop1-npc1.json`~`npc3.json`, `mvp-final-loop2-npc1-os.json`, `mvp-final-loop2-npc2.json`, `mvp-final-loop2-npc3.json`.
- 실제 OS 입력: `tmp/mvp-os-*-click.json` 및 이후 상태 결과.
- 해상도: `tmp/mvp-modal-layout1280.json`, `tmp/mvp-modal-layout1920.json`; 캡처 `Assets/Screenshots/InspectionMvp/inspection-1280.png`, `inspection-1920.png`, `desk-1280.png`, `completed-1920.png`.
- 규칙 재검사: `tmp/mvp-final-rules.json` (15/15).
- 최종 추가 클린 루프: `tmp/mvp-clean-npc1.json`, `mvp-clean-npc2.json`, `mvp-clean-npc3.json` (29+25+28항목), `tmp/mvp-clean-console-status.json` (오류·경고 0, compilationFailed=false).
- 최종 저장 상태: `tmp/mvp-final-saved-state.json` (대상 씬, 저장 완료, Play 종료, 애니메이션 활성화, NPC 3명, EventSystem 1개, 정적 폰트 연결).

검증 중 Play 진입 직후 요청 하나는 도메인 리로드와 겹쳐 Unity Pipeline 서버의 `Thread was being aborted` 도구 오류를 남겼습니다. 게임 코드 예외와 구분하여 `tmp/mvp-pipeline-reload-error.txt`에 보존했고, 이후 코드를 바꾸지 않은 깨끗한 Play 시작과 세 명 전체 루프에서 실제 Console 오류·경고가 0임을 재확인했습니다.

### 범위와 미실행 항목

별도 플레이어 빌드·배포는 미실행이며 이번 검증은 Editor Play Mode 기준입니다. Unity Test Framework 테스트 발견 수는 0이므로 그 환경의 테스트 통과로 계산하지 않았습니다. 독립 규칙 검사와 실제 플레이 하네스 결과는 별도 증거로 남깁니다. 전용 배경 아트, 음성, 벌점·게임 오버, 분기 스토리, 실제 상품·인벤토리는 요청 범위에 포함하지 않았습니다.

전체 `git diff --check`는 Unity가 저장한 씬 YAML의 빈 필드 뒤 공백을 보고합니다. 작성한 C#과 문서의 공백 검사는 통과했으며 이 이유로 씬을 직접 정규화하지 않았습니다.

## 이전 시점·문서·물품 입력 수정 검증

이 절은 별도 확대 팝업을 사용했던 이전 수정의 검증 이력입니다. 현재 조작은 위의 동일 원본 이동 확대 설명을 따릅니다.

후속 수정에서는 기존 시점 조작을 복원하고, URP Lens Distortion이 적용된 실제 화면 픽셀을 레이캐스트 및 드래그 좌표와 연결했습니다. 두 문서의 작은/확대 프리팹 4개를 기존 씬의 연결된 인스턴스로 만들고 더미 3개에도 같은 입력 컴포넌트를 적용했습니다. 기존 씬을 재생성하지 않았습니다. 조율 기록은 `tmp/agent-runs/inspection-interactions-20260922/tasks.md`입니다.

왜곡 검사에는 실제 GPU 캡처에서 찾은 버튼 내부 픽셀을 사용했습니다. 원래 투영 위치만 계산한 검사는 화면상 변형을 증명하지 못하므로 사용하지 않았습니다. 경계의 색수차·외곽선·래스터 오차를 구분하기 위해 **4px 경계를 제외한 실제 버튼 내부**와 **보이는 경계보다 8px 바깥의 네 방향**을 검사했습니다. 별도로 활성 Outline/Shadow가 실제로 그리는 범위만큼 `raycastPadding`을 설정했습니다. 1920×1080에서 내부 6,750개, 1280×720에서 2,813개 표본 모두 버튼을 hit했으며 바깥 검사는 모두 miss였습니다(`tmp/revision-final-gpu1920.json`, `revision-final-gpu1280.json`). 화면상 이동된 버튼을 실제 OS 마우스로 눌러 Bottom 전환도 확인했습니다(`revision-final-os-warped-click-state.json`).

현재 좌표 보정은 이 씬의 URP Lens Distortion을 대상으로 합니다. 기존 색수차의 각 채널 경계는 서로 다를 수 있으며, Panini·사용자 정의 전체 화면 워프·XR 조합까지 검증한 것은 아닙니다. 렌즈 효과나 기존 후처리 프로필을 끄지 않았습니다.

개발 중 확인한 두 배치 문제도 수정했습니다. 더미의 최초 하단 위치가 잘리던 문제는 위치·안내문 배치와 `VisibleItemBounds`를 조정했습니다. 마이그레이션 재적용 시 더미가 모달 차단막 위로 올라가던 문제는 마이그레이션 마지막과 확대창을 열 때 차단막을 맨 위로 올리도록 수정했습니다.

| 분류 | 후속 수정 검증 | 결과·증거 |
| --- | --- | --- |
| 컴파일·Console | 최종 통합 코드의 Unity 컴파일 및 실제 Console | 통과, `revision-final-console.json`의 groundTruth: compilationFailed=false, 오류 0 / 경고 0 |
| 시점 | WASD·방향키 8개와 4방향 가장자리 표시·오른쪽 마우스 드래그 | 통과, `revision-final-keyboard.json` 8항목 / `revision-final-hover-drag.json` 5항목 |
| 실제 OS 입력 | 물품 드래그·클릭 확대·가장자리 버튼으로 Front 전환 | 통과, `revision-os-drag.json`, `revision-os-expand.json`, `revision-os-edge.json` |
| 프리팹·재적용 | 마이그레이션 2회 후 독립 문서 4개 연결, 문서 2개·더미 3개 공용 입력 참조 | 통과, `revision-final-prefabs-after-repeat.json` 4항목 |
| 물품 | 5개 물품 드래그·놓기·전체 사각형 경계 제한, 드래그 해제 시 확대 방지 | 통과, `revision-final-r3-drag1.json`, `revision-final-drag-dummy2.json`, `revision-final-drag-dummy3-clamp.json`, `revision-final-drag-identity.json`, `revision-final-drag-order-clamp.json` |
| 확대창 | 더미 3개 확대·닫기, 진행 보존, 시점/판정/배경 물품 차단; 문서창 배경 차단 | 통과, `revision-final-r3-dummy1.json`~`dummy3.json` 각 7항목, `revision-final-document-background.json` 3항목 |
| 재시작 | 더미 및 문서를 누르는 중 다시 시작 후 제스처·입력 잠금 정리 | 통과, `revision-final-r3-restart-held.json`, `revision-final-restart-document-held.json` 각 3항목 |
| 재시작 스트레스 | 등장·전달·패널 전환 중 재시작과 지연 동작 정리 | 통과, `revision-final-restart-stress.json` 5항목, 오브젝트 116→116 |
| MVP 회귀 | 오답 순서 3명과 정답 순서 3명, 전체 2회 완료 | 통과, `revision-final-loop1-npc1.json`~`npc3.json`, `revision-final-loop2-npc1.json`~`npc3.json`, 각 루프 38+38+37항목 |
| 화면 | 1280×720·1920×1080 검사대와 문서 비교창 | 통과, `revision-final-desk-layout720.json`, `revision-final-desk-layout1080.json`, `revision-final-document-layout720.json`, `revision-final-documents-layout1080.json`에 잘림·텍스트 넘침·누락 글자 없음 |
| 규칙 | 고객 코드 독립 판정 재검사 | 15/15 통과, `revision-final-rules.json` |

위 JSON 경로는 모두 프로젝트의 `tmp/` 기준입니다. 최종 캡처는 `Assets/Screenshots/InspectionRevision/final-desk-1280.png`, `final-documents-1280.png`, `final-documents-1920.png`, `final-corrected-1280.png`, `final-corrected-1920.png`에 보존합니다. 최초 단계의 잘림·경계 불일치 캡처도 삭제하지 않았습니다.

1920×1080 작은 문서까지 표시한 검사대도 별도로 확인했습니다(`revision-final-compact-layout1080.json`, 표시 텍스트 12개, 위반·누락 글자 없음; `final-desk-documents-1920.png`).

후속 수정 전 Console에는 Unity 조직 정보를 가져오지 못한 기존 네트워크 오류가 1개 있었습니다(`tmp/revision-console-baseline.json`). 최종 Pipeline 누적 버퍼의 error=1은 이 이력을 포함하며, 실제 최종 Console 집계는 오류·경고 0입니다. 사용자 수정인 조직 설정을 변경하지 않았습니다. 별도 플레이어 빌드와 XR·다른 전체 화면 왜곡 조합은 미실행입니다.

최종 씬은 저장됐고 Play Mode를 종료했습니다(`tmp/revision-final-saved-state.json`). 애니메이션 활성화, NPC 3명, EventSystem 1개, 아이템 5개와 모달 최상위 순서를 확인했습니다. GPU 검사에만 사용한 빨간 버튼 색상은 저장하지 않았으며 원래 회색 버튼과 외곽선 보정 범위가 저장되어 있습니다.

## 동일 원본 확대·대사 숨김·면 입력 수정 검증

이번 수정은 별도 확대 표시를 같은 원본의 실제 이동으로 교체한 최종 상태를 검증합니다. 코드 작성 중에는 자동 가져오기를 잠그고, 소스를 동결한 뒤 별도 담당자가 연결된 Unity Editor에서 컴파일·마이그레이션·Play 검사를 수행했습니다. 기록은 `tmp/agent-runs/inspection-focus-20260922/tasks.md`입니다.

| 확인 내용 | 결과·증거 (`tmp/` 기준) |
| --- | --- |
| 6개 면 중 현재 면만 raycast, 전환 중 차단 | 최종 재검사 74항목 통과, `focus-final-final-gates.json` |
| 이전 면에서 선택한 버튼 및 전환 직후 Submit 차단 | 최종 재검사 3항목 통과, `focus-final-final-submit.json` |
| 기본 96px 감지: 80px에서 표시, 범위 밖 숨김 | 1920×1080에서 9항목 통과, `focus-final-hover.json`; 추가 1280×720 검사는 아래 제한 참조 |
| 입장·반응 대사의 중간 fade 및 완전 숨김, 다음 NPC 복구 | 9항목 통과, `focus-final-r2-dialogue-lifecycle.json` |
| 실제 문서 원본 ID 유지·카메라에 가까워짐·전체 주문 정보·원본 초상화 | 18항목 통과, `focus-final-r2-docs1920.json`. 문서 거리가 약 4.249→2.821로 감소하고 대체 오브젝트 생성 없음 |
| 문서 닫기 후 부모·위치·크기·서식 및 compact 상태 복귀 | 30항목 통과, `focus-final-r2-doc-restore1280.json` |
| 더미 원본 확대·복귀 | 물품 2·3 각각 17항목 통과, `focus-final-r2-dummy2-restore.json`, `focus-final-r2-dummy3-restore.json` |
| 실제 OS로 끌어 놓은 물품의 확대 후 드롭 위치 복귀 | 17항목 통과, `focus-final-os-dropped-dummy-restore.json` |
| 확대·닫힘 도중 재시작 및 루트 비활성화 정리 | 17항목 통과, `focus-final-r2-interruptions.json` |
| 최종 코드에서 오답 3명·정답 3명 전체 2회 진행 | 총 226항목 통과, `focus-final-loop1-npc1.json`~`npc3.json`, `focus-final-loop2-npc1.json`~`npc3.json` |
| 1280×720·1920×1080 원본문서 표시 | 각각 30항목 통과, `focus-final-r2-docs-layout1280.json`, `focus-final-r2-docs-layout1920.json`; 텍스트 넘침·누락 글자·화면 이탈 없음 |
| 독립 고객 코드 규칙 | 15/15 통과, `focus-final-rules.json` |
| 왜곡된 실제 GPU 버튼 영역과 입력 좌표 회귀 | 1920×1080 내부 6,750개·1280×720 내부 2,813개 표본 모두 hit, 누락 0; `focus-final-gpu1920.json`, `focus-final-gpu1280.json` |
| 최종 컴파일·실제 Console | compilationFailed=false, 오류 0·경고 0; `focus-final-console.json`의 groundTruth 기준 |
| 저장된 씬·프리팹 연결 | Play 종료, 씬 저장 완료, NPC 3명·애니메이션 활성·EventSystem 1개·FocusCanvas 1개·물품 5개·compact 프리팹 2개 연결; `focus-final-final-saved-state.json`, `focus-final-asset-links.json` |

실제 화면은 `Assets/Screenshots/InspectionFocus/final-documents-1280.png`, `final-documents-1920.png`에 보존했습니다. 신분증 실루엣은 원본 `IdentityDocument.prefab`의 영구 자식이며 확대할 때만 보입니다.

첫 마이그레이션에서 Unity의 native null과 C# null 차이로 누락된 Canvas를 추가하지 못한 오류를 수정했습니다. 기존 부분 생성 노드를 유지하고 필요한 컴포넌트를 추가하도록 고쳤으며, 최종 두 번 적용에서 오브젝트 120→120, FocusCanvas 1개, compact 문서 프리팹 2개 연결을 확인했습니다. 이전 큰 종이의 초상화가 사라지는 회귀도 원본 ID의 실루엣으로 복구했습니다.

검증 도중 임시 하네스의 obsolete API, 짧은 고정 대기, 비활성 오브젝트 재검색 문제는 하네스에서 수정했습니다. 기대 조건을 낮추거나 제품 오류를 건너뛰지 않았습니다. OS 드래그로 실제 위치를 변경한 뒤 확대·닫기·복귀는 `EventSystem.RaycastAll`과 `ExecuteEvents` 자동 입력으로 검증했습니다. 외부 포인터와 Game View 포커스가 개입해 불안정했던 합성 드래그는 통과로 계산하지 않았습니다. 이번 수정의 실제 OS 클릭을 통한 확대 성공은 확인하지 못했으며, 앞 절의 이전 수정 OS 클릭 결과와 구분합니다. 별도 플레이어 빌드는 미실행입니다.

추가 1280×720 hover 재검사에서는 왼쪽 80px 표시 조건이 실패했습니다(`focus-final-final-hover.json`). 포커스 간섭 가능성이 있으나 원인을 확정하지 못해 이 추가 검사는 **미확인**으로 남깁니다. 1920×1080의 초기 9항목 성공과 구분하며, 이후 hover·navigation 소스는 바뀌지 않았습니다.

GPU 재검사는 앞 절과 같은 4px 내부 표본·8px 바깥 표본 기준입니다. 진단 마커가 기존 비활성 버튼의 ColorTint로 어두워지는 문제는 검사용 색상만 복구하여 검사했으며 제품 코드는 변경하지 않았습니다. 저장된 버튼은 원래 회색이고 FocusCanvas는 비활성 상태입니다. Keyboard·Mouse·Pen 입력 장치를 복구하고 임시 검증 Mouse를 제거했습니다. Pipeline 누적 버퍼의 과거 error=1과 최종 실제 Console 오류 0은 구분합니다.
