# 감독 기반 에이전트 운영

메인은 사용자와 소통하고, 감독은 작업 배정과 수정 루프를 관리한다. Orca 관리 세션에서는 런타임과 컨텍스트를 확인한 뒤 Orca orchestration을 우선 사용한다. Orca가 없는 일반 Codex 세션에서는 기본 서브 에이전트를 사용한다. 시작할 때 선택한 방식을 밝히며 상시 백그라운드 실행 서비스는 설치하지 않는다.

## 적용 파일

- `AGENTS.md`: 프로젝트의 지속적인 작업 규칙과 병렬 위임 요청.
- `.codex/config.toml`: 메인을 제외한 동시 서브 에이전트 상한 3개. 실제 실행 환경의 상한이 우선한다.
- `.codex/agents/supervisor.toml`: 감독 역할.
- `.codex/agents/implementer.toml`: 구현/수정 역할.
- `.codex/agents/implementer_lite.toml`: 범위가 좁고 완료 조건이 분명한 간단한 구현 역할.
- `.codex/agents/verifier.toml`: 독립 검증 역할.

사용자가 위임한 난이도별 모델 선택은 일반 skill 지침에서 모델명을 명시한 경우에만 재정의하라는 조건보다 우선한다. 일반적인 모델/추론 수준 선택에는 재승인이 필요 없다. `.codex/config.toml`은 기본 서브 에이전트를 `gpt-6-luna` / `high`로 지정한다. 역할별 기본은 다음과 같다.

| 작업 | 역할 또는 모델 / 추론 수준 |
| --- | --- |
| 탐색, 문서, 간단 구현 | `implementer_lite`: `gpt-6-luna` / `high` |
| 일반 구현 | `gpt-6-sol` / `medium` |
| 감독, 복잡한 버그, 독립 검증 | `gpt-6-sol` / `high` |
| 최상위 추론이 필요한 예외 | 근거를 기록하고 `gpt-6-astra` / `high` |

런타임이 요청 모델을 지원하지 않으면 확인된 사용 가능 모델 중 가장 가까운 낮은 등급으로 대체하고 요청값, 실제값, 이유를 기록한다.

역할 파일에 모델이 고정된 경우 해당 역할의 모델과 추론 수준이 생성 요청의 모델 플래그보다 우선한다. 작업에 맞는 역할 파일을 고르고, 다른 값이 필요하면 일반 worker에 역할 지침과 요청값을 직접 전달한다. native Codex에서 모델/추론 값을 명시할 때는 `fork_turns="none"` 또는 제한된 이력 범위와 독립적으로 끝낼 수 있는 작업을 사용하고 `fork_turns="all"`은 사용하지 않는다. 예: 복잡한 조사 worker를 `gpt-6-sol` / `high`, `fork_turns="none"`으로 시작하고 파일 소유권과 완료 조건을 프롬프트에 담는다.

Orca `worker-start`에서 `--effort`를 쓰려면 `--model`도 지정해야 하며, 두 플래그는 `--terminal` 재사용과 함께 쓸 수 없다. 모델이 다른 새 worker는 재사용하지 말고 새로 시작한다. 시작 뒤 요청/실제 모델과 추론 수준을 확인하고, 다르면 실제값과 사유를 기록한다. 설정 파일은 새 프로젝트 세션에서 로드되는지 확인한다. 기존 세션이나 역할 선택 기능이 없는 환경에서는 메인이 역할 파일을 읽고 생성 프롬프트에 내용을 전달한다. 파일을 만들었다는 사실만으로 실행 중인 세션의 도구나 한도가 바뀌지는 않는다.

## 운영 순서

1. 메인이 요청 범위, 기존 변경, 완료 조건을 확인하고 감독을 생성한다.
2. 감독이 작업 계약과 의존 관계를 작성하고 독립 작업을 최대 두 작업자에게 배정한다. Orca 모드에서는 감독이 Run/Task/Dispatch를 관리하고, 터미널 권한상 필요하면 메인이 감독의 CLI 호출을 중계한다. 메인은 검증 준비 등 별도의 유용한 작업을 수행한다. Orca 작업자는 주입된 worker preamble에 따라 완료 보고하며 감독을 재귀 생성하지 않는다.
3. 구현자는 담당 파일만 수정하고 개별 검증 결과를 감독에게 보고한다. 공유 인터페이스 변경은 먼저 합의한다.
4. 감독이 모든 결과를 통합하고 쓰기 작업을 멈춘다. 공유 디렉터리에서는 변경이 이미 함께 보이므로 불필요한 병합을 하지 않는다. 별도 작업 트리를 쓰는 경우에만 명시적으로 통합한다.
5. 독립 검증자가 실제 통합본과 완료 조건을 확인한다. 런타임이 새 검증자 생성을 허용하지 않으면 메인이 검증한다. 자신이 구현한 부분은 다른 담당자가 검증한다.
6. 실패는 재현 -> 원인 분석 -> 담당자 수정 -> 관련 재검증 순서로 처리한다. 수정 후에는 이전 테스트 결과를 그대로 재사용하지 않는다.
7. 감독이 검증 근거를 취합하고 메인이 한국어로 결과를 보고한다. 외부 환경 때문에 필수 검증이 불가능하면 미검증 상태와 구체적인 원인을 보고한다.

