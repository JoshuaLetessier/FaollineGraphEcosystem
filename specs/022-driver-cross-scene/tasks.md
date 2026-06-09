---
description: "Task list for 022-driver-cross-scene (gameflow driver cross-scene hardening, gameflow 0.3.0)"
---

# Tasks: gameflow driver cross-scene hardening (slice 3)

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/public-api.md, quickstart.md

**Tests**: REQUIRED (TDD — tests before code). EditMode (stub loader) for the boot/event/query additions;
**PlayMode with the REAL `SceneManager`** for the cross-scene survival regression (the keystone). Batchmode
(no `-quit`; re-run after source change; verify XML). Branch `022-driver-cross-scene` (stacks on master, where
gameflow 0.2.0 lives). **graphcore + graphstandard UNTOUCHED; slice-1/2 driver API append-only.**

> All runtime change is additive to the single file `Runtime/Driver/GraphFlowDriver.cs`. US3/US2 (small
> EditMode-testable members) land before the US1 keystone because the US1 PlayMode test boots the driver
> explicitly (it relies on `bootOnStart=false` from US3).

## Phase 1: US3 — Boot control + waiting-state query (Priority: P2)

**Goal**: disable auto-boot for explicit/configured boot; read the parked await state without runner internals.

**Independent test**: with `BootOnStart=false`, `Start` doesn't boot and an explicit `Boot` doesn't warn;
while parked on an await node, `IsWaitingForSignal` is true and `CurrentAwaitSignal` is the name.

- [ ] T001 [P] [US3] Extend `com.faolline.graphgameflow/Tests/EditMode/GraphFlowDriverTests.cs`: `BootOnStart=false` ⇒ activating the driver does not boot (no `OnNodeEntered`, `IsRunning` false) and a later explicit `Boot()` logs no "already running" warning (use `LogAssert`); while parked on an await-signal node `IsWaitingForSignal` is true and `CurrentAwaitSignal` equals the awaited name; before `Boot`, after `OnEnded`, and on a non-await node, `IsWaitingForSignal` is false and `CurrentAwaitSignal` is `""`. (Drive via the public methods + `StubSceneLoader`, as the existing tests do.) Confirm RED.
- [ ] T002 [US3] In `com.faolline.graphgameflow/Runtime/Driver/GraphFlowDriver.cs` add (additive): `[SerializeField] bool _bootOnStart = true` + `BootOnStart` property; gate the Unity `Start()` hook on it (`if (_bootOnStart) Boot();`); add read-only `IsWaitingForSignal` (running && `Runner.State == WaitingForSignal`) and `CurrentAwaitSignal` (the awaited name while waiting, else `""`). XML docs; `[GraphGameFlow]` unchanged. Confirm T001 GREEN.

## Phase 2: US2 — Re-expose OnWaitingForTime (Priority: P2)

**Goal**: scene code can react to timed nodes via the driver's public events (symmetric with signal waits).

**Independent test**: subscribing to the driver's `OnWaitingForTime` fires (node + duration) when the flow
enters a `WaitDuration` node.

- [ ] T003 [P] [US2] Extend `com.faolline.graphgameflow/Tests/EditMode/GraphFlowDriverTests.cs`: a graph with a `WaitDuration` node; subscribing to `driver.OnWaitingForTime` receives the event for that node with its duration when the node is entered. Confirm RED.
- [ ] T004 [US2] In `GraphFlowDriver.cs` add `public event Action<BaseNodeData,float> OnWaitingForTime`; wire `_runner.OnWaitingForTime += HandleWaitingForTime` / `-=` in the existing `Subscribe`/`Unsubscribe`; `HandleWaitingForTime(node,secs) => OnWaitingForTime?.Invoke(node,secs)`. XML docs. Confirm T003 GREEN.

## Phase 3: US1 — A single driver runs a flow across scene loads (Priority: P1) 🎯 the keystone

**Goal**: a persistent driver survives single-mode scene loads and runs a graph that spans scenes; scene
scripts reach it via `GraphFlowDriver.Active`. The missing REAL cross-scene test is added here.

**Independent test**: a real `start → loadA(Single) → await "advance" → loadB(Single) → end` run, via the real
`SceneManager`, reaches the end with the persistent driver still alive.

