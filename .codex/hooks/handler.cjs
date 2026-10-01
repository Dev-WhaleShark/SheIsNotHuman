'use strict';
const c = require('./common.cjs');
const { inspectShell } = require('./shell-policy.cjs');
const { inspectCSharp } = require('./csharp-policy.cjs');
const { format, prepare, responseText } = require('./format.cjs');
function context(event, workspace) {
  const instructionText = (file, cap) => c.readBounded(file, cap) + (c.fs.statSync(file).size > cap ? '\n[문맥 제한으로 잘림: 적용 지침 원문 확인 필요.]' : '');
  const branch = c.run('git', ['branch', '--show-current'], event.cwd);
  const status = c.run('git', ['status', '--short', '--untracked-files=normal'], event.cwd);
  const log = c.run('git', ['log', '-5', '--format=%h %s'], event.cwd);
  const parts = [`Workspace: ${workspace}; cwd: ${event.cwd}; source: ${event.source}`, `Branch: ${c.clip(branch.stdout, 200)}`, `Recent commits:\n${c.clip(log.stdout, 500)}`, `Existing changes (preserve):\n${c.clip(status.stdout, 1200)}`];
  const dirs = []; let dir = c.path.resolve(event.cwd);
  while (true) { dirs.unshift(dir); const parent = c.path.dirname(dir); if (dir === workspace || parent === dir || dirs.length >= 32) break; dir = parent; }
  const codexHome = process.env.CODEX_HOME || c.path.join(require('node:os').homedir(), '.codex');
  const globalFile = ['AGENTS.override.md', 'AGENTS.md'].map(f => c.path.join(codexHome, f)).find(f => c.fs.existsSync(f));
  if (globalFile) parts.push(`Global ${globalFile} (bounded excerpt):\n${instructionText(globalFile, 1200)}`);
  for (const d of dirs) {
    const file = ['AGENTS.override.md', 'AGENTS.md'].map(f => c.path.join(d, f)).find(f => c.fs.existsSync(f));
    if (file) parts.push(`Applicable ${file} (하위 지침 우선; 제한 초과 시 원문 확인):\n${instructionText(file, 8192)}`);
  }
  parts.push('Self-correction: 실패를 재현하고 원인을 한정한 뒤 관련 검사를 실행한다. 소유권·다른 작성자 동결·Editor 상태를 보존하고 자동 수정이나 권한 확대를 하지 않는다.');
  let text = parts.join('\n');
  if (text.length > c.LIMIT) text = text.slice(0, c.LIMIT - 140) + '\n[문맥 제한으로 잘림: 편집 전 적용 AGENTS 원문을 모두 확인하고 하위/override 지침을 우선한다.]';
  return c.feedback('SessionStart', text);
}
function handle(event, options = {}) {
  const name = event.hook_event_name;
  if (!['SessionStart', 'PreToolUse', 'PostToolUse'].includes(name) || typeof event.cwd !== 'string' || !c.path.isAbsolute(event.cwd)) throw new Error('Malformed hook event/cwd.');
  const workspace = options.workspace || c.root(event.cwd);
  if (!c.inside(workspace, c.fs.realpathSync(event.cwd))) throw new Error('Session cwd is outside resolved workspace.');
  if (name === 'SessionStart') {
    if (!['startup', 'resume', 'clear', 'compact'].includes(event.source)) throw new Error('Invalid SessionStart source.');
    return context(event, workspace);
  }
  if (!['Bash', 'exec_command', 'apply_patch', 'Edit', 'Write'].includes(event.tool_name)) return {};
  if (typeof event.tool_input?.command !== 'string') throw new Error('Malformed tool_input.command.');
  if (name === 'PreToolUse') {
    let reason;
    if (['Bash', 'exec_command'].includes(event.tool_name)) reason = inspectShell(event.tool_input.command, event.cwd, workspace);
    else for (const f of c.patchFiles(event.tool_input.command)) {
      const full = c.resolve(event.cwd, f.path);
      if (!c.inside(workspace, full) || c.generated(c.path.relative(workspace, full))) { reason = 'Patch touches outside workspace or Unity generated Library/Temp/obj/solution/project path.'; break; }
      c.noLinks(workspace, full);
    }
    if (reason) return c.feedback(name, reason + ' 해당 파괴적 옵션을 제거하거나 작업 공간 내부의 정확한 하위 경로로 수정한다. 이 훅은 사용자 승인 여부를 추정하지 않는다.', true);
    if (!['Bash', 'exec_command'].includes(event.tool_name)) {
      const code = inspectCSharp(event.tool_input.command, event.cwd, workspace);
      if (code.deny) return c.feedback(name, code.deny, true);
      try { return c.feedback(name, (code.advisory ? code.advisory + '\n' : '') + prepare(event, workspace)); }
      catch (error) { return c.feedback(name, 'Formatter 준비 생략: ' + c.clip(error.message, 1600) + '; 편집은 기본 권한 절차를 따른다.'); }
    }
    return {};
  }
  if (['Bash', 'exec_command'].includes(event.tool_name)) {
    const text = responseText(event.tool_response);
    const code = event.tool_response?.exit_code ?? /(?:Process exited with code|Exit code:|exit_code["']?\s*:)\s*(-?\d+)/i.exec(text)?.[1];
    if (code != null && Number(code) !== 0) return c.feedback(name, `Command failed (exit ${code}). Diagnostic: ${c.clip(text, 1800)}\nReproduce narrowly, inspect the cause, and rerun the relevant check; do not broaden permissions or change tests automatically.`, true);
    return {};
  }
  return c.feedback(name, format(event, workspace, options));
}
function failure(name, error) { return c.feedback(name, 'Hook diagnostic/훅 진단: ' + c.clip(error.message, 2200) + ' 이벤트·정책·lease를 확인하고 최소 관련 검사를 재실행한다. 자동 수정·권한 확대는 하지 않는다.', name === 'PreToolUse' || name === 'PostToolUse'); }
async function main() {
  const expected = process.argv[2]; let raw = ''; let overflow = false;
  const timer = setTimeout(() => { process.stdout.write(JSON.stringify(failure(expected, new Error('Input timeout.')))); process.exit(0); }, 3000);
  for await (const chunk of process.stdin) { if (Buffer.byteLength(raw) + chunk.length > 1048576) overflow = true; else raw += chunk; }
  clearTimeout(timer);
  let output;
  try {
    if (overflow) throw new Error('Input exceeds 1 MiB bound.');
    const event = JSON.parse(raw);
    if (!event || event.hook_event_name !== expected) throw new Error('Event does not match registered handler.');
    output = handle(event);
  } catch (error) { output = failure(expected, error); if (expected === 'PreToolUse') { process.exitCode = 2; process.stderr.write(c.clip(output.hookSpecificOutput.permissionDecisionReason, 2200)); } }
  process.stdout.write(JSON.stringify(output));
}
if (require.main === module) main().catch(e => { process.stdout.write(JSON.stringify(failure(process.argv[2], e))); });
module.exports = { handle, failure, context, main };