`implementer_lite`는 좁고 분명한 소유 파일과 완료 조건이 있는 간단한 작업에만 사용한다. 범위가 넓어지거나 설계 판단이 추가로 필요하면 작업자는 변경을 멈추고 감독에게 에스컬레이션한다.

작업 상태: `planned -> running -> implemented -> verifying -> verified`.
검증 실패 시 `verifying -> repairing -> implemented -> verifying`로 돌아간다.
`blocked`는 실패 원인, 이미 시도한 대안, 필요한 외부 조치와 함께 기록한다.

## 이 프로젝트의 권장 작업 경계

독립 구현은 메인·감독·작업자 2명으로 시작한다. Editor 작업과 최종 독립 검증 단계에서는 작업자 슬롯을 재배치하되, Editor 담당자는 한 번에 한 명만 둔다. 아래 경계는 현재 파일을 기준으로 한 배정 예시이며 실제 변경 파일의 작성자는 작업 계약에서 확정한다.

| 작업 경계 | 현재 파일과 조율 조건 |
| --- | --- |
| 검사 데이터·흐름·규칙 | `Assets/InspectionMvp/Runtime/InspectionData.cs`, `InspectionNpcData.cs`, `InspectionFlowController.cs`의 데이터와 판정 계약. 메뉴 규칙 검사는 `Assets/InspectionMvp/Tests/Editor/InspectionRuleChecks.cs`에 있으며 NUnit 발견 수와 별도로 센다. |
| 화면·입력 결합 | `InspectionMvpView.cs`, `DeskInspectableItem.cs`와 `Assets/CubeScreen/Runtime/PerspectiveCubeViewController.cs`, `DistortionCorrectedGraphicRaycaster.cs`, `LensDistortionCoordinates.cs`는 같은 상호작용을 이룬다. 병렬 수정 전 좌표 변환, 현재 면, 확대 중 입력 잠금, 취소 API 계약을 합의한다. |
| 씬 구성·마이그레이션 | `Assets/InspectionMvp/Editor/InspectionMvpBuilder.cs`, `InspectionDeskMigration.cs`, `InspectionNavigationMigration.cs`, `InspectionDocumentLayout.cs`의 코드 소유권과 실제 씬·프리팹·에셋 및 `.meta` 출력 소유권을 각각 지정한다. 실행은 단일 Editor 담당자가 맡는다. |
| 최종 검증 | 모든 관련 소스 작성자의 쓰기를 동결한 최신 통합 상태에서 독립 검증자가 컴파일, 관련 검사, 실제 씬 동작을 확인한다. |

## 작업 기록

감독은 각 실행에 고유한 이름을 부여하고 `tmp/agent-runs/<run-id>/tasks.md`에 기록한다. `tmp/`는 Git 제외 대상이며 자동 삭제하지 않는다. 하나의 감독만 기록을 수정한다. Orca 모드에서는 Orca 작업 상태가 기준이며 이 파일은 결과 내보내기 용도로만 사용한다.

```markdown
# Run: <run-id>
요청 / 범위:
백엔드: native-codex 또는 orca (실제 연결 확인 필요)
기존 사용자 변경:
최종 완료 조건:
Unity Editor 담당자: 없음 또는 <agent>

| ID | 목표 | 담당자 | 모델 / 추론 | 선택 이유 | 소유 파일 | 선행 작업 | 완료 조건 | 검증 방법 | 상태 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| T1 | ... | ... | gpt-6-luna / high | 좁고 명확한 수정 | ... | ... | ... | ... | planned |

## 검증 증거
- 검사 / 실행 명령 또는 도구 동작:
- 검사한 revision 또는 변경 파일 해시:
- 결과: passed / failed / blocked / not-run
- 발견·실행·통과·실패·건너뛴 테스트 수:
- 메뉴 규칙 검사 결과 / 발견·실행한 NUnit 테스트 수 (별도 기록):
- 로그 / 결과 파일:

## 수정 기록
- 실패 서명 / 재현 / 원인 / 수정 / 재검증:

## 인계
- 현재 작업과 파일 소유자:
- Unity 프로젝트 / Editor 인스턴스 / 대상 씬 / dirty·저장 / Play / 컴파일 / 출력 에셋·.meta / 다음 Editor 담당자:
- 완료 항목 / 남은 항목 / 차단 사유:
```

## Unity 검증 계약

현재 프로젝트 버전은 `ProjectSettings/ProjectVersion.txt`가 기준이다. 설정 당시 Unity 6000.5.4f1이며 `Packages/manifest.json`에 Test Framework 1.7.0이 있다. 패키지 설치와 프로젝트 테스트 존재 여부는 별개다.

