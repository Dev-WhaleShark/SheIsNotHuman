'use strict';
const fs = require('node:fs');
const path = require('node:path');
const { spawnSync } = require('node:child_process');
const crypto = require('node:crypto');
const LIMIT = 6000;
const clip = (s, n = LIMIT) => String(s ?? '').slice(0, n);
function run(exe, args, cwd, timeout = 3000) {
  return spawnSync(exe, args, { cwd, encoding: 'utf8', timeout, maxBuffer: 65536, windowsHide: true, shell: false });
}
function root(cwd) {
  const r = run('git', ['rev-parse', '--show-toplevel'], cwd);
  if (r.status !== 0) throw new Error('Cannot resolve git workspace; verify cwd and git installation.');
  return fs.realpathSync(r.stdout.trim());
}
function inside(base, target, equal = true) {
  const rel = path.relative(base, target);
  return (equal || rel !== '') && !path.isAbsolute(rel) && rel !== '..' && !rel.startsWith('..' + path.sep);
}
function resolve(base, value) {
  if (typeof value !== 'string' || !value || /[\0\r\n]/.test(value)) throw new Error('Invalid path.');
  return path.resolve(base, value.replace(/\\/g, path.sep));
}
function generated(value) {
  return /(?:^|\/)(?:Library|Temp|obj)(?:\/|$)/i.test(value.replace(/\\/g, '/')) || /\.(?:sln|slnx|csproj)$/i.test(value);
}
function excluded(value) {
  return generated(value) || /(?:^|\/)(?:Packages|Assets\/Plugins)(?:\/|$)/i.test(value.replace(/\\/g, '/'));
}
function noLinks(base, target) {
  if (!inside(base, target)) throw new Error('Path outside workspace.');
  let p = base;
  for (const part of path.relative(base, target).split(path.sep).filter(Boolean)) {
    p = path.join(p, part);
    if (fs.existsSync(p) && fs.lstatSync(p).isSymbolicLink()) throw new Error('Symlink/reparse link is not authorized.');
  }
  if (fs.existsSync(target) && !inside(fs.realpathSync(base), fs.realpathSync(target))) throw new Error('Resolved path outside workspace.');
}
function readBounded(file, size = 65536) {
  const fd = fs.openSync(file, 'r');
  try { const b = Buffer.alloc(size); return b.subarray(0, fs.readSync(fd, b, 0, size, 0)).toString('utf8'); }
  finally { fs.closeSync(fd); }
}
function digest(file) { return crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex'); }
function feedback(event, message, block = false) {
  const m = clip(message);
  const out = { hookSpecificOutput: { hookEventName: event, additionalContext: m } };
  if (block && event === 'PreToolUse') {
    out.hookSpecificOutput.permissionDecision = 'deny';
    out.hookSpecificOutput.permissionDecisionReason = m;
    delete out.hookSpecificOutput.additionalContext;
  } else if (block) { out.decision = 'block'; out.reason = m; }
  return out;
}
function patchFiles(command) {
  if (typeof command !== 'string' || !command.startsWith('*** Begin Patch\n') || !command.trimEnd().endsWith('*** End Patch')) throw new Error('Malformed apply_patch input.');
  const files = [];
  for (const line of command.split(/\r?\n/)) {
    const m = /^\*\*\* (Add|Update|Delete) File: (.+)$/.exec(line);
    const move = /^\*\*\* Move to: (.+)$/.exec(line);
    if (m) files.push({ action: m[1], path: m[2] });
    if (move) files.push({ action: 'Move', path: move[1] });
  }
  if (!files.length) throw new Error('Patch has no explicit file paths.');
  return files;
}
module.exports = { fs, path, run, root, inside, resolve, generated, excluded, noLinks, readBounded, digest, clip, feedback, patchFiles, LIMIT };
