'use strict';
const c = require('./common.cjs');
// 완전한 C# parser가 아니다. 문맥이 불명확한 파일은 advisory만 반환한다.
function scanLine(line, state) {
  let code = ''; let quote = '';
  if (/\$@?"|@\$"|@"|"""/.test(line)) return { ambiguous: true, code: '' };
  for (let i = 0; i < line.length; i++) {
    const ch = line[i]; const next = line[i + 1];
    if (state.block) { if (ch === '*' && next === '/') { state.block = false; i++; } continue; }
    if (quote) { if (ch === '\\') i++; else if (ch === quote) quote = ''; code += ' '; continue; }
    if (ch === '/' && next === '/') break;
    if (ch === '/' && next === '*') { state.block = true; i++; continue; }
    if (ch === '"' || ch === "'") { quote = ch; code += ' '; continue; }
    code += ch;
  }
  return { code, ambiguous: !!quote };
}
function inspectCSharp(command, cwd, workspace) {
  const version = c.path.join(workspace, 'ProjectSettings/ProjectVersion.txt');
  if (!c.fs.existsSync(version) || !/m_EditorVersion:\s*6000\./.test(c.readBounded(version, 500))) return {};
  let file = null; let eligible = false; let ambiguous = false; let advisory = false; let state = { block: false };
  for (const line of command.split(/\r?\n/)) {
    const header = /^\*\*\* (Add|Update|Delete) File: (.+)$/.exec(line);
    if (header) {
      file = c.resolve(cwd, header[2]); const rel = c.path.relative(workspace, file).replace(/\\/g, '/');
      eligible = header[1] !== 'Delete' && /^Assets\/SheIsNotHuman\/Scripts\/.+\.cs$/i.test(rel);
      state = { block: false }; ambiguous = false;
      if (eligible && header[1] === 'Update' && c.fs.existsSync(file)) {
        const text = c.readBounded(file, 65536);
        ambiguous = c.fs.statSync(file).size > 65536 || /\/\*|@"|"""|\$@?"|@\$"/.test(text);
      }
      continue;
    }
    if (!eligible || !(line.startsWith('+') || line.startsWith(' '))) continue;
    if (ambiguous) { advisory = true; continue; }
    const parsed = scanLine(line.slice(1), state);
    if (parsed.ambiguous) { ambiguous = true; advisory = true; continue; }
    if (line.startsWith('+') && /\bnew\s+UnityEngine\s*\.\s*WWW\s*\(/.test(parsed.code)) return { deny: 'Unity 6 first-party 신규 코드의 new UnityEngine.WWW(...)는 obsolete이다. UnityWebRequest를 사용한다.' };
  }
  return advisory ? { advisory: 'C# obsolete 검사 advisory: 블록 주석/복잡 문자열/큰 파일의 문맥이 모호해 단정 차단을 생략했다. 추가 코드의 UnityEngine.WWW 여부를 직접 검토한다.' } : {};
}
module.exports = { inspectCSharp, scanLine };
