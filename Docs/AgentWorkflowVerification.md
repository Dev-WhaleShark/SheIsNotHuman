# 에이전트 운영 설정 검증 기록

검증일: 2026-09-17. 범위: 작업 운영 설정과 문서. 게임 코드, 씬, 패키지 설정은 이번 작업에서 변경하지 않았다.

## 적용

- `AGENTS.md`와 `Docs/AgentWorkflow.md`: 감독 배정, 병렬 작업, 파일/Editor 소유권, 통합 검증, 자동 수정과 재검증, 최종 보고 규칙.
- `.codex/config.toml`: 서브 에이전트 활성화 및 동시 상한 3. 실제 런타임 제한 우선.
- `.codex/agents/{supervisor,implementer,verifier}.toml`: 역할 지침. 모델과 권한은 상속.
- Orca 관리 세션에서는 연결 확인 후 Orca 우선, 일반 세션에서는 native Codex 사용. 두 방식의 작업 상태는 혼용하지 않는다.

## 확인된 결과

| 항목 | 결과 | 근거 |
| --- | --- | --- |
| Native 감독의 작업 분할과 병렬 위임 | 통과 | 감독이 Unity 조사와 Codex 설정 조사를 자식 두 명에게 배정하고 양쪽 결과 회수 |
| 독립 문서/역할 리뷰 | 통과 | 감독이 6개 설정·운영 파일 검토, 재귀 감독 생성/파일 소유권/검증 누락 관련 차단 결함 없음 |
| Codex 설정 로드 | 통과 | CLI 0.154.0, `codex.cmd --strict-config doctor --json`, `config.load.status=ok`; 승인된 일반 사용자 실행 |
| TOML 구문 검사 | 통과 | Orca 독립 검증자가 Python 3.13.7 `tomllib`으로 프로젝트 설정과 역할 3개, 총 4개 파일 파싱 성공 |
| 사용자 정의 역할 자동 발견 | 미검증 | doctor가 역할별 발견 목록을 제공하지 않음; 새 세션에서 확인 필요, 역할 지침 직접 전달 경로도 문서화 |
| Orca 런타임 연결 | 통과 | `orca status --json`: 1.4.205, ready, reachable, connected |
| Orca 실제 작업 배정/실행 시작 | 통과 | worker-start의 Task/Dispatch 생성, input_accepted 및 turn_started 확인 |
| Orca worker_done 및 ack | 통과 | 해당 Task/Dispatch의 `outcome=succeeded` 수신 및 `delivery_7a94fc5318e7` ack 확인 |
| Orca 작업자 정리 결정 | 완료 | 런타임이 `user_takeover` 사유로 `user_owned/retained` 표시; 터미널 보존. reclaimable 작업자 0개 확인. release 자체는 실행하지 않음 |
| Unity MCP | 미연결 | doctor에서 http://127.0.0.1:8080/mcp 연결 실패 |
| 게임 자동 테스트 | 실행 안 함 | 설정/문서만 변경. 프로젝트 자체 테스트 .cs/.asmdef 발견 못함; 패키지 테스트 csproj는 게임 테스트로 계산하지 않음 |

## Orca 검증 실행 식별자

- Run: `run_ad44de49d850`
- Task: `task_2cde1b8a56b3`
- Dispatch: `ctx_63c3d40f9951`
- Worker terminal: `term_54a503b8-ef5e-477b-8a2b-c00e10cddf8d`
- 작업: 여섯 설정·운영 파일의 읽기 전용 독립 검증. 게임 수정 및 추가 에이전트 생성 금지.

승인 대기 중에는 작업자가 live로 관측되어 실패 또는 종료로 추정하지 않았다. 사용자 승인 후 같은 Dispatch에서 `worker_done` 메시지 `msg_c01a6a22081f`를 2026-09-17 11:47:57 UTC에 보냈고, 감독 측에서 Task/Dispatch 일치와 `outcome=succeeded`를 검토한 뒤 delivery ack를 완료했다. 작업자는 런타임상 `retainedReason=user_takeover`인 사용자 소유 터미널이므로 종료하지 않았다. `worker-list --run run_ad44de49d850 --terminal-state reclaimable --json`은 작업자 0개를 반환했다.

독립 검증자는 여섯 설정·운영 파일에서 중대한 지시 충돌을 발견하지 않았으며, TOML 4개 구문 검사와 `tmp/agent-runs/` Git 제외 확인을 통과했다. 파일 수정이나 Unity 실행은 하지 않았다. 새 세션의 사용자 정의 역할 자동 발견과 각 설정 키의 실제 적용은 여전히 별도 런타임 검증 대상이며, 이번 구문 검사 및 전체 config 로드 성공과 구분한다.

## 보존한 기존 변경

작업 시작 시 수정 상태였던 `Assets/CubeScreen/Profiles/PerspectiveCameraLensProfile.asset`, `Assets/NuGet.config.meta`, `Packages/manifest.json`, `Packages/packages-lock.json` 및 기존 미추적 `.agents/`, `.claude/`, `Assets/Plugins/Roslyn/`, `skills-lock.json`은 보존했다. 전체 diff 검사에서 보인 `Assets/NuGet.config.meta`의 후행 공백은 기존 변경이므로 수정하지 않았다.

## 실행 환경에서 해결한 문제

샌드박스에서 Git 소유권 검사, Codex runtime home 접근, Orca 명령 검색이 제한됐다. Git은 해당 저장소 한정 `-c safe.directory=...`로 조회했고, Codex 진단과 Orca CLI는 정식 승인 후 같은 실행 파일로 실행했다. 전역 권한 정책이나 사용자 모델 설정을 변경하지 않았다.
