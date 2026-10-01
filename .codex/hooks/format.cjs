'use strict';
const c = require('./common.cjs');
const crypto = require('node:crypto');
const hashText = value => crypto.createHash('sha256').update(value).digest('hex');
function responseText(response) {
  if (typeof response === 'string') return response;
  return response && typeof response.output === 'string' ? response.output : '';
}
function changedFiles(event) {
  const text = responseText(event.tool_response);
  if (event.tool_response?.exit_code != null && event.tool_response.exit_code !== 0) throw new Error('Edit tool did not succeed.');
  if (!text.startsWith('Success. Updated the following files:')) throw new Error('No successful explicit apply_patch file evidence.');
  const evidence = [...text.matchAll(/^([AMD]) (.+)$/gm)].map(m => ({ action: m[1], path: m[2].trim() }));
  const patch = c.patchFiles(event.tool_input.command);
  const eligible = evidence.filter(f => f.action !== 'D' && /\.cs$/i.test(f.path));
  for (const f of eligible) if (!patch.some(p => p.path === f.path && p.action !== 'Delete')) throw new Error('Response file is absent from patch.');
  return [...new Set(eligible.map(f => f.path))];
}
function loadLease(event, workspace, now = Date.now()) {
  const file = c.path.join(workspace, '.codex/hooks/format-lease.json');
  if (!c.fs.existsSync(file)) return null;
  c.noLinks(workspace, file);
  const raw = c.readBounded(file); const lease = JSON.parse(raw);
  if (lease.schemaVersion !== 1 || typeof lease.issuedBy !== 'string' || !lease.issuedBy || typeof lease.owner !== 'string' || !lease.owner || lease.writersFrozen !== true || lease.editorIdle !== true) throw new Error('Lease lacks supervisor/owner/frozen writers/idle Editor prerequisites.');
  const expiry = Date.parse(lease.expiresAt); const issued = Date.parse(lease.issuedAt);
  if (!Number.isFinite(expiry) || !Number.isFinite(issued) || issued > now || expiry <= now || expiry - issued > 300000) throw new Error('Expired/invalid lease (maximum five minutes).');
  if (!event.session_id || !event.tool_use_id || lease.sessionId !== event.session_id) throw new Error('Lease does not bind this session and edit invocation.');
  if (lease.agentId !== (event.agent_id ?? null)) throw new Error('Lease agent identity does not match root/subagent event.');
  if (lease.mode !== 'folder' || typeof lease.folder !== 'string') throw new Error('Only explicit folder leases are supported; project entries are rejected.');
  const folder = c.resolve(workspace, lease.folder);
  if (c.excluded(lease.folder)) throw new Error('Generated/vendor project scope excluded.');
  c.noLinks(workspace, folder);
  if (!c.fs.statSync(folder).isDirectory()) throw new Error('Lease folder is not a directory.');
  if (!Array.isArray(lease.includes) || !lease.includes.length || lease.includes.length > 16) throw new Error('Lease/include count invalid (1-16 exact files).');
  const seen = new Set();
  for (const grant of lease.includes) {
    const full = c.resolve(workspace, grant.path); const rel = c.path.relative(workspace, full).replace(/\\/g, '/');
    if (grant.path !== rel || !c.inside(folder, full, false) || c.excluded(rel) || !/\.cs$/i.test(rel) || /(?:\.g|\.generated|\.designer)\.cs$/i.test(rel) || seen.has(rel)) throw new Error('Lease include outside authorized source scope.');
    if (grant.owner !== lease.owner || !(grant.sha256 === null || /^[a-f0-9]{64}$/.test(grant.sha256))) throw new Error('File ownership/hash is missing or stale.');
    c.noLinks(workspace, full); seen.add(rel);
  }
  return { lease, leaseHash: hashText(raw), folder };
}
function receiptPath(event, workspace) {
  return c.path.join(workspace, '.codex/hooks/receipts', hashText(event.session_id + '\0' + event.tool_use_id) + '.json');
}
function sourceFile(workspace, cwd, file, folder) {
  const full = c.resolve(cwd, file); const rel = c.path.relative(workspace, full).replace(/\\/g, '/');
  if (!c.inside(folder, full, false) || c.excluded(rel) || !/\.cs$/i.test(rel) || /(?:\.g|\.generated|\.designer)\.cs$/i.test(rel)) throw new Error('Changed file outside authorized source scope.');
  c.noLinks(workspace, full);
  if (c.fs.existsSync(full) && (!c.fs.statSync(full).isFile() || c.fs.statSync(full).size > 2097152)) throw new Error('Source file missing/oversized.');
  return { full, rel };
}
function prepare(event, workspace) {
  const state = loadLease(event, workspace);
  if (!state) return 'Formatter 준비 생략: 감독 lease 없음; 파일 쓰기 없음.';
  const { lease, folder, leaseHash } = state;
  const files = c.patchFiles(event.tool_input.command).filter(f => f.action !== 'Delete' && /\.cs$/i.test(f.path));
  if (!files.length) return 'Formatter 준비 생략: 명시적 C# 편집 없음.';
  if (/\*\*\* Move to:/.test(event.tool_input.command)) throw new Error('Renames require separate supervisor review; automatic format skipped.');
  const paths = [];
  for (const file of files) {
    const { full, rel } = sourceFile(workspace, event.cwd, file.path, folder);
    const grant = lease.includes.find(g => g.path === rel);
    const before = c.fs.existsSync(full) ? c.digest(full) : null;
    if (!grant || grant.sha256 !== before) throw new Error('File ownership/hash is missing or stale before edit.');
    paths.push({ path: rel, before });
  }
  const receipt = { schemaVersion: 1, sessionId: event.session_id, agentId: event.agent_id ?? null, toolUseId: event.tool_use_id, owner: lease.owner, leaseHash, patchHash: hashText(event.tool_input.command), paths, used: false };
  const file = receiptPath(event, workspace); c.noLinks(workspace, c.path.dirname(file));
  c.fs.mkdirSync(c.path.dirname(file), { recursive: true });
  c.fs.writeFileSync(file, JSON.stringify(receipt), { flag: 'wx' });
  return 'Formatter 준비 완료: 편집 전 해시와 정확한 도구 호출 receipt 기록; 다른 작성자 동결 유지.';
}
function format(event, workspace, options = {}) {
  const state = loadLease(event, workspace, options.now ?? Date.now());
  if (!state) return 'Formatter skipped: no supervisor format lease; no files written.';
  const { lease, folder, leaseHash } = state;
  const files = changedFiles(event);
  if (!files.length) return 'Formatter skipped: no successful changed C# files; no files written.';
  if (files.length > 16) throw new Error('Changed file count invalid.');
  const receiptFile = receiptPath(event, workspace); c.noLinks(workspace, receiptFile);
  if (!c.fs.existsSync(receiptFile)) throw new Error('No PreToolUse receipt for this edit; automatic format skipped.');
  const receipt = JSON.parse(c.readBounded(receiptFile));
  if (receipt.used || receipt.sessionId !== event.session_id || receipt.agentId !== (event.agent_id ?? null) || receipt.toolUseId !== event.tool_use_id || receipt.owner !== lease.owner || receipt.leaseHash !== leaseHash || receipt.patchHash !== hashText(event.tool_input.command)) throw new Error('Receipt is stale, mismatched or already consumed.');
  const exact = []; const snapshots = [];
  for (const file of files) {
    const { full, rel } = sourceFile(workspace, event.cwd, file, folder);
    const grant = lease.includes.find(g => g.path === rel); const prior = receipt.paths.find(p => p.path === rel);
    if (!grant || !prior || grant.sha256 !== prior.before || !c.fs.existsSync(full)) throw new Error('File ownership/hash is missing or stale.');
    exact.push(c.path.relative(folder, full)); snapshots.push({ full, hash: c.digest(full) });
  }
  if (receipt.paths.length !== files.length) throw new Error('Post edit evidence is incomplete; refuse partial formatting.');
  const lock = c.path.join(workspace, '.codex/hooks/format.lock'); c.noLinks(workspace, lock); let fd;
  try { fd = c.fs.openSync(lock, 'wx'); } catch { throw new Error('Formatter lease is already in use (or stale lock); supervisor must review.'); }
  try {
    if (Date.parse(lease.expiresAt) <= Date.now() || c.digest(c.path.join(workspace, '.codex/hooks/format-lease.json')) !== leaseHash || snapshots.some(s => c.digest(s.full) !== s.hash)) throw new Error('Lease expired or source changed before formatter start.');
    // 협력적 lease와 receipt: OS 권한 또는 타 작성자 배제 보장은 아니다.
    receipt.used = true; receipt.afterEdit = snapshots.map(s => ({ path: c.path.relative(workspace, s.full).replace(/\\/g, '/'), sha256: s.hash }));
    c.fs.writeFileSync(receiptFile, JSON.stringify(receipt));
    // SDK 9/10은 --folder와 --no-restore 조합을 거부한다. Folder 모드는 MSBuild/restore를 사용하지 않는다.
    const args = ['format', 'whitespace', folder, '--folder', '--include', ...exact, '--verbosity', 'quiet'];
    const r = (options.runner || c.run)('dotnet', args, folder, 20000);
    if (r.error?.code === 'ENOENT' || /ENOENT/.test(r.error?.message || '')) throw new Error('Formatter failed: SDK 없음으로 skipped/미검증; dotnet 설치/경로를 확인한다. passed로 간주하지 않는다.');
    if (r.error || r.status !== 0) throw new Error('Formatter failed/미검증: ' + c.clip(r.error?.message || r.stderr || r.stdout || ('exit ' + r.status), 1800) + '; SDK와 exact includes를 확인한다. restore/범위 확대는 자동 실행하지 않는다.');
    return 'Formatted whitespace for exact lease-authorized C# files: ' + files.join(', ') + '. Receipt consumed; supervisor must reissue lease before another edit.';
  } finally { if (fd !== undefined) { c.fs.closeSync(fd); c.fs.unlinkSync(lock); } }
}
module.exports = { format, prepare, changedFiles, responseText, loadLease };
