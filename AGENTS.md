# Project agent workflow

## Default execution

For implementation and bug-fix requests, use supervised parallel agents when independent work exists. This is the user's standing request for delegation. Answer explanation-only requests without starting implementation.

- The main agent owns user communication, scope, and the final report. Create one `supervisor` subagent to own decomposition, dispatch, dependencies, integration, and repair decisions.
- Read `Docs/AgentWorkflow.md` and the relevant role instructions in `.codex/agents/`. If custom role selection is unavailable, pass the corresponding instructions explicitly when spawning a native agent.
- The supervisor may spawn implementers and a verifier. Implementers and verifiers must not spawn additional supervisors or workers. Inherited project instructions do not override an agent's assigned role.
- Respect the actual runtime limit. Start with main + supervisor + two workers (four active agents total). Reuse workers for follow-up work. Run verification after implementation; if a new verifier cannot be created, the main agent provides independent verification while the supervisor coordinates repairs. Never wait for capacity while keeping unnecessary workers active.
- The main agent must do useful independent work while delegation runs, such as requirements analysis, preparing validation, or inspecting integration boundaries. Route worker changes through the supervisor.
- For a small indivisible change, use one implementer and an independent review rather than inventing parallel tasks.

## Ownership and completion

- Before editing, inspect the worktree and record existing user changes. Preserve them. No automatic reset, cleanup, commit, push, or deployment.
- Each task needs an ID, objective, file ownership, dependencies, acceptance criteria, and validation plan. Assign disjoint files where possible; shared interfaces must be agreed before parallel implementation.
- Shared-directory agents see each other's changes immediately. Only one writer per file. Only the supervisor assigns or transfers ownership. Freeze implementation during integrated verification.
- Worker completion means `implemented`, not `verified`. Integrate all required changes, then run relevant checks on the final combined state and review against acceptance criteria.
- Failures follow reproduce -> diagnose -> scoped fix -> rerun failing checks and affected regression checks. Continue authorized repairs without asking for routine confirmation. After three attempts with the same failure signature, reassess assumptions and change approach; escalate only a concrete blocker that requires user input or external action.
- Do not weaken tests, skip failures, or claim unexecuted checks passed. Record pre-existing failures separately. Report missing test coverage and unavailable tooling as unverified, not success.
- Final report in Korean: delivered changes, tests actually run and their results, repaired issues, remaining blockers or unverified behavior.

## Unity coordination

- Use the version in `ProjectSettings/ProjectVersion.txt`. Read `Docs/CODEX_HANDOFF.md` and `Docs/UnifiedFaceViewDesign.md` for relevant CubeScreen work; verify historical claims and the target scene against current files and the user's request.
- The supervisor assigns one Unity Editor owner at a time. Serialize scene/prefab edits, asset imports, package changes, Play Mode transitions, and test/build runs against the same Editor/project. Pause relevant code writes during compilation or tests.
- Before Editor actions, use the applicable Unity skill and confirm the active project/instance. Never start a second Editor on the same project or discard unsaved scenes.
- Keep Unity asset `.meta` files paired and preserve GUIDs. Do not edit generated `Library/`, `Temp/`, `obj/`, or generated solution/project files.
- For runtime changes, check compilation and relevant EditMode/PlayMode behavior. For visual/input changes, also inspect the actual scene and capture relevant evidence. Zero discovered tests is a coverage gap.
- `Assets/Scenes/TestScene.unity` and its `.meta` are outside CubeScreen work unless the user explicitly includes them.

## Coordination backend

- In an Orca-managed session, prefer Orca orchestration after loading the installed orchestration skill and its version-matched CLI guide and confirming runtime/context. The supervisor owns the Run and dispatches; the main agent relays CLI calls only if needed for terminal authority. Native-only sessions use Codex subagents. Announce the chosen backend before dispatching.
- Orca CLI access may require approved execution outside the Windows sandbox. A sandbox command-not-found result is not proof Orca is uninstalled. Retry the same selected executable with proper approval when environment evidence supports that diagnosis; never switch to an unverified binary.
- Orca Task/Dispatch state is the sole live task-state authority in Orca mode. A native supervisor can coordinate Orca workers, but workers must be real Orca Dispatches. A dispatched implementer/verifier must follow its live worker preamble and must not spawn a supervisor because it inherited these project rules.
- This setup is not a background service and does not imply work continues after the session is stopped. Never present native agents as Orca workers.
- Do not silently switch an active Orca run to native coordination. Reconcile workers, ownership, and completed changes before any backend transition.
