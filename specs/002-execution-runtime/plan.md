# Implementation Plan: GraphCore Execution Runtime

**Branch**: `002-execution-runtime` | **Date**: 2026-05-27 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/002-execution-runtime/spec.md`

## Summary

This feature builds the headless execution layer of `com.faolline.graphcore` on top of the
data layer delivered in `001-data-layer`. It delivers four cohesive components:

1. **`BaseContext`** — a typed parameter blackboard (replacing the empty placeholder from the
   data layer) with `Set<T>`/`Get<T>`/`TryGet<T>`/`Has`, per-key change subscriptions, deep
   clone, and graph-parameter initialization.
2. **`INodeExecutor` + `NodeExecutorRegistry`** — a pluggable dispatch system that maps
   `NodeType` strings to executor implementations, enabling downstream libs to inject
   type-specific logic without modifying graphcore.
3. **`BaseRunner`** — a headless state machine (`Idle → NodeReady → Ended`, `Paused` for
   SubGraph) that drives graph traversal: evaluating conditions, executing actions and
   executors, managing a nested-SubGraph stack, and maintaining a bounded history for
   rewind and checkpoint restore.
4. **`GraphCycleException`** — raised before any cyclic SubGraph execution begins, per
   the constitution's mandatory cycle detection requirement.

Semver assessment: **MINOR** bump (0.1.0 → 0.2.0) — new public API; no existing public API
is removed or broken.

## Technical Context

**Language/Version**: C# 9 (Unity 6000.x Roslyn compiler — required for default interface methods on `INodeExecutor.Undo`)

**Primary Dependencies**: `com.faolline.graphcore.Runtime` (001-data-layer assembly); Unity 6000.x engine assemblies

**Storage**: None — fully in-memory runtime

**Testing**: Unity Test Runner, EditMode only (`com.faolline.graphcore.Tests.EditMode.asmdef`)

**Target Platform**: Any (Unity 6000.x, all platforms — headless, no platform-specific code)

**Project Type**: Library

**Performance Goals**: Synchronous; no performance targets — execution is frame-bound per host application

**Constraints**: No `MonoBehaviour`, no `UnityEvent`, no async/await, no ecosystem lib references, no Unity lifecycle dependency in `BaseContext`

**Scale/Scope**: Single runtime assembly extension; ~8 new files, ~500 LOC estimated

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Foundation Stability | ✅ PASS | New public API only (MINOR bump). `BaseContext` gains methods — non-breaking since it was abstract/empty. `INodeExecutor.Undo` has a default no-op — no force-upgrade on existing implementors. |
| II. Universal Abstractions Only | ✅ PASS | All concepts (context blackboard, runner, executor registry, cycle detection) are universal to any graph-based runtime. No domain semantics. |
| III. Specification-First | ✅ PASS | `spec.md` written and approved before `plan.md`. |
| IV. Test-Driven Development | ✅ PASS | TDD enforced in tasks. Tests written before each component. EditMode only. |
| V. Simplicity (YAGNI) | ✅ PASS | `Dictionary<string, object>` for context store (no per-type maps). `List<HistoryEntry>` for history. No async, no pooling. See research.md for alternatives rejected. |
| VI. Cross-lib Compatibility via SubGraph Only | ✅ PASS | `GraphCycleException` mandated. SubGraph context handled via `InheritParentContext` flag. No lib-specific knowledge in runner. |

**Pre-implementation gate**: PASSED. All principles satisfied. No violations requiring justification.

*Post-design re-check*: PASSED. The Phase 1 data model introduces no new violations.

## Project Structure

### Documentation (this feature)

```text
specs/002-execution-runtime/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── public-api.md    # Phase 1 output — public C# interface surface
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created by /speckit-plan)
```

### Source Code

```text
Runtime/
├── Graph/
│   └── BaseContext.cs          # REPLACE — full blackboard implementation (was empty abstract)
├── Execution/
│   ├── INodeExecutor.cs        # NEW
│   ├── NodeExecutorRegistry.cs # NEW
│   ├── BaseRunner.cs           # NEW
│   ├── GraphExecutionState.cs  # NEW
│   ├── HistoryEntry.cs         # NEW
│   ├── RunnerState.cs          # NEW (enum)
│   └── GraphCycleException.cs  # NEW
└── com.faolline.graphcore.Runtime.asmdef   # UNCHANGED

Tests/
└── EditMode/
    ├── BaseContextTests.cs             # NEW
    ├── NodeExecutorRegistryTests.cs    # NEW
    ├── BaseRunnerLinearTests.cs        # NEW
    ├── BaseRunnerSubGraphTests.cs      # NEW
    ├── BaseRunnerHistoryTests.cs       # NEW
    └── com.faolline.graphcore.Tests.EditMode.asmdef  # NEW (if not already present)
```

**Structure Decision**: All new execution types go in a new `Runtime/Execution/` subfolder to
separate execution concerns from the existing data-layer subfolders (`Graph/`, `Nodes/`, etc.).
`BaseContext` stays at `Runtime/Graph/BaseContext.cs` to preserve the path established by
the 001 data-model. No new assembly definition is added — the execution layer lives in the
same `com.faolline.graphcore.Runtime` assembly.

## Complexity Tracking

> No constitution violations. Section not required.
