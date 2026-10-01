'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const c = require('../common.cjs');
const { inspectShell } = require('../shell-policy.cjs');
const { handle, failure } = require('../handler.cjs');
const { format, prepare, changedFiles } = require('../format.cjs');
const repo = c.path.resolve(__dirname, '../../..');
const dir = c.path.join(repo, 'tmp/agent-runs/agents-hooks-20261001/hooks-fixtures');
c.fs.mkdirSync(dir, { recursive: true });
function fixture() {
  const workspace = c.fs.mkdtempSync(c.path.join(dir, 'case-'));
  c.fs.mkdirSync(c.path.join(workspace, '.codex/hooks'), { recursive: true });
  c.fs.mkdirSync(c.path.join(workspace, 'src'), { recursive: true });
  const file = c.path.join(workspace, 'src/Owned.cs');
  c.fs.writeFileSync(file, 'class Owned{static int Value( ){return 1+2;}}\n');
  const event = { hook_event_name: 'PostToolUse', cwd: workspace, session_id: 'session-fixture', tool_use_id: 'edit-fixture', tool_name: 'apply_patch', tool_input: { command: '*** Begin Patch\n*** Update File: src/Owned.cs\n@@\n-old\n+new\n*** End Patch' }, tool_response: 'Success. Updated the following files:\nM src/Owned.cs\n' };
  const lease = { schemaVersion: 1, issuedBy: 'supervisor-fixture', owner: 'formatter-fixture', sessionId: event.session_id, agentId: null, writersFrozen: true, editorIdle: true, issuedAt: new Date(Date.now() - 1000).toISOString(), expiresAt: new Date(Date.now() + 120000).toISOString(), mode: 'folder', folder: 'src', includes: [{ path: 'src/Owned.cs', owner: 'formatter-fixture', sha256: c.digest(file) }] };
  const write = () => c.fs.writeFileSync(c.path.join(workspace, '.codex/hooks/format-lease.json'), JSON.stringify(lease));
  return { workspace, file, event, lease, write };
}
const config = JSON.parse(c.fs.readFileSync(c.path.join(repo, '.codex/hooks.json'), 'utf8'));
test('registration uses only official event/handler keys and aliases', () => {
  assert.deepEqual(Object.keys(config.hooks), ['SessionStart', 'PreToolUse', 'PostToolUse']);
  for (const [event, groups] of Object.entries(config.hooks)) for (const group of groups) {
    for (const h of group.hooks) { assert.equal(h.type, 'command'); assert.equal(typeof h.commandWindows, 'string'); assert.equal(h.timeout, 30); assert.match(h.command, /git/); assert.doesNotMatch(h.command, /bypass-hook-trust/); }
    if (event !== 'SessionStart') for (const alias of ['Bash', 'exec_command', 'apply_patch', 'Edit', 'Write']) assert.ok(new RegExp(group.matcher).test(alias));
  }
});
const blocked = [
  'git reset --hard', 'git -C src reset --hard HEAD', 'git clean -fd', 'git clean -fx', 'git push --force origin main', 'git push --force-with-lease', 'git push origin +HEAD:main', 'git push -f',
  'rm -rf .', 'rm -rf ../outside', 'rm -rf /', 'Remove-Item -LiteralPath . -Recurse -Force', 'REMOVE-ITEM -Path .. -RECURSE -FORCE', 'ri -Recurse -Force .', 'rd /s /q .', 'rm -rf $target', 'rm -rf *',
  'Set-Content Library/foo.cs text', 'Add-Content ./Temp/a.cs text', 'Out-File obj/a.cs', 'echo data > Library/a.cs', 'echo data >Library/a.cs', 'git status; git reset --hard', 'pwsh -Command "git reset --hard"',
  'cd ..; rm -rf child', 'Set-Location $dynamic; Remove-Item -Recurse child', 'sc -LiteralPath Library/foo.cs -Value text', 'ac -Path Temp/file -Value text', 'write-output text > obj/out', 'ri -r ..', 'ri -rec .'
];
for (const command of blocked) test('blocked actual shell: ' + command, () => assert.ok(inspectShell(command, repo, repo)));
const allowed = [
  'git status --short', 'git log -5 --oneline', 'git diff --stat', 'git push origin main', 'Get-Content Library/file', 'cat obj/a.cs',
  'echo "git reset --hard"', 'Write-Output "rm -rf ."', 'printf "%s" "git push --force"', '# git reset --hard\ngit status', 'echo "Remove-Item -Recurse -Force ."',
  'git log --grep="git reset --hard"', 'echo ">" "Library/a.cs"', 'Remove-Item -LiteralPath tmp/child -Recurse -Force', 'rm -rf "tmp/child with spaces"',
  'git clean -nd', 'git push --force --dry-run', 'git reset --hard --help', 'Set-Content -Path tmp/safe.txt -Value "Library/file"', 'Copy-Item Library/file tmp/safe.txt', 'Copy-Item Library/cache.txt -Destination tmp/copy.txt', 'cd tmp; rm -rf child'
];
for (const command of allowed) test('allowed read/quoted/scope: ' + command, () => assert.equal(inspectShell(command, repo, repo), null));
for (const command of [
  "Copy-Item -LiteralPath 'Library/cache.txt' -Destination 'tmp/copy.txt'",
  "Copy-Item -Path Library/cache.txt tmp/copy.txt",
  "Copy-Item Library/cache.txt",
  "cpi -LiteralPath Library/cache.txt -Destination tmp/copy.txt"
]) test('생성 파일 원본을 읽어 일반 목적지로 복사 허용: ' + command, () => assert.equal(inspectShell(command, repo, repo), null));
for (const command of [
  'Copy-Item -LiteralPath tmp/copy.txt -Destination Library/cache.txt',
  'Move-Item -LiteralPath Library/cache.txt -Destination tmp/copy.txt'
]) test('생성 목적지 쓰기와 생성 원본 이동 차단: ' + command, () => assert.ok(inspectShell(command, repo, repo)));
for (const file of ['./Library/a.cs', 'Temp/a.cs', 'obj/a.cs', 'Library\\a.cs', './generated.csproj', 'sample.sln']) test('generated patch blocked: ' + file, () => {
  const event = { hook_event_name: 'PreToolUse', cwd: repo, tool_name: 'apply_patch', tool_input: { command: `*** Begin Patch\n*** Update File: ${file}\n@@\n-old\n+new\n*** End Patch` } };
  assert.equal(handle(event, { workspace: repo }).hookSpecificOutput.permissionDecision, 'deny');
});
test('patch reads added/removed lines without obsolete inference', () => {
  for (const action of ['Add', 'Update', 'Delete']) {
    const command = `*** Begin Patch\n*** ${action} File: Assets/Code/Foo.cs\n@@\n-Application.LoadLevel(0);\n+// UnityEngine.WWW in a comment\n+string value = "FindObjectOfType";\n*** End Patch`;
    assert.notEqual(handle({ hook_event_name: 'PreToolUse', cwd: repo, tool_name: 'apply_patch', tool_input: { command } }, { workspace: repo }).hookSpecificOutput?.permissionDecision, 'deny');
  }
});
test('move destination generated path is blocked', () => {
  const command = '*** Begin Patch\n*** Update File: Assets/Foo.cs\n*** Move to: Library/Foo.cs\n@@\n-old\n+new\n*** End Patch';
  assert.equal(handle({ hook_event_name: 'PreToolUse', cwd: repo, tool_name: 'apply_patch', tool_input: { command } }, { workspace: repo }).hookSpecificOutput.permissionDecision, 'deny');
});
test('malformed PreToolUse JSON exits 2 with one denial JSON and stderr', () => {
  const { spawnSync } = require('node:child_process');
  for (const input of ['{', '{}', JSON.stringify({ hook_event_name: 'PreToolUse', cwd: repo, tool_name: 'Bash', tool_input: {} })]) {
    const r = spawnSync(process.execPath, [c.path.join(__dirname, '../handler.cjs'), 'PreToolUse'], { input, encoding: 'utf8', timeout: 5000 });
    assert.equal(r.status, 2); assert.ok(r.stderr); assert.equal(JSON.parse(r.stdout).hookSpecificOutput.permissionDecision, 'deny');
  }
});
test('malformed patch and unterminated quote are diagnostic errors', () => {
  assert.throws(() => c.patchFiles('not a patch'), /Malformed/);
  assert.throws(() => inspectShell('git "status', repo, repo), /quote/);
});
test('SessionStart context bounds, instructions, branch/status/recent log', () => {
  const event = { hook_event_name: 'SessionStart', cwd: repo, source: 'compact' };
  const text = handle(event, { workspace: repo }).hookSpecificOutput.additionalContext;
  assert.ok(text.length <= c.LIMIT); for (const value of ['Workspace:', 'Branch:', 'Existing changes', 'Recent commits', 'AGENTS.md', 'Self-correction']) assert.ok(text.includes(value));
});
test('diagnostic output truncates and uses official PostToolUse block shape', () => {
  const out = failure('PostToolUse', new Error('a'.repeat(30000)));
  assert.equal(out.decision, 'block'); assert.ok(out.reason.length <= 6000); assert.equal(out.hookSpecificOutput.hookEventName, 'PostToolUse'); assert.equal('continue' in out, false);
});
test('shell nonzero output receives bounded correction feedback', () => {
  const out = handle({ hook_event_name: 'PostToolUse', cwd: repo, tool_name: 'Bash', tool_input: { command: 'false' }, tool_response: { exit_code: 1, output: 'error'.repeat(10000) } }, { workspace: repo });
  assert.equal(out.decision, 'block'); assert.ok(out.reason.length < 2300);
});
test('no lease skips with no file writes or runner invocation', () => {
  const f = fixture(); const before = c.digest(f.file); let called = false;
  assert.match(format(f.event, f.workspace, { runner() { called = true; } }), /no supervisor/);
  assert.equal(c.digest(f.file), before); assert.equal(called, false);
});
const invalid = [
  ['unfrozen writers', l => { l.writersFrozen = false; }, /prerequisites/],
  ['active Editor', l => { l.editorIdle = false; }, /prerequisites/],
  ['missing supervisor', l => { l.issuedBy = ''; }, /prerequisites/],
  ['expired', l => { l.expiresAt = new Date(Date.now() - 10000).toISOString(); }, /Expired/],
  ['future issue', l => { l.issuedAt = new Date(Date.now() + 10000).toISOString(); }, /Expired/],
  ['too long', l => { l.expiresAt = new Date(Date.now() + 600000).toISOString(); }, /Expired/],
  ['stale ownership', l => { l.includes[0].owner = 'other'; }, /ownership/],
  ['stale hash', l => { l.includes[0].sha256 = '0'.repeat(64); }, /ownership/],
  ['other session', l => { l.sessionId = 'other'; }, /bind/],
  ['outsider folder', l => { l.folder = '..'; }, /outside/],
  ['generated project entry', l => { l.folder = 'Game.csproj'; }, /excluded/],
  ['project mode', l => { l.mode = 'project'; }, /folder leases/],
  ['empty includes', l => { l.includes = []; }, /count/]
];
for (const [name, mutate, expected] of invalid) test('lease rejects ' + name + ' before writes', () => {
  const f = fixture(); mutate(f.lease); f.write(); const before = c.digest(f.file);
  assert.throws(() => { prepare(f.event, f.workspace); format(f.event, f.workspace, { runner() { assert.fail('runner must not run'); } }); }, expected); assert.equal(c.digest(f.file), before);
});
test('successful authorized edit produces exact include args only', () => {
  const f = fixture(); f.write(); prepare(f.event, f.workspace); let invoked;
  const text = format(f.event, f.workspace, { runner(exe, args, cwd, timeout) { invoked = { exe, args, cwd, timeout }; return { status: 0 }; } });
  assert.match(text, /exact lease-authorized/); assert.equal(invoked.exe, 'dotnet');
  assert.deepEqual(invoked.args, ['format', 'whitespace', c.path.join(f.workspace, 'src'), '--folder', '--include', 'Owned.cs', '--verbosity', 'quiet']);
  assert.ok(!c.fs.existsSync(c.path.join(f.workspace, '.codex/hooks/format.lock')));
});
test('missing formatter and nonzero produce diagnostic failures and release lock', () => {
  for (const result of [{ status: null, error: new Error('spawn dotnet ENOENT') }, { status: 1, stderr: 'formatter failure'.repeat(2000) }]) {
    const f = fixture(); f.write(); prepare(f.event, f.workspace);
    assert.throws(() => format(f.event, f.workspace, { runner: () => result }), /Formatter failed/);
    assert.ok(!c.fs.existsSync(c.path.join(f.workspace, '.codex/hooks/format.lock')));
  }
});
test('lock stops concurrent formatter and is not cleared by another invocation', () => {
  const f = fixture(); f.write(); prepare(f.event, f.workspace); const lock = c.path.join(f.workspace, '.codex/hooks/format.lock'); c.fs.writeFileSync(lock, 'held');
  assert.throws(() => format(f.event, f.workspace, { runner() { assert.fail(); } }), /already in use/); assert.equal(c.fs.readFileSync(lock, 'utf8'), 'held');
});
test('failed edit/no explicit evidence/outsider response cannot format', () => {
  const f = fixture();
  for (const response of ['Failed patch', { exit_code: 1, output: f.event.tool_response }, 'Success. Updated the following files:\nM outsider.cs\n']) {
    assert.throws(() => changedFiles({ ...f.event, tool_response: response }));
  }
});
test('delete evidence and non-C# evidence do not format', () => {
  const f = fixture(); f.write(); f.event.tool_response = 'Success. Updated the following files:\nD src/Owned.cs\nM README.md\n';
  assert.match(format(f.event, f.workspace, { runner() { assert.fail(); } }), /no successful changed/);
});
for (const prefix of ['Assets/Plugins', 'Packages', 'Library', 'Temp', 'obj']) test('vendor/generated changed source excluded: ' + prefix, () => {
  const f = fixture(); f.lease.folder = '.'; const file = prefix + '/Vendor.cs';
  f.event.tool_input.command = `*** Begin Patch\n*** Update File: ${file}\n@@\n-a\n+b\n*** End Patch`; f.event.tool_response = `Success. Updated the following files:\nM ${file}\n`; f.write();
  assert.throws(() => prepare(f.event, f.workspace), /outside authorized/);
});
test('symlink source and recursive deletion containment', t => {
  const f = fixture(); const link = c.path.join(f.workspace, 'link');
  try { c.fs.symlinkSync(repo, link, process.platform === 'win32' ? 'junction' : 'dir'); } catch (e) { t.skip('OS cannot create symlink: ' + e.code); return; }
  assert.throws(() => c.noLinks(f.workspace, c.path.join(link, 'AGENTS.md')), /link/);
  assert.throws(() => inspectShell('Remove-Item -Recurse -LiteralPath link', f.workspace, f.workspace), /link/);
  f.lease.folder = 'link'; f.write(); assert.throws(() => format(f.event, f.workspace), /link/);
});
test('actual .NET 9 SDK folder format changes authorized source only, verify succeeds offline', () => {
  const f = fixture(); const folder = c.path.join(f.workspace, 'src');
  c.fs.writeFileSync(c.path.join(folder, 'global.json'), JSON.stringify({ sdk: { version: '9.0.304', rollForward: 'disable' } }));
  const project = c.path.join(folder, 'Fixture.csproj');
  c.fs.writeFileSync(project, '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup></Project>');
  c.fs.writeFileSync(c.path.join(folder, '.editorconfig'), 'root = true\n[*.cs]\nindent_style = space\nindent_size = 4\ncsharp_space_around_binary_operators = before_and_after\n');
  const outsider = c.path.join(folder, 'Outsider.cs'); c.fs.writeFileSync(outsider, 'class Outsider{int x=1+2;}\n');
  const before = c.digest(f.file); const untouched = c.digest(outsider); const projectHash = c.digest(project); f.write(); prepare(f.event, f.workspace);
  const sdk = c.run('dotnet', ['--version'], folder); assert.equal(sdk.status, 0); assert.equal(sdk.stdout.trim(), '9.0.304');
  assert.match(format(f.event, f.workspace), /Formatted whitespace/); assert.notEqual(c.digest(f.file), before);
  assert.equal(c.digest(outsider), untouched); assert.equal(c.digest(project), projectHash); assert.ok(!c.fs.existsSync(c.path.join(folder, 'obj')));
  const verify = c.run('dotnet', ['format', 'whitespace', folder, '--folder', '--include', 'Owned.cs', '--verify-no-changes', '--verbosity', 'quiet'], folder, 20000);
  assert.equal(verify.status, 0, verify.stderr || verify.error?.message);
  assert.throws(() => format(f.event, f.workspace), /consumed/);
});
