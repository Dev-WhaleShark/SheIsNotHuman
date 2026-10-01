'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const c = require('../common.cjs');
const { spawnSync } = require('node:child_process');
const { prepare, format } = require('../format.cjs');
const repo = c.path.resolve(__dirname, '../../..');
const config = JSON.parse(c.fs.readFileSync(c.path.join(repo, '.codex/hooks.json'), 'utf8'));
function command(event, input, cwd = repo) {
  const hook = config.hooks[event][0].hooks[0];
  return spawnSync(process.platform === 'win32' ? hook.commandWindows : hook.command, { shell: true, cwd, input: typeof input === 'string' ? input : JSON.stringify(input), encoding: 'utf8', timeout: 30000, windowsHide: true });
}
test('exact registered commands execute Session/Pre/Post from subdirectory', () => {
  const cwd = c.path.join(repo, '.codex/hooks');
  const start = command('SessionStart', { hook_event_name: 'SessionStart', cwd, source: 'startup' }, cwd);
  assert.equal(start.status, 0, start.stderr); assert.equal(JSON.parse(start.stdout).hookSpecificOutput.hookEventName, 'SessionStart');
  const base = { cwd, tool_name: 'Bash', tool_input: { command: 'git status --short' } };
  const pre = command('PreToolUse', { ...base, hook_event_name: 'PreToolUse' }, cwd);
  assert.equal(pre.status, 0, pre.stderr); assert.deepEqual(JSON.parse(pre.stdout), {});
  const post = command('PostToolUse', { ...base, hook_event_name: 'PostToolUse', tool_response: { exit_code: 1, output: 'fixture command failure' } }, cwd);
  assert.equal(post.status, 0, post.stderr); assert.equal(JSON.parse(post.stdout).decision, 'block');
});
test('exact registered Pre command preserves denial JSON and malformed exit2', () => {
  const blocked = command('PreToolUse', { hook_event_name: 'PreToolUse', cwd: repo, tool_name: 'Bash', tool_input: { command: 'git reset --hard' } });
  assert.equal(blocked.status, 0); assert.equal(JSON.parse(blocked.stdout).hookSpecificOutput.permissionDecision, 'deny');
  const malformed = command('PreToolUse', '{');
  assert.equal(malformed.status, 2); assert.equal(JSON.parse(malformed.stdout).hookSpecificOutput.permissionDecision, 'deny'); assert.ok(malformed.stderr);
});
test('bootstrap maps missing and crashed handler to exit2 for Pre', () => {
  const parent = c.path.join(repo, 'tmp/agent-runs/agents-hooks-20261001/hooks-fixtures'); c.fs.mkdirSync(parent, { recursive: true });
  const fake = c.fs.mkdtempSync(c.path.join(parent, 'bootstrap-'));
  assert.equal(c.run('git', ['init', '--quiet'], fake).status, 0);
  const input = { hook_event_name: 'PreToolUse', cwd: fake, tool_name: 'Bash', tool_input: { command: 'git status' } };
  assert.equal(command('PreToolUse', input, fake).status, 2);
  c.fs.mkdirSync(c.path.join(fake, 'scripts')); c.fs.writeFileSync(c.path.join(fake, 'scripts/agent-guard.js'), 'invalid !!! syntax');
  const crashed = command('PreToolUse', input, fake); assert.equal(crashed.status, 2); assert.match(crashed.stderr, /훅 핸들러 실패/);
});
test('no Pre receipt, changed tool ID, lease change and replay cannot format', () => {
  const parent = c.path.join(repo, 'tmp/agent-runs/agents-hooks-20261001/hooks-fixtures');
  const workspace = c.fs.mkdtempSync(c.path.join(parent, 'receipt-')); c.fs.mkdirSync(c.path.join(workspace, '.codex/hooks'), { recursive: true }); c.fs.mkdirSync(c.path.join(workspace, 'src'));
  const file = c.path.join(workspace, 'src/Owned.cs'); c.fs.writeFileSync(file, 'class Owned{}');
  const event = { hook_event_name: 'PreToolUse', session_id: 'known-session', tool_use_id: 'future-id-received-in-pre', cwd: workspace, tool_name: 'apply_patch', tool_input: { command: '*** Begin Patch\n*** Update File: src/Owned.cs\n@@\n-class Owned{}\n+class Owned{int x=1;}\n*** End Patch' }, tool_response: 'Success. Updated the following files:\nM src/Owned.cs\n' };
  const lease = { schemaVersion: 1, issuedBy: 'supervisor', owner: 'one-writer', sessionId: event.session_id, agentId: null, writersFrozen: true, editorIdle: true, issuedAt: new Date(Date.now() - 100).toISOString(), expiresAt: new Date(Date.now() + 120000).toISOString(), mode: 'folder', folder: 'src', includes: [{ path: 'src/Owned.cs', owner: 'one-writer', sha256: c.digest(file) }] };
  const leaseFile = c.path.join(workspace, '.codex/hooks/format-lease.json'); const raw = JSON.stringify(lease); c.fs.writeFileSync(leaseFile, raw);
  const runner = () => ({ status: 0 });
  assert.throws(() => format(event, workspace, { runner }), /No PreToolUse receipt/);
  prepare(event, workspace); c.fs.writeFileSync(file, 'class Owned{int x=1;}');
  assert.throws(() => format({ ...event, agent_id: 'different-agent' }, workspace, { runner }), /agent identity/);
  assert.throws(() => format({ ...event, tool_use_id: 'another-edit' }, workspace, { runner }), /receipt/);
  c.fs.writeFileSync(leaseFile, raw + ' '); assert.throws(() => format(event, workspace, { runner }), /stale/); c.fs.writeFileSync(leaseFile, raw);
  assert.match(format(event, workspace, { runner }), /Formatted/);
  assert.throws(() => format(event, workspace, { runner }), /consumed/);
});
test('global CODEX_HOME AGENTS override wins and Session context truncation is explicit', () => {
  const parent = c.path.join(repo, 'tmp/agent-runs/agents-hooks-20261001/hooks-fixtures');
  const home = c.fs.mkdtempSync(c.path.join(parent, 'home-'));
  c.fs.writeFileSync(c.path.join(home, 'AGENTS.md'), 'discarded-global-marker');
  c.fs.writeFileSync(c.path.join(home, 'AGENTS.override.md'), 'selected-global-marker\n' + '가'.repeat(10000));
  const saved = process.env.CODEX_HOME;
  try {
    process.env.CODEX_HOME = home;
    const out = command('SessionStart', { hook_event_name: 'SessionStart', cwd: repo, source: 'resume' });
    assert.equal(out.status, 0, out.stderr); const text = JSON.parse(out.stdout).hookSpecificOutput.additionalContext;
    assert.ok(text.length <= 6000); assert.match(text, /selected-global-marker/); assert.doesNotMatch(text, /discarded-global-marker/);
    const workspace = c.fs.mkdtempSync(c.path.join(parent, 'context-'));
    c.fs.writeFileSync(c.path.join(workspace, 'AGENTS.md'), '상위규칙');
    c.fs.writeFileSync(c.path.join(workspace, 'AGENTS.override.md'), '선택된 프로젝트 override\n' + '나'.repeat(9000));
    const bounded = require('../handler.cjs').handle({ hook_event_name: 'SessionStart', cwd: workspace, source: 'clear' }, { workspace }).hookSpecificOutput.additionalContext;
    assert.ok(bounded.length <= 6000); assert.match(bounded, /문맥 제한으로 잘림/); assert.match(bounded, /선택된 프로젝트 override/); assert.doesNotMatch(bounded, /상위규칙/);
  } finally { if (saved === undefined) delete process.env.CODEX_HOME; else process.env.CODEX_HOME = saved; }
});
