# Implementation Plan: Full Graph-Stack Save & Restore

**Branch**: `053-graph-stack-snapshot` | **Date**: 2026-10-03 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/053-graph-stack-snapshot/spec.md` (decisions D1-A, D2-A, D3-A, D4-A; D5 candidates adopted provisionally)

## Summary

`GraphRunSnapshot` stops being "top-level only". Three layers, each doing one job:

1. **graphcore** gets the only code that can touch `BaseRunner`'s private graph stack. `BaseRunner.GetExecutionPath()` reads the active levels root → deepest as immutable `GraphRunFrame`s (graph id, node id, context mode). `GraphRunPath.TryResolve(root, path, …)` is a pure check that walks down from the supplied root through each enclosing `SubGraphNodeData.TargetGraph` and reports the first mismatch as a `GraphRunPathError` (reason, failing frame, deepest valid frame). `BaseRunner.StartFromPath(root, path, context, registry)` re-checks, then rebuilds every frame (shared / scoped / fresh context per frame), re-enters **only** the deepest frame's node, and leaves the run indistinguishable from one built by play. It throws `GraphRunPathException` on an invalid path, before mutating anything.
2. **graphsave** serializes the path: `GraphRunSnapshot.Frames` (its own `[Serializable]` `Frame`, public fields like `Param`/`Collection`). `Capture(runner, context)` fills it and reports, without throwing, a fresh-context frame whose values the save cannot hold (D1-A). `HasStack`, `RootGraphId`, `TryValidate(root, out error)` and `GetExecutionPath()` are new. `Restore(runner, graph, context, registry)` keeps its signature: no stack → unchanged; stack → check first, log an error and change nothing on mismatch, else `ApplyTo` then `StartFromPath`.
3. **graphgameflow**: `GraphFlowDriver.Boot(snapshot, …)` checks a stacked snapshot against the driver's `Graph` (its root) before creating its context or runner. A mismatch logs a `[GraphGameFlow]` error and stays inert. Otherwise it restores the stack via `StartFromPath`.

graphdialoguesystem gets documentation only (D4-A). The `IGraphCatalog` docs say the root id is the key to resolve.

## Technical Context

**Language/Version**: C# (Unity 6000.0 floor; dev project on 6000.3.20f1), same conventions as 047–052

**Primary Dependencies**:
- `com.faolline.graphcore` 0.43.4 → **0.44.0** (MINOR: new public API only): `GraphRunFrame`, `GraphRunContextMode`, `GraphRunPath`, `GraphRunPathError`, `GraphRunPathErrorReason`, `GraphRunPathException`, `BaseRunner.GetExecutionPath()`, `BaseRunner.StartFromPath(...)`
- `com.faolline.graphsave` 0.10.0 → **0.11.0** (MINOR): `GraphRunSnapshot.Frame`, `Frames`, `HasStack`, `RootGraphId`, `TryValidate`, `GetExecutionPath`; `Capture(runner, ctx)` records the stack; `Restore` restores it. Floor graphcore → 0.44.0.
- `com.faolline.graphgameflow` 0.19.0 → **0.20.0** (MINOR, behavior addition): `Boot(snapshot)` restores the stack. Floors graphcore → 0.44.0, graphsave → 0.11.0.
- `com.faolline.graphdialoguesystem` 0.20.0 → **0.20.1** (PATCH, documentation only)
- `com.faolline.graphsave.savesystem`: test only (stacked round-trip through the real Newtonsoft-based `JsonSaveSystem`), no version change
- No new external dependency, no new assembly, no new asmdef edge

**Storage**: the existing `GraphRunSnapshot` JSON, with one added list field (`Frames`). JsonUtility (graphsave's own tests, `JsonFileGraphSaveStore`) and Newtonsoft (`com.faolline.savesystem.json`, via the savesystem bridge) must both round-trip it.

**Testing**: Unity Test Framework, EditMode only, graphs built in code (`ScriptableObject.CreateInstance<BaseGraph>`), `LambdaExecutor`/tracking actions to count re-entries. The dev project's Editor is closed (only Cryptique's is open), so tests run headless: `Unity.exe -batchmode -projectPath <dev project> -runTests -testPlatform EditMode -testFilter <…> -testResults <xml> -logFile <log>`, **without** `-quit`. Targeted filters during red/green; full ecosystem suite before each commit batch. Constitution IV asks for Coplay MCP `run_tests`. It is not available in this session, so batchmode is used, the established fallback (as in 049–052).

**Target Platform**: all player platforms (runtime code; no editor-only API in the restore path)

**Project Type**: Library changes across 3 existing packages + documentation in 2 more

**Performance Goals**: restore is O(depth × node lookup), a negligible cost next to a scene load. `GetExecutionPath()` allocates one small list per call (capture-time only).

**Constraints**:
- Existing public signatures unchanged (FR-018); `Restore`/`Boot(snapshot)` keep `void`.
- All-or-nothing: no context value or runner state changes before the whole path is checked (FR-007).
- Capture never throws (FR-004): it runs inside the consumer's `OnNodeEntered` handler, where an exception would abort `EnterCurrentNode` mid-entry.
- No silent guess: every mismatch is reported; no automatic partial resume (D3-A).

**Scale/Scope**: Cryptique-scale. Depth 2–3 (chapter → zone → maybe one nested sub-graph), one save per checkpoint.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment |
|---|---|
| I. Foundation Stability | ✅ graphcore MINOR (0.44.0): only additions. `BaseNodeData` untouched, `INodeExecutor` untouched, `StartFrom`/`Start`/`GoBack` behavior unchanged. |
| II. Universal Abstractions Only | ✅ Execution path, sub-graph frames and context modes are core execution concepts (the stack already exists in graphcore). Nothing save-, scene- or dialogue-specific enters graphcore; serialization stays in graphsave. |
| III. Specification-First | ✅ spec.md written, decisions D1–D4 answered by the user before this plan. |
| IV. TDD | ✅ Every implementation task is preceded by a failing test (red confirmed in a batchmode run). EditMode only. Runner: batchmode fallback (see Testing). |
| V. Simplicity | ⚠️ Two justified deviations — see Complexity Tracking. |
| VI. Typed Context Contract | N/A. No context keys added. A restored fresh frame uses `new BaseContext()`, exactly as `EnterSubGraph` does today. |
| VII. Cross-lib via SubGraph only | ✅ No new asmdef edge. Restore walks `SubGraphNodeData.TargetGraph` (a `BaseGraph`). A repeated graph in a saved path is reported as a `GraphRunPathError` before anything executes. Live cycle detection (`GraphCycleException` in `EnterSubGraph`) is unchanged and still applies once the restored run continues. |
| Dev standards: logging/docs | ✅ Diagnostics via `Faolline.GraphLogging.Logging` with `[GraphCore]`/`[GraphSave]`/`[GraphGameFlow]` prefixes. XML `<summary>` on every new public type and member. |
| Semver gate | ✅ Per-package assessment above. One behavior note in the graphsave CHANGELOG: a snapshot captured from a runner now carries its path, so restoring it into a graph other than its root fails loudly instead of re-entering a same-id node by chance (spec Assumptions; no saves exist). |

**Post-design re-check (after Phase 1)**: ✅ unchanged. The contracts ([contracts/public-api.md](contracts/public-api.md)) add types and members only; no existing signature changes.

## Project Structure

### Documentation (this feature)

```text
specs/053-graph-stack-snapshot/
├── plan.md              # This file
├── research.md          # Phase 0: decisions R1–R12
├── data-model.md        # Phase 1: frames, modes, errors, snapshot fields, check order
├── quickstart.md        # Phase 1: consumer-side capture/restore + validation recipe
├── contracts/
│   └── public-api.md    # C# surface added, per package
├── checklists/requirements.md
└── tasks.md             # Phase 2 (/speckit-tasks)
```

### Source Code (repository root)

```text
com.faolline.graphcore/
├── Runtime/Execution/
│   ├── GraphRunContextMode.cs        # NEW enum: Shared = 0, Scoped = 1, Fresh = 2
│   ├── GraphRunFrame.cs              # NEW immutable frame description
│   ├── GraphRunPath.cs               # NEW static pure check (TryResolve)
│   ├── GraphRunPathError.cs          # NEW error (Reason, FrameIndex, DeepestValidFrameIndex, Message)
│   ├── GraphRunPathErrorReason.cs    # NEW enum
│   ├── GraphRunPathException.cs      # NEW (thrown by StartFromPath on an invalid path)
│   └── BaseRunner.cs                 # + GetExecutionPath(), + StartFromPath(...)
├── Tests/EditMode/Execution/
│   ├── ExecutionPathTests.cs         # NEW: GetExecutionPath, StartFromPath continuation, modes
│   └── ExecutionPathValidationTests.cs  # NEW: TryResolve reasons / indices, all-or-nothing
├── README.md / CHANGELOG.md / package.json

