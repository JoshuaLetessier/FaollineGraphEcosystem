---
description: "Task list for 020-gameflow-host-bridge (Unity host bridge + Linear scene-flow, vertical slice 1)"
---

# Tasks: gameflow host bridge + Linear scene-flow (vertical slice 1)

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/public-api.md, quickstart.md

**Tests**: REQUIRED (TDD — tests before code). EditMode for all wiring + the full scene-flow logic (against a
stub `ISceneLoader`); PlayMode for the single real `SceneManager` path. Batchmode (no `-quit`; re-run after
source change; verify XML). Branch `020-gameflow-host-bridge`. **graphcore + graphstandard UNTOUCHED.**

**Package**: new `com.faolline.graphgameflow` 0.1.0, depends on `com.faolline.graphcore` 0.6.0 (pinned).

## Phase 1: Setup

- [ ] T001 Create `com.faolline.graphgameflow/package.json` (name `com.faolline.graphgameflow`, version `0.1.0`, displayName, description, unity `6000.0`, dependency `com.faolline.graphcore`: `0.6.0` — pinned, NOT `0.0.0`; author block).
- [ ] T002 [P] Create `com.faolline.graphgameflow/Runtime/com.faolline.graphgameflow.Runtime.asmdef` (name `com.faolline.graphgameflow.Runtime`; references `com.faolline.graphcore.Runtime` by name; auto-referenced; no editor-only platform restriction — needs `UnityEngine.SceneManagement` at runtime).
- [ ] T003 [P] Create `com.faolline.graphgameflow/Tests/EditMode/com.faolline.graphgameflow.Tests.EditMode.asmdef` (references Runtime + `com.faolline.graphcore.Runtime` + `UnityEngine.TestRunner` + `UnityEditor.TestRunner` + `nunit.framework`; `includePlatforms: [Editor]`; `"defineConstraints": ["UNITY_INCLUDE_TESTS"]`).
- [ ] T004 [P] Create `com.faolline.graphgameflow/Tests/PlayMode/com.faolline.graphgameflow.Tests.PlayMode.asmdef` (references Runtime + `com.faolline.graphcore.Runtime` + `UnityEngine.TestRunner` + `nunit.framework`; both Editor + standalone platforms; `"defineConstraints": ["UNITY_INCLUDE_TESTS"]`).
- [ ] T005 [P] Create `com.faolline.graphgameflow/README.md` and `CHANGELOG.md` stubs (title, version 0.1.0, one-line purpose — filled in T018/T019).
- [ ] T006 Compile/resolve check: batchmode import; confirm the three new asmdefs resolve against graphcore with zero errors (no logic yet).

## Phase 2: Foundational (BLOCKS all user stories)

- [ ] T007 [P] Write `GameFlowContextTests` in `com.faolline.graphgameflow/Tests/EditMode/GameFlowContextTests.cs`: `DeepClone` returns a `GameFlowContext` (not a bare `BaseContext`) and carries the `SceneLoader` reference; `CreateCloneInstance` yields the subclass (history-restore correctness, Constitution VI). Confirm RED.
- [ ] T008 Create `com.faolline.graphgameflow/Runtime/Scene/ISceneLoader.cs` (`void LoadScene(string sceneName, LoadSceneMode mode)`), `Runtime/Scene/UnitySceneLoader.cs` (default → `SceneManager.LoadScene`; missing/empty scene logs `[GraphGameFlow]` error, no throw), `Runtime/Context/GameFlowContext.cs` (`ISceneLoader SceneLoader` property; `CreateCloneInstance` → `new GameFlowContext()`; `DeepClone` → base + copy `SceneLoader`), and `Runtime/Context/GameFlowContextKeys.cs` (empty placeholder static class with a comment). XML docs. Confirm T007 GREEN.
- [ ] T009 [P] Create `com.faolline.graphgameflow/Tests/EditMode/StubSceneLoader.cs` — an `ISceneLoader` that records each `(sceneName, mode)` call into a list and loads nothing (deterministic test seam; used by US1/US2/US3).

## Phase 3: US1 — Boot and drive a flow from a scene component (Priority: P1) 🎯 MVP

**Goal**: the `GraphFlowDriver` MonoBehaviour boots the Linear runner, pumps the frame tick, re-exposes
lifecycle events, and supports auto/manual advance — proven in EditMode via callable public methods.

**Independent test**: a graph of statement nodes (+ one time-wait node) assigned to the driver runs to End
under `Boot`/`Tick`/`Advance`, raising events in order; guards handle missing graph/dt≤0/destroy.