Unity 작업은 소스 작성과 Editor 실행을 구분해 배정한다. 감독은 C# 및 관련 `.meta`의 작성자를 파일별로 한 명씩 지정하고, 씬·프리팹 편집, AssetDatabase 작업, 에셋 가져오기, 컴파일, Play Mode, Unity 테스트·빌드는 한 번에 한 명의 Editor 담당자에게 맡긴다. Editor 담당 권한은 다른 작업자의 파일 소유권을 넘겨받는 뜻이 아니다. Builder·Migration·Editor 스크립트가 간접 생성하거나 저장하는 씬·프리팹·에셋과 각각의 `.meta`도 실행 전에 출력 파일 소유자와 범위를 작업 계약에 적는다.

Editor 가져오기·컴파일·테스트를 시작하기 전에 모든 작성자의 관련 소스 쓰기를 멈추고 감독이 최신 파일 상태를 기록한다. 가져오기·컴파일·테스트가 진행되는 동안 관련 C#을 수정하지 않는다. 수정이 필요하면 Editor 검증을 중단하고 파일 소유자에게 돌려보낸 뒤 다시 쓰기를 동결하고 영향을 받은 검사를 재실행한다. Editor 인계에는 활성 프로젝트와 인스턴스, 대상 씬, 씬·프리팹의 dirty/저장 상태, Play Mode 상태, 컴파일 상태, 생성·변경 출력물과 `.meta`, 다음 담당자를 기록한다.

- 문서/에이전트 설정 변경: 설정 파싱, 파일 참조, 역할 간 충돌, 위임/결과 회수 확인. 게임 동작 변경이 없으면 Unity 실행을 불필요하게 요구하지 않는다.
- C# 런타임/Editor 변경: 실제 Unity 컴파일 결과와 관련 테스트 확인. 생성된 csproj의 빌드 결과만으로 Unity 검증을 대체하지 않는다.
- 시각/입력/씬 변경: 관련 씬의 실제 동작과 화면, Console 오류/경고를 확인한다. CubeScreen 관련 작업은 현행 `Docs/ProjectStructure.md`, `Docs/InspectionMvp.md`와 실제 대상 씬 파일을 대조한다. 현재 MVP 씬은 `Assets/Scenes/PerspectiveCubeViewPrototype.unity`이지만 작업 대상은 요청과 현재 파일에서 확인한다.
- `InspectionRuleChecks`의 메뉴 실행 규칙 검사 수와 Unity Test Framework에서 발견·실행한 NUnit 테스트 수를 별도 항목으로 기록한다. 테스트가 0개 발견되면 0개 통과를 성공 근거로 삼지 않는다. 변경 위험에 맞는 재현 시나리오 또는 필요한 회귀 테스트를 준비한다.
- Editor가 열려 있으면 적용되는 Unity 스킬을 통해 연결된 올바른 프로젝트를 확인한다. 같은 프로젝트로 두 번째 Editor를 실행하지 않는다.
- 최종 검증은 코드 쓰기가 멈춘 상태에서 수행하고, 검증 후 관련 파일이 바뀌면 영향받는 검사를 다시 한다.

## Orca 연결 전환

2026-09-17 샌드박스 PowerShell의 `orca skills get orchestration`은 `CommandNotFoundException`으로 실패했으나, 승인된 일반 사용자 환경에서 같은 명령이 성공했다. `orca status --json`으로 Orca 1.4.205의 `ready`, `reachable: true`, `connectionState: connected`를 확인했다. Windows 샌드박스의 명령 검색/접근 제한과 앱 설치 여부를 구분해야 한다. 샌드박스 밖 실행은 기존 승인 정책을 따른다.

Orca에서 이 프로젝트를 연 세션에서 설치된 `orchestration` 스킬의 실행 파일 선택 규칙을 따른다. 선택한 CLI로 버전에 맞는 `skills get orchestration` 가이드를 읽고, 가이드에 따른 실제 런타임/작업 컨텍스트를 확인한 뒤 Run을 만들고 독립 작업을 배정한다. 완료 이벤트를 처리하고 검증한 뒤 각 작업자를 재사용하거나 release하고 전달 메시지를 ack한다. 현재 가이드는 `run-create`, `worker-start`, `check`, `worker-release`를 제공하지만 실행 시 설치 버전의 지침을 다시 확인한다. 지원 명령을 추측하거나 다른 바이너리로 임의 전환하지 않는다.

전환 시 진행 중인 native 작업을 정리하고 파일 소유권과 완료 결과를 대조한 뒤 Orca를 단일 상태 관리 기준으로 사용한다. Orca의 작업자가 Codex인 경우에도 native 서브 에이전트 수와 Orca 작업자 수는 별개로 관리하며 중복 배정하지 않는다.

## 완료 보고 양식

변경 결과 / 실행한 검증과 결과 / 수정한 문제 / 남은 제약을 보고한다. 검증하지 않은 기능을 완료로 표현하지 않는다. 이 설정은 활성 세션 내 운영 지침이며, 앱 종료 후 자동 재개나 상시 작업 서비스의 설치를 의미하지 않는다.

공식 설정 참고: https://learn.chatgpt.com/docs/agent-configuration/subagents