- [ ] T005 [P] [US1] Generate two minimal empty committed scenes at edit time under `com.faolline.graphgameflow/Tests/PlayMode/Scenes/`: `GameFlowCrossSceneA.unity` and `GameFlowCrossSceneB.unity` (via a one-shot `-executeMethod` helper using `EditorSceneManager.NewScene(EmptyScene)` + `SaveScene`, or hand-authored minimal scenes). Commit them + their `.meta`.
- [ ] T006 [US1] Write `com.faolline.graphgameflow/Tests/PlayMode/CrossSceneSurvivalTests.cs` (`[UnityTest]`): `[OneTimeSetUp]`/`[OneTimeTearDown]` (`#if UNITY_EDITOR`) register/remove the two scenes in `EditorBuildSettings.scenes`; build `start → loadA(Single) → await "advance" → loadB(Single) → end` with the **real** `UnitySceneLoader` (no stub); create the driver via the inactive-GameObject pattern with `PersistAcrossScenes=true`, `BootOnStart=false`, then `SetActive(true)` + `Boot()`; after a yield, scene A is the active/loaded scene, the driver GameObject is still alive, `IsWaitingForSignal` is true; `RaiseSignal("advance")` then a yield ⇒ scene B is loaded, the driver is **still alive**, `OnEnded` fired, and `GraphFlowDriver.Active == driver`. Confirm RED (without persist the driver is destroyed by the first Single load → the flow can't advance).
- [ ] T007 [US1] In `GraphFlowDriver.cs` add (additive): `[SerializeField] bool _persistAcrossScenes = false` + `PersistAcrossScenes` property; `static GraphFlowDriver Active { get; private set; }`; an `Awake()` that, when `_persistAcrossScenes`, dedups against `Active` (a duplicate per-scene copy `Destroy(gameObject)`s itself, leaving the original) else sets `Active = this` + `DontDestroyOnLoad(gameObject)`; extend `OnDestroy` to clear `Active` when it owned it (keep `Stop()`). XML docs. Confirm T006 GREEN.

## Phase 4: Polish & cross-cutting

- [ ] T008 Run the ENTIRE suite via batchmode: 659 EditMode (unchanged behavior with persist OFF) + the new EditMode tests green, AND PlayMode (the prior 8 + the new cross-scene = 9) green. Record totals (graphcore/graphstandard untouched, FR-009).
- [ ] T009 [P] Bump `com.faolline.graphgameflow/package.json` `0.2.0` → `0.3.0`.
- [ ] T010 [P] Update `com.faolline.graphgameflow/README.md` (a prominent "Running a flow across scenes" section: the persist flag, `GraphFlowDriver.Active`, the SubGraph-decomposition note, `OnWaitingForTime`, `bootOnStart`, the waiting-state query) and `CHANGELOG.md` (`0.3.0`).
- [ ] T011 [P] Verify `[GraphGameFlow]` prefix, XML docs on every new public member, and that the slice-1/2 API is append-only (no changed signatures on `Boot`/`Tick`/`Advance`/`RaiseSignal`/`Stop`/existing events).

## Dependencies

- **US3 (T001→T002)** first: `bootOnStart` is used by the US1 PlayMode test (explicit boot). Same file as all
  other runtime edits.
- **US2 (T003→T004)** independent EditMode addition (same file → sequential after US3's edit).
- **US1 (T005→T006→T007)** the keystone: test scenes → real PlayMode regression (RED) → persist + `Active`
  (GREEN). Depends on `bootOnStart` (US3) for the explicit boot in the test.
- **Polish (T008–T011)** last.

## Parallel opportunities

- The three RED test tasks (T001, T003, T006) are each in their own test file region and can be written
  up-front; the implementations (T002, T004, T007) all touch `GraphFlowDriver.cs` so run **sequentially**.
- T005 (generate scenes) ‖ the EditMode test writing. T009/T010/T011 ‖ in Polish.

## Implementation strategy

- **MVP = US1** (persist + the real cross-scene regression) — it fixes the headline dogfooding gap and adds
  the test that was missing. US3's `bootOnStart` is a small prerequisite for cleanly booting in that test.
- **+ US2 + US3** complete the ergonomics the consumer worked around.
- **+ Polish**: 659 EditMode + 9 PlayMode green, docs, 0.3.0.

## Notes

- Only `Runtime/Driver/GraphFlowDriver.cs` (additive), the new PlayMode test + two test scenes, and
  `package.json`/docs change. graphcore + graphstandard + the rest of the gameflow runtime/editor UNTOUCHED.
- The keystone is FR-008: a real `SceneManager(Single)` test. The slice-1/2 stub recorded loads without
  tearing scenes down — exactly why the bug shipped green; the regression must load scenes for real.
