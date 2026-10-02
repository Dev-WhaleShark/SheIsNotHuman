# 프로젝트 Codex hooks

등록은 [hooks.json](../hooks.json), 진입점은 [agent-guard.js](../../scripts/agent-guard.js), 구현은 [handler.cjs](handler.cjs)다.
Node 내장 모듈과 Git을 사용하고, 선택적 공백 포맷에는 .NET SDK가 필요하다. 명령은 Git 루트를 찾아 실행하므로 하위 디렉터리에서도 동작한다.

## 실행과 신뢰

| 이벤트 | 실행 조건 | 처리 |
| --- | --- | --- |
| SessionStart | startup / resume / clear / compact | 작업 위치·브랜치·기존 변경·최근 커밋 5개·적용 AGENTS 전달 |
| PreToolUse | Bash / exec_command / apply_patch / Edit / Write | 명령·편집 경로 검사, 조건 충족 시 포맷 준비 기록 |
| PostToolUse | 같은 도구의 실행 후 | 실패 진단, 허가된 C# 파일의 공백 포맷 |

신뢰된 프로젝트 config 옆의 hooks.json을 발견한다. 등록·명령 변경 후 `/hooks`에서 현재 정의의 신뢰 상태를 확인한다. 신뢰 저장소를 직접 수정하거나 자동 승인하지 않는다.
기존 Orca 전역 훅 8개는 수명·메시지 전달을 담당하며 프로젝트 훅 3개와 함께 사용한다.
SessionStart는 CODEX_HOME의 전역 지침과 저장소 루트부터 cwd까지의 AGENTS를 읽는다. 같은 위치의 AGENTS.override.md가 우선하며 하위 지침을 우선한다. 발췌가 잘리면 원문 확인을 안내한다.

## 실행 전 검사

- 실제 force/mirror push, hard reset, 파괴적인 git clean을 차단한다. help·dry-run은 허용한다.
- 식별 가능한 재귀 삭제의 대상이 작업 공간 루트·외부·동적/와일드카드 경로이거나 링크를 통과하면 차단한다.
- apply_patch의 Add/Update/Delete/Move 경로를 검사하고 작업 공간 밖, Library/Temp/obj 및 sln/slnx/csproj 편집을 차단한다.
- 셸의 명시적 쓰기·리다이렉션에서 생성 파일 경로를 검사한다. Copy는 목적지, Move는 원본과 목적지를 검사한다.
- Unity 6의 Assets/SheIsNotHuman/Scripts/**/*.cs에 추가되는 명시적 `new UnityEngine.WWW(...)`를 검사한다. 주석·문자열·삭제 줄은 제외하고 복잡한 문맥은 검토 안내로 처리한다.

[shell-policy.cjs](shell-policy.cjs)와 [csharp-policy.cjs](csharp-policy.cjs)는 제한된 구문 검사다. 임의 스크립트 내부 쓰기·eval·복잡한 치환을 모두 분석하지 못한다.
Edit/Write도 등록되어 있으나 편집 핸들러는 tool_input.command의 apply_patch 형식을 요구하므로 다른 입력 형식은 호환성 확인이 필요하다.
[policy.example.json](policy.example.json)은 설명용 목록이며 런타임 정책을 덮어쓰지 않는다. 예외가 필요하면 구체적인 범위·권한과 훅 소스 변경을 검토한다.

## 선택적 C# 공백 포맷

실제 format-lease.json이 없으면 파일을 쓰지 않고 포맷을 생략한다. 감독은 [format-lease.example.json](format-lease.example.json)을 참고해 다음 절차를 따른다.

1. 다른 작성자와 Editor 컴파일·테스트를 동결한다. 단일 owner, sessionId, agentId, 정확한 파일 경로와 편집 전 SHA-256을 지정한다. 신규 파일의 해시는 null이다.
2. lease 기간은 최대 5분, includes는 1~16개, 파일당 최대 2MiB다. mode는 folder이며 writersFrozen:true, editorIdle:true가 필수다.
3. PreToolUse가 실제 tool_use_id·patch·lease·원본 해시를 묶은 receipt를 기록한다. 준비 불일치는 포맷을 생략하며 편집은 기본 권한 절차를 따른다.
4. PostToolUse가 성공한 apply_patch의 명시적 A/M 파일 목록과 receipt를 대조하고, 단일 lock을 얻어 lease·파일 안정성을 다시 확인한 뒤 포맷한다.
5. receipt는 사용 완료 처리한다. 새 편집에는 새 원본 해시에 맞춘 lease가 필요하며 만료·재사용·소유권 변경·누락 기록은 포맷할 수 없다. rename은 수동 검토한다.

```text
dotnet format whitespace <folder> --folder --include <exact files> --verbosity quiet
```

공백만 정리한다. Assets/Plugins, Packages, 생성 폴더·프로젝트, g/generated/designer C# 및 링크 경로는 제외한다. SDK 9.0.304 검증에서 거부된 `--folder --no-restore` 조합은 사용하지 않는다.
lease·receipts·format.lock은 gitignore 대상이다. 비정상 종료로 남은 lock·기록은 감독이 상태를 확인해 정리한다.
lease는 협업 계약이며 OS 권한이나 다른 프로세스의 쓰기 차단을 보장하지 않는다. SDK 누락·포맷 실패는 미검증으로 보고하고, 실패 전에 생긴 공백 변경은 자동 원복하지 않는다.

## 진단과 제한

정상 Pre 정책 거부는 permissionDecision:deny와 사유를 반환한다. 잘못된 입력과 bootstrap의 Git/핸들러 실패는 Pre에서 exit 2로 처리한다.
Post의 명령 실패·포맷 오류는 제한된 진단과 관련 검사 방향을 전달한다. 이미 수행된 작업을 되돌리거나 코드를 자동 수정하거나 권한을 확대하지 않는다.
Node 누락·비활성/비신뢰 훅·런타임 오류·timeout에서는 실행 차단을 보장하지 못할 수 있으므로 기존 권한·소유권 절차를 함께 지킨다.

| 항목 | 제한 |
| --- | --- |
| 입력 크기·대기 | 1MiB / 3초 |
| Git / 포매터 / bootstrap / 등록 timeout | 3 / 20 / 25 / 30초 |
| 전역 / 프로젝트 AGENTS 발췌 | 1,200바이트 / 파일당 8,192바이트 |
| 핸들러 문맥 / 등록 additionalContextLimit | 6,000자 / 1,800 |

## 검사와 참고

저장소 루트에서 실행한다.

```text
node --test .codex/hooks/tests/*.test.cjs
```

검사는 경로·셸 인용·거부 응답·문맥 제한·lease/receipt/lock·등록 명령과 실제 SDK 격리 fixture를 포함한다. 제한된 Windows 환경에서 SDK의 EditorConfig 조회가 거부되면 원인을 기록하고 승인 절차로 같은 검사를 재실행한다.
훅 신뢰·실제 실행 확인과 Unity/게임 동작 검증은 별도로 기록한다.

- [OpenAI Hooks](https://learn.chatgpt.com/docs/hooks): 등록·이벤트·신뢰·응답 규격.
- [Microsoft dotnet format](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format): whitespace·folder 옵션.
- [Unity WWW 소스](https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/master/Modules/UnityWebRequestWWW/Public/WWW.cs): obsolete 규칙 근거.