com.faolline.graphsave/
├── Runtime/GraphRunSnapshot.cs       # + Frame, Frames, HasStack, RootGraphId, TryValidate, GetExecutionPath;
│                                     #   Capture(runner) records the path + fresh-frame report; Restore stack path
├── Tests/EditMode/GraphStackSnapshotTests.cs   # NEW
├── README.md / CHANGELOG.md / package.json

com.faolline.graphsave.savesystem/
└── Tests/EditMode/SaveSystemGraphStoreTests.cs # + stacked round-trip through the real JsonSaveSystem

com.faolline.graphgameflow/
├── Runtime/Driver/GraphFlowDriver.cs # Boot(snapshot): check against Graph, StartFromPath when stacked
├── Runtime/Graph/IGraphCatalog.cs    # doc: root id is the key to resolve
├── Tests/EditMode/GraphFlowDriverSnapshotTests.cs  # NEW
├── README.md / CHANGELOG.md / package.json

com.faolline.graphdialoguesystem/
├── Runtime/Playback/DialoguePlayer.cs, DialogueSessionState.cs   # XML doc: single-level limit (D4-A)
├── README.md / CHANGELOG.md / package.json

README.md, com.faolline.graphcore/Editor/GraphEcosystemModules.json, every package.json floor + README header
                                      # final version/floor sync (recurring drift habit)
