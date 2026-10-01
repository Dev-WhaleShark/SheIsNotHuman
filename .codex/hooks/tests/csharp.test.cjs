'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const c = require('../common.cjs');
const { inspectCSharp } = require('../csharp-policy.cjs');
const repo = c.path.resolve(__dirname, '../../..');
const first = 'Assets/SheIsNotHuman/Scripts/HookFixture.cs';
function inspect(lines, file = first, action = 'Add') { return inspectCSharp(`*** Begin Patch\n*** ${action} File: ${file}\n${lines}\n*** End Patch`, repo, repo); }
test('only new explicit UnityEngine.WWW first-party C# is denied', () => assert.match(inspect('+var r = new UnityEngine.WWW(url);').deny, /obsolete/));
for (const lines of ['-var r = new UnityEngine.WWW(url);\n+var r = UnityWebRequest.Get(url);', '+// new UnityEngine.WWW(url);', '+string s = "new UnityEngine.WWW(url)";', '+/* new UnityEngine.WWW(url); */', '+/*\n+new UnityEngine.WWW(url);\n+*/', '+var r = new WWW(url);']) test('obsolete removed/comment/string/unqualified allowed: ' + lines, () => assert.equal(inspect(lines).deny, undefined));
for (const file of ['README.md', 'Assets/Plugins/HookFixture.cs', 'Packages/HookFixture.cs']) test('non-first-party/non-C# excluded: ' + file, () => assert.equal(inspect('+var r = new UnityEngine.WWW(url);', file).deny, undefined));
test('complex string advisory avoids false positive denial', () => { const out = inspect('+var s = $"text {value}";\n+new UnityEngine.WWW(url);'); assert.equal(out.deny, undefined); assert.match(out.advisory, /모호/); });
test('unknown existing block comment context is advisory', () => {
  const parent = c.path.join(repo, 'tmp/agent-runs/agents-hooks-20261001/hooks-fixtures');
  const workspace = c.fs.mkdtempSync(c.path.join(parent, 'csharp-')); const file = c.path.join(workspace, first);
  c.fs.mkdirSync(c.path.dirname(file), { recursive: true }); c.fs.writeFileSync(file, '/* prior block comment\n*/');
  c.fs.mkdirSync(c.path.join(workspace, 'ProjectSettings')); c.fs.writeFileSync(c.path.join(workspace, 'ProjectSettings/ProjectVersion.txt'), 'm_EditorVersion: 6000.5.4f1');
  const out = inspectCSharp(`*** Begin Patch\n*** Update File: ${first}\n@@\n+new UnityEngine.WWW(url);\n*** End Patch`, workspace, workspace);
  assert.equal(out.deny, undefined); assert.match(out.advisory, /모호/);
});
