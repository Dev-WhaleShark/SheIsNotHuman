'use strict';
const { path, resolve, inside, generated, noLinks } = require('./common.cjs');
// 제한된 어휘 분석이며 셸을 실행하지 않는다. 따옴표 안의 내용은 인자로 유지한다.
function lex(source) {
  const segments = []; let words = []; let word = ''; let quote = ''; let started = false; let quoted = false;
  const flush = () => { if (started) { words.quoted ??= []; words.quoted.push(quoted); words.push(word); } word = ''; started = false; quoted = false; };
  const segment = () => { flush(); if (words.length) segments.push(words); words = []; };
  for (let i = 0; i < source.length; i++) {
    const c = source[i];
    if (quote) {
      if (c === quote) { if (source[i + 1] === quote && quote === "'") { word += c; i++; } else quote = ''; }
      else if (c === '`' && quote === '"') { word += source[++i] || ''; }
      else word += c;
    } else if (c === '"' || c === "'") { quote = c; started = true; quoted = true; }
    else if (c === '#' && !started) { while (i < source.length && source[i] !== '\n') i++; segment(); }
    else if (';|&\n'.includes(c)) { segment(); }
    else if (/\s/.test(c)) flush();
    else if (c === '`') { word += source[++i] || ''; started = true; }
    else { word += c; started = true; }
  }
  if (quote) throw new Error('Unterminated shell quote; provide a directly reviewable command.');
  segment(); return segments;
}
function executable(value) { return path.win32.basename(value).toLowerCase().replace(/\.(exe|cmd|bat)$/, ''); }
function inspectShell(command, cwd, workspace, depth = 0) {
  if (depth > 3) return 'Nested shell wrappers exceed policy inspection bound.';
  let current = cwd; let dynamicCwd = false; const locations = [];
  for (const words of lex(command)) {
    let offset = words[0] === '&' ? 1 : 0;
    let exe = executable(words[offset]); const args = words.slice(offset + 1); const lower = args.map(s => s.toLowerCase());
    if (exe === 'ri') exe = 'remove-item';
    exe = ({ sc: 'set-content', ac: 'add-content', ni: 'new-item', cpi: 'copy-item', mi: 'move-item', copy: 'copy-item', move: 'move-item' })[exe] || exe;
    if (['cd', 'chdir', 'set-location', 'push-location', 'pushd'].includes(exe)) {
      const target = args.filter(a => !a.startsWith('-'))[0];
      if (['push-location', 'pushd'].includes(exe)) locations.push(current);
      if (!target || /[$*?{}]/.test(target)) dynamicCwd = true;
      else { current = resolve(current, target); dynamicCwd = false; }
    }
    if (['pop-location', 'popd'].includes(exe)) { if (locations.length) current = locations.pop(); else dynamicCwd = true; }
    if (['powershell', 'pwsh', 'bash', 'sh', 'cmd'].includes(exe)) {
      const index = lower.findIndex(s => ['-command', '-c', '/c'].includes(s));
      if (index >= 0 && args[index + 1]) { const reason = inspectShell(args.slice(index + 1).join(' '), current, workspace, depth + 1); if (reason) return reason; }
    }
    if (exe === 'git') {
      // -C dir 같은 단순 전역 옵션을 건너뛴 뒤 Git 하위 명령을 찾는다.
      let i = 0;
      while (i < args.length && args[i].startsWith('-')) { i += ['-c', '-C', '--git-dir', '--work-tree'].includes(args[i]) ? 2 : 1; }
      const sub = lower[i]; const flags = lower.slice(i + 1);
      if (flags.includes('--help') || flags.includes('-h') || flags.includes('--dry-run') || (sub === 'clean' && flags.some(s => /^-[^-]*n/.test(s)))) continue;
      if (sub === 'push' && flags.some(s => /^--force(?:=|$)|^--force-with-lease(?:=|$)|^--mirror$/.test(s) || /^-[^-]*f/.test(s) || s.startsWith('+'))) return 'Force/mirror push blocked by repository policy.';
      if (sub === 'reset' && flags.includes('--hard')) return 'git reset --hard blocked by repository policy.';
      if (sub === 'clean' && flags.some(s => /^-[^-]*[fdx]/.test(s) || ['--force', '--directory', '--ignored'].includes(s))) return 'Destructive git clean blocked by repository policy.';
    }
    if (['rm', 'rmdir', 'rd', 'remove-item', 'del', 'erase'].includes(exe)) {
      const recursive = lower.some(s => /^-[^-]*r/.test(s) || ['--recursive', '-recurse', '/s'].includes(s));
      if (recursive) {
        if (dynamicCwd) return 'Recursive deletion after dynamic cwd is not statically verifiable.';
        const targets = [];
        for (let i = 0; i < args.length; i++) {
          const a = args[i];
          if (['-literalpath', '-path'].includes(lower[i])) { if (args[i + 1]) targets.push(args[++i]); }
          else if (!a.startsWith('-') && !/^\/[a-z]$/i.test(a)) targets.push(a);
        }
        if (!targets.length) return 'Recursive deletion has no statically verifiable target.';
        for (const target of targets) {
          if (/[$*?{}]/.test(target)) return 'Recursive deletion uses a dynamic/wildcard target; require literal in-workspace child paths.';
          const full = resolve(current, target);
          if (!inside(workspace, full, false)) return 'Recursive deletion at/outside workspace root blocked by repository policy.';
          noLinks(workspace, full);
        }
      }
    }
    // 명시적 쓰기 대상만 검사하며 읽기·출력 문자열·주석은 경로로 추정하지 않는다.
    if (['set-content', 'add-content', 'out-file', 'new-item', 'copy-item', 'move-item', 'cp', 'mv', 'touch', 'tee'].includes(exe)) {
      const explicit = []; const positional = []; const namedDestination = [];
      for (let i = 0; i < args.length; i++) {
        if (lower[i] === '-destination') { if (args[i + 1]) namedDestination.push(args[++i]); }
        else if (['-path', '-literalpath', '-filepath'].includes(lower[i])) { if (args[i + 1]) explicit.push(args[++i]); }
        else if (['-value', '-inputobject', '-encoding', '-itemtype', '-name', '-filter'].includes(lower[i])) i++;
        else if (!args[i].startsWith('-')) positional.push(args[i]);
      }
      let destinations;
      if (['copy-item', 'cp'].includes(exe)) {
        // Copy의 -Path/-LiteralPath는 읽기 원본이다. 목적지 생략 시 현재 폴더를 사용한다.
        destinations = namedDestination.length ? namedDestination : positional.length > (explicit.length ? 0 : 1) ? positional.slice(-1) : ['.'];
      } else if (['move-item', 'mv'].includes(exe)) {
        // Move는 원본도 삭제하므로 원본과 목적지의 생성 경로를 함께 보호한다.
        destinations = [...explicit, ...positional, ...namedDestination];
      } else {
        destinations = explicit.length || namedDestination.length ? [...explicit, ...namedDestination] : ['touch', 'tee'].includes(exe) ? positional : positional.slice(0, 1);
      }
      if (destinations.some(p => generated(path.relative(workspace, resolve(current, p))))) return 'Direct mutation of Unity generated path blocked.';
    }
    for (let i = offset + 1; i < words.length; i++) if (!words.quoted?.[i] && /^>{1,2}/.test(words[i])) {
      const target = words[i].replace(/^>{1,2}/, '') || words[i + 1] || '';
      if (generated(path.relative(workspace, resolve(current, target)))) return 'Redirect into Unity generated path blocked.';
    }
  }
  return null;
}
module.exports = { lex, inspectShell };
