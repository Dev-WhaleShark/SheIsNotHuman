# 프로젝트 구조와 유지보수

메인 진입점은 `Assets/SheIsNotHuman/Scenes/PerspectiveCubeViewPrototype.unity`, 독립 책상 UI 확인용 씬은 `Assets/SheIsNotHuman/Scenes/DeskSandbox.unity`입니다. Unity 버전은 `ProjectSettings/ProjectVersion.txt` 기준 6000.5.4f1입니다. 에이전트 작업 절차는 [AgentWorkflow.md](AgentWorkflow.md)를 따릅니다.

직접 작성 코드는 `Assets/SheIsNotHuman/Scripts` 아래에 있으며 기본 네임스페이스는 `WhaleShark`입니다. UI 약어는 파일·타입·폴더·네임스페이스에서 대문자 `UI`를 사용합니다. 에셋 루트와 저장된 메인 씬의 GUID는 유지합니다.

| 영역 | 책임 |
| --- | --- |
| `Runtime/Gameplay` | `GameFlowManager`가 단계·현재 방문자·판정 확정·처리 수를 소유합니다. `VisitorProfile`은 대사·신분·주문 데이터를 담고, `CustomerCodeRule`은 고객 코드만 정확히 비교합니다. `VisitorActor`는 Front 방문자 연출을 담당합니다. |
| `Runtime/UI/ServiceDesk.cs` | 한 책상의 표시 데이터 바인딩, 로컬 모드와 입력, 물품 전달·회수, 판정/재시작 요청을 연결합니다. 게임 흐름·방문자·큐브를 직접 참조하지 않습니다. |
| `Runtime/UI` | `IdentityCard`는 신분 표시, `MobileDevice`는 앱 등록·이동·입력, `WitchformApp`은 주문 표시, `XFeedApp`은 피드 스크롤을 담당합니다. `DialoguePlayer`가 대사 순서/완료를, `DialoguePanel`이 Text Animator 및 DOTween 표시를 소유합니다. |
| `Runtime/Interaction` | `DeskItem`은 누르기·드래그·전체 경계 제한, `DeskInteractionContext`는 책상별 포인터 점유, `DeskFocus`는 두 문서 원본 또는 소품의 확대와 복원을 담당합니다. |
| `Runtime/Rendering` | `CubeCameraRig`·`CubeNavigationOverlay`는 기존 시점 이동을 유지합니다. 왜곡 좌표와 raycaster는 면별 입력을 제한하고 선택적 `CubeDeskAdapter`가 메인 책상을 큐브에 연결합니다. |
| `Runtime/InputLock.cs`, `TweenScope.cs`, `UI/UILayoutSnapshot.cs` | 겹치는 입력 잠금, 트윈 수명과 취소, 레이아웃·텍스트 복원을 제공합니다. |
| `Editor` | `ServiceDeskMigration`은 명시적 참조 이관, `ServiceDeskBuilder`는 독립 샌드박스, `MobileDeviceMigration`은 기존 휴대전화 참조 연결, `DeviceLayout`은 저장 서식 검사, `CubeNavigationMigration`은 기존 면 입력 연결을 담당합니다. `UIPrefabFactory`는 반복 UI 생성만 돕습니다. |
| `Tests/Editor/CustomerCodeRuleChecks.cs` | Editor 메뉴의 고객 코드 규칙 15개 검사입니다. NUnit 발견 테스트와 구분합니다. |

## 데이터와 독립 UI

아래 상대 에셋 경로의 기준은 `Assets/SheIsNotHuman`입니다. `Data/NPC_01.asset`~`NPC_03.asset`은 `VisitorProfile` 방문자 데이터이며, 카드와 앱에는 에셋 대신 복사한 `IdentityCardData`/`OrderDisplayData`를 전달합니다. `ServiceDesk`는 로컬 `Disabled/Dialogue/Review/Completed` 모드로 표시·입력을 바꾸고 R3 판정/재시작 요청을 발행합니다. 문장 진행과 완료는 `DialoguePlayer`가 관리하며 게임 흐름은 완료를 기다립니다.

`Prefabs/IdentityCard.prefab`, `MobileDevice.prefab`, `DialoguePanel.prefab`은 책상이나 게임 진행 없이 바인딩·지우기·입력/대사를 확인할 수 있는 독립 프리팹입니다. `ServiceDesk.prefab`은 책상을 구성하며 책상 제스처는 씬/ServiceDesk의 인스턴스 래퍼에 연결합니다. 기존 원본 GUID를 유지하고, 별도 Expanded 프리팹은 이미 제거되어 두 문서 원본을 확대합니다.

`DeskSandbox.unity`는 일반 Camera·Canvas·GraphicRaycaster·EventSystem과 샘플 표시 하네스로 물품·포커스·휴대전화 UI를 확인합니다. 방문자 게임 루프·VisitorActor·CubeCameraRig는 포함하지 않으며, `CubeDeskAdapter`는 메인에서만 선택적으로 연결합니다.

## 조작과 검수 흐름

메인 씬은 기존 육면체 화면과 검수 흐름을 유지합니다. 저장된 기본값은 방문자 3명과 애니메이션 활성화이며 판정이 틀려도 다음 방문자로 진행합니다. 고객 코드는 빈 값이 아닌 두 문자열을 `Ordinal`로 정확히 비교하고 공백 제거·대소문자 보정은 하지 않습니다.

대사 타이핑 중 첫 클릭은 현재 문장을 완성하고 다음 클릭이 문장을 진행합니다. 마지막 대사가 완료되면 문서가 전달됩니다. 신분증이나 휴대전화를 클릭하면 두 원본이 함께 카메라 앞으로 이동하며, 닫으면 사용자가 옮겨 놓은 위치·부모·형제 순서·크기·서식이 복원됩니다. 소품 확대에는 판정 버튼이 없습니다. 누른 시간 0.18초와 이동 8픽셀을 모두 만족하면 드래그하고 물품 전체를 책상 경계 안에 제한합니다.

휴대전화는 Home에서 Witchform 주문 정보와 X 피드로 이동하고 Back/Home으로 복귀합니다. 앱은 안정된 `appId`·화면·실행 버튼 목록으로 등록하며 `OpenApp(id)`로 엽니다. 확대 중 입력이 허용될 때만 앱 버튼·스크롤이 동작하고 피드 입력은 책상 제스처로 전달되지 않습니다.

판정은 즉시 한 번만 수락한 뒤 문서 닫기·회수·결과 표시·선택에 따른 반응·퇴장을 기다립니다. 완료 화면의 다시 시작과 비활성화/재활성화는 진행 중 트윈·대사·포인터·잠금을 정리합니다.

## Inspector와 개발 도구

Odin Inspector는 실제 실행 상태를 위쪽에 읽기 전용으로 보여 주고 `설정/연결/개발 도구` 탭을 사용합니다. 연결은 기본 접힘이며 필수 참조 누락을 표시합니다. 개발 버튼은 실제 런타임 경로를 호출하고 진행 단계를 직접 덮어쓰지 않습니다.

`Tools > Service Desk`의 이관·샌드박스 도구는 저장된 기존 배치·폰트·데이터를 사용합니다. 실행 전 활성 씬·dirty·Play 상태를 확인하고 씬·프리팹은 Editor API로 수정합니다. 동일 이관을 반복해도 컴포넌트·포커스 캔버스·구독·프리팹 인스턴스가 중복되지 않도록 검사합니다. 메인 설정을 `animate=false`나 방문자 1명으로 덮어쓰거나 한글 폰트 아틀라스를 재생성하지 않습니다.