- [ ] T010 [P] [US1] Write `GraphFlowDriverTests` in `com.faolline.graphgameflow/Tests/EditMode/GraphFlowDriverTests.cs`: `Boot` enters the start node and raises `OnNodeEntered` (INV-1 happy path); `Boot` with null graph / no start logs `[GraphGameFlow]` warning and `IsRunning==false` (INV-1); auto-advance runs a statement chain to `OnEnded` (SC-002); manual-advance (AutoAdvance off) advances only on `Advance()`; `Tick(dt)` forwards so a `WaitDuration` node resolves, and `Tick(0)`/`Tick(-1)` are ignored (INV-2); `OnNodeEntered/OnNodeCompleted/OnEnded/OnStuck` are re-raised; `RaiseSignal` and `Advance`/`Tick` before `Boot` and after `OnEnded` are no-ops, no throw (INV-3/INV-4); after `OnDestroy` no runner callback fires (INV-6). Build the driver via `new GameObject().AddComponent<GraphFlowDriver>()`, inject a `StubSceneLoader`, call the public methods directly (no PlayMode). Confirm RED.
- [ ] T011 [US1] Create `com.faolline.graphgameflow/Runtime/Driver/GraphFlowDriver.cs`: serialized `BaseGraph _graph` + `bool _autoAdvance`; `ISceneLoader SceneLoader { get; set; }` (default `UnitySceneLoader`); read-only `Context`/`Runner`/`IsRunning`; re-exposed events; `Boot()` (build `GameFlowContext` with `SceneLoader` + empty `NodeExecutorRegistry`, subscribe, `runner.Start`, guard null graph/start with `[GraphGameFlow]` warning, guard double-boot); `Tick(float)` (forward when running, ignore dt≤0); `Advance()`; `RaiseSignal(string)` + `RaiseSignal<T>`; Unity `Start()`→`Boot()`, `Update()`→`Tick(Time.deltaTime)`, `OnDestroy()`→unsubscribe; auto-advance subscribes `OnNodeCompleted`→`Advance`. `[GraphGameFlow]` prefix; XML docs; C# `Action<T>` (no `UnityEvent`). Confirm T010 GREEN.

## Phase 4: US2 — A scene transition is an action attachable to any node (Priority: P1)

**Goal**: `LoadSceneAction` (a graphcore `BaseAction`, not a node type) loads a Unity scene when it runs,
from any node's enter or exit list.

**Independent test**: the action attached to a node's enter list — and equally its exit list, on statement /
choice / subgraph nodes — calls the resolved loader with the configured name + mode.

- [ ] T012 [P] [US2] Write `LoadSceneActionTests` in `com.faolline.graphgameflow/Tests/EditMode/LoadSceneActionTests.cs`: with a `GameFlowContext` carrying a `StubSceneLoader`, `Execute` records the configured `(SceneName, Single)` and `(SceneName, Additive)` (SC-003); the SAME action on a node's enter list vs. exit list, and on statement / choice / subgraph host nodes, records identically (INV-5 / US2 acceptance 1-4); empty `SceneName` logs `[GraphGameFlow]` error and does not throw (US2 acceptance 5); with a non-`GameFlowContext` it falls back to a default loader without throwing. Confirm RED.
- [ ] T013 [US2] Create `com.faolline.graphgameflow/Runtime/Scene/LoadSceneAction.cs` (`: BaseAction`): serialized `string _sceneName` + `LoadSceneMode _mode = Single`; public `SceneName`/`Mode`; `Execute(BaseContext)` resolves `(context as GameFlowContext)?.SceneLoader` else a shared default `UnitySceneLoader`, calls `LoadScene`; empty name → `[GraphGameFlow]` error, no throw. XML docs. Confirm T012 GREEN.

## Phase 5: US3 — Resume an awaiting flow + the full reference scene-flow (Priority: P2)

**Goal**: a flow parks on an await-signal node and resumes when the scene raises the matching signal; the
reference flow walks scene A → (wait) → scene B over one shared context.

**Independent test**: the `start → loadA → await"advance" → loadB → end` graph under the driver with a
`StubSceneLoader` records A on boot, parks, then records B only after `RaiseSignal("advance")`.

