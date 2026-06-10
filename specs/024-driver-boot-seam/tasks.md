---
description: "Task list for 024-driver-boot-seam (additive Boot(context, registry) overload on GraphFlowDriver)"
---

# Tasks: gameflow driver boot configuration seam (slice 5)

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/public-api.md, quickstart.md

**Tests**: REQUIRED (TDD — tests before code), all EditMode. Batchmode (no `-quit`; re-run after source
change; verify XML). Branch `024-driver-boot-seam` (stacks on master). **graphcore + graphstandard UNTOUCHED;
slice-1..4 driver API append-only (`Boot()` unchanged).** All change is in one file
(`Runtime/Driver/GraphFlowDriver.cs`) + its test file → the three user stories are one cohesive edit.

## Phase 1: US1–US3 — the boot seam (Priority: P1) 🎯

**Goal**: an additive `Boot(GameFlowContext, NodeExecutorRegistry)` overload — run the flow on a caller-seeded
context (US1) with caller-registered executors (US2), while `Boot()` stays byte-for-byte unchanged (US3).

**Independent test**: a context seeded before boot is the live one and survives (no re-init); a registered
executor runs for its node type; `Boot()` (no args) behaves exactly as before.

- [ ] T001 [P] [US1] Extend `com.faolline.graphgameflow/Tests/EditMode/GraphFlowDriverTests.cs`: a `GameFlowContext` with `Set<int>("seed",42)` (and an `AddToCollection`) passed to `Boot(context, null)` ⇒ `driver.Context` is **that** instance and the value survives (INV-1); a graph declaring a `ParameterData` default X with the provided context pre-set to Y ⇒ after `Boot(context, null)` the value is still Y (no `InitFromGraph`, INV-2); a provided context with null `SceneLoader` gets the driver's (a `LoadSceneAction` reaches the injected `StubSceneLoader`), and a context with its own loader keeps it (INV-3). Confirm RED.
- [ ] T002 [P] [US2] Add to `GraphFlowDriverTests.cs`: a `NodeExecutorRegistry` with a test `INodeExecutor` registered for `StatementNodeData.NodeTypeId` (its `Execute` writes a sentinel to the context) ⇒ `Boot(ctx, registry)` and running a flow through such a node invokes the executor (the sentinel is set) (INV-4); booting with a null registry still runs statement/await nodes. Confirm RED.
- [ ] T003 [P] [US3] Add to `GraphFlowDriverTests.cs`: `Boot()` (no args) creates a fresh context initialised from the graph (a graph-declared parameter default is applied) and an empty registry (INV-5); the guards (no graph / already running) fire identically when `Boot(context, registry)` is called (same `[GraphGameFlow]` warnings, stays inert). Confirm RED.
- [ ] T004 [US1] Implement the seam in `com.faolline.graphgameflow/Runtime/Driver/GraphFlowDriver.cs`: extract the current `Boot()` body into `private void BootInternal(GameFlowContext context, NodeExecutorRegistry registry)` (guards; `if (context != null) { _context = context; if (_context.SceneLoader == null) _context.SceneLoader = SceneLoader; }` else `{ _context = new GameFlowContext { SceneLoader = SceneLoader }; _context.InitFromGraph(_graph); }`; subscribe; `_running = true`; `_runner.Start(_graph, _context, registry ?? new NodeExecutorRegistry())`); make `Boot()` call `BootInternal(null, null)`; add public `Boot(GameFlowContext context, NodeExecutorRegistry registry)` → `BootInternal(context, registry)`. XML docs; `[GraphGameFlow]` unchanged. Confirm T001/T002/T003 GREEN.

## Phase 2: Polish

- [ ] T005 Run the ENTIRE suite via batchmode: EditMode (667 prior + the new boot-seam tests) green AND PlayMode (9) green (graphcore/graphstandard untouched, INV-6). Record totals.
- [ ] T006 [P] Bump `com.faolline.graphgameflow/package.json` `0.4.0 → 0.5.0`; update `README.md` (a short "prepare the context / register executors before boot" note on the `Boot(context, registry)` overload — the foundation for hosting a progression/ability system on the shared context) and `CHANGELOG.md` (`0.5.0`).
- [ ] T007 [P] Verify `[GraphGameFlow]` prefix, XML docs on the new overload, and append-only (no changed signatures on `Boot()`/`Tick`/`Advance`/`RaiseSignal`/`Stop`/events/properties; graphcore + graphstandard untouched).

## Dependencies

- **US1/US2/US3 tests (T001–T003)** can be written together (same file) → all RED; **T004** implements the
  single seam that turns them GREEN.
- **Polish (T005–T007)** last.

## Implementation strategy

- One cohesive append-only edit: the overload + a shared private path. `Boot()` is preserved by delegation, so
  US3 (no behavior change) is structurally guaranteed.
- This is the **seam only** — the Reactive/Flow hosting helpers that will use it are the next slice.