```

**Structure Decision**: The stack stays private to `BaseRunner`. graphcore exposes only immutable descriptions (`GraphRunFrame`) and two operations, read and restore, plus one pure check. That is the minimum surface that lets a layer above save and restore a run without being able to corrupt a live one. Serialization stays in graphsave, which already owns the save format. The driver only routes.

## Complexity Tracking

| Deviation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Two frame types: graphcore `GraphRunFrame` (immutable) + graphsave `GraphRunSnapshot.Frame` (`[Serializable]`, public fields), mapped in graphsave | graphsave owns the save format (its JSON must not change when a graphcore runtime type gains a member), and JsonUtility + Newtonsoft both need public fields on a `[Serializable]` class | One `[Serializable]` class with public mutable fields in graphcore would put a persistence format into the foundation (Principle II) and let any caller mutate a frame description the runner handed out. The mapping is ~10 lines. |
| `GraphRunPathException` + a separate `TryResolve` check, instead of a single `bool TryStartFromPath` | The driver's `Boot` is `void` and deferred when re-entrant, so the caller needs a check it can run *before* booting (FR-008). `StartFromPath` must still refuse an unchecked bad path (FR-007/FR-009). | A `bool` return on `StartFromPath` alone gives the driver no way to pre-check, and a silent `false` from a direct runner call is easy to ignore. Throwing on invalid input matches `StartFrom`'s existing argument exceptions. |