- [ ] T014 [P] [US3] Write `SceneFlowReferenceTests` in `com.faolline.graphgameflow/Tests/EditMode/SceneFlowReferenceTests.cs`: build the reference graph (load-A enter-action, an `AwaitSignalName="advance"` node, load-B enter-action) and a driver with a `StubSceneLoader`; on `Boot` the loader recorded `A` and the flow is parked (`OnWaitingForSignal("advance")`, no `B` yet — US3 acceptance 1/4); a non-matching `RaiseSignal("nope")` records nothing more (US3 acceptance 2); the matching `RaiseSignal("advance")` resumes and records `B`, then reaches `OnEnded` (SC-005); `RaiseSignal` when not awaiting / after end is a no-op (US3 acceptance 3). Confirm RED → GREEN (driver+action wiring from US1/US2 should satisfy it; fix only real gaps).

## Phase 6: PlayMode — the genuine SceneManager path

**Goal**: prove the real `UnitySceneLoader` → `SceneManager` load happens under the live Unity pump.

- [ ] T015 [P] Add minimal PlayMode test scene(s) under `com.faolline.graphgameflow/Tests/PlayMode/Scenes/` and a build-settings registration helper (a `[OneTimeSetUp]`/`[OneTimeTearDown]` adding/removing the scene via `EditorBuildSettings.scenes`, guarded by `#if UNITY_EDITOR`).
- [ ] T016 Write `SceneFlowPlayModeTests` in `com.faolline.graphgameflow/Tests/PlayMode/SceneFlowPlayModeTests.cs` (`[UnityTest]`): a `GraphFlowDriver` on a live GameObject running a one-node graph whose enter-action loads the test scene **additively**; after entering Play and yielding a frame (real `Start`/`Update` pump, default `UnitySceneLoader`), assert the scene is loaded (`SceneManager.GetSceneByName(...).isLoaded`) — SC-003 real path. Confirm RED → GREEN.

## Phase 7: Polish & cross-cutting

- [ ] T017 Run the ENTIRE existing 634-test EditMode suite UNCHANGED via batchmode; confirm green (graphcore + graphstandard untouched, INV-7 / SC-007). Record totals (634 + new gameflow EditMode tests).
- [ ] T018 [P] Fill `com.faolline.graphgameflow/README.md` (driver quickstart, the "scene = action, not a node" note, the EditMode-stub / PlayMode-real testing note) and `CHANGELOG.md` (`0.1.0` — host bridge + scene-load action + reference scene-flow).
- [ ] T019 [P] Verify XML `<summary>` docs on every public member (driver, action, seam, context) and the `[GraphGameFlow]` prefix on all misuse logs; validate quickstart.md against the shipped API.
- [ ] T020 Final batchmode green: existing 634 EditMode + new gameflow EditMode all pass; PlayMode suite passes. Verify XML results.

## Dependencies

- **Setup (T001–T006)** → everything.
- **Foundational (T007–T009)** → blocks all user stories (context + seam + stub).
- **US1 (T010→T011)** and **US2 (T012→T013)** are independent of each other (both depend only on Foundational); either order. US1 is the MVP.
- **US3 (T014)** depends on US1 + US2 (driver + scene action).
- **PlayMode (T015→T016)** depends on US1 + US2 (driver + real loader).
- **Polish (T017–T020)** last.

## Parallel opportunities

- T002/T003/T004/T005 (different files) in parallel after T001.
- T007 and T009 in parallel (different test files); T008 implements after T007 is RED.
- T010 (US1 test) and T012 (US2 test) in parallel — different files, both after Foundational.
- T018/T019 in parallel during Polish.

## Implementation strategy

- **MVP = Phase 1 + 2 + US1**: a driver that boots and runs a Linear flow in a scene with re-exposed events.
  Demonstrable on its own (statement flow runs to End).
- **+ US2**: scene transitions as composable actions.
- **+ US3**: the full A→await→B reference scene-flow (the slice's headline proof, EditMode-deterministic).
- **+ PlayMode**: the real `SceneManager` seam proven once under the live pump.
- **+ Polish**: 634 suite still green, docs shipped, XML/prefix verified.

## Notes

- Only the new `com.faolline.graphgameflow/` tree changes. graphcore + graphstandard UNTOUCHED — the driver
  is pure wiring over graphcore 0.6.0's existing `BaseRunner` (await-signal, `Tick`, signals, events).
- The `ISceneLoader` seam is the single abstraction: it keeps all wiring + the full scene-flow logic in fast
  EditMode tests and confines PlayMode to the one real `SceneManager` load.
- Scene change is an **action** (`LoadSceneAction : BaseAction`), never a node type — attachable to any
  node's enter or exit list (locked spec decision US2/FR-007).
