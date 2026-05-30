# Implementation Plan: GraphTest — Editor Authoring Gaps

**Branch**: `008-graphtest-authoring` | **Date**: 2026-05-30 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/008-graphtest-authoring/spec.md`

## Summary

Wire three already-runtime-supported graphcore capabilities into the `com.faolline.graphTest`
authoring surface so a developer can configure, run, save, and reload them from the editor:
(US1) an **EndReason** selector in the inspector for End nodes; (US2) a **SubGraph node**
(menu entry + `SubGraphNodeView` + inspector `TargetGraph`/`InheritParentContext` + edit-time
cycle refusal); (US3) **typed Int/Float/String parameters** with a generalized parameter panel
plus typed conditions and actions. **No graphcore change** — `EndReason`, `SubGraphNodeData`,
`ParameterType`, `BaseContext` (bool/int/float/string + `InitFromGraph` parsing), and
`CycleDetector` already exist.

---

## Technical Context

**Language/Version**: C# 9 (Unity 6000.3.x)

**Primary Dependencies**: `com.faolline.graphTest` (features 005–007), `com.faolline.graphcore`

**Storage**: Unity `ScriptableObject` assets; node/parameter fields serialized on `BaseGraph`; conditions/actions are `BaseCondition`/`BaseAction` `ScriptableObject`s referenced from nodes/edges/choices

**Testing**: NUnit via Unity Test Runner (EditMode only)

**Target Platform**: Unity Editor

**Project Type**: Extension of an existing downstream package — no new assembly definitions

**Performance Goals**: Inspector edits reflect on canvas in under one frame; Run/Choose response under 1 second

**Constraints**: No `MonoBehaviour`; no `UnityEvent`; EditMode tests only; synchronous execution loop; **no graphcore modification**; raw string keys never at C# call sites (Principle VI)

**Scale/Scope**: Three independent user stories; ~6 new runtime types (3 conditions + 3 actions), 1 new node view, 2 inspector sections, 1 parameter-panel generalization

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I – Foundation Stability | ✅ Pass | Zero graphcore changes. Reuses `EndNodeData.EndReason`, `SubGraphNodeData`, `ParameterType`, `BaseContext`, `CycleDetector` as-is. |
| II – Universal Abstractions Only | ✅ Pass | Typed conditions/actions are domain-specific and live in graphTest, not graphcore. |
| III – Specification-First | ✅ Pass | `spec.md` written, validated (checklist all-pass), approved. |
| IV – Test-Driven Development | ✅ Pass | Red-Green-Refactor per story; EditMode tests authored before implementation. |
| V – Simplicity (YAGNI) | ✅ Pass | Reuses existing runtime; typed conditions use a minimal operator/equality model — no expression language. |
| VI – Typed Context Contract | ✅ Pass | Conditions/actions stay generic (`BaseContext` parameter, key as serialized data — not a call-site literal). Any sample typed property added to `TestGameContext` follows the `XxxContextKeys` + `CreateCloneInstance()` pattern. |
| VII – Cross-lib Compatibility via SubGraph | ✅ Pass | `SubGraphNodeView` exposes `TargetGraph` as `BaseGraph`. Cycle detection enforced at edit time (inspector reuses `CycleDetector`) and runtime (existing `GraphCycleException`). |

**Result**: No violations. Complexity Tracking is empty.

---

## Project Structure

### Documentation (this feature)

```text
specs/008-graphtest-authoring/
├── plan.md          # This file
├── research.md      # Phase 0 — decisions
├── data-model.md    # Phase 1 — entities
├── quickstart.md    # Phase 1 — manual validation walkthrough
└── tasks.md         # Phase 2 — /speckit-tasks (not created here)
```

No `contracts/` directory: graphTest is internal verification tooling with no external/consumer API surface (consistent with features 005–007).

### Source Code

```text
com.faolline.graphTest/
├── Runtime/
│   ├── Conditions/
│   │   ├── TestIntCondition.cs        ← new; compares an int param via an operator
│   │   ├── TestFloatCondition.cs      ← new; compares a float param via an operator
│   │   └── TestStringCondition.cs     ← new; string equality/inequality
│   └── Actions/
│       ├── TestSetIntAction.cs        ← new; writes an int into context
│       ├── TestSetFloatAction.cs      ← new; writes a float into context
│       └── TestSetStringAction.cs     ← new; writes a string into context
└── Editor/
    ├── Nodes/
    │   └── SubGraphNodeView.cs        ← new; one "in", one "out"
    ├── Inspector/
    │   └── TestNodeInspectorView.cs   ← update: EndReason section (US1),
    │                                     SubGraph section (US2),
    │                                     typed parameter panel (US3)
    └── Graph/
        └── TestGraphView.cs           ← update: "Add SubGraph Node" menu + CreateNodeView dispatch
```

**Structure Decision**: Extend the existing `com.faolline.graphTest` package in place. New runtime types go under `Runtime/Conditions` and `Runtime/Actions` (existing folders); the new node view under `Editor/Nodes`; all inspector wiring in the existing `TestNodeInspectorView`. No new assembly definitions, no graphcore edits.

---

## Phase 0: Research

### R-001 — Editing EndReason in the inspector (US1)

- **Decision**: In `TestNodeInspectorView.BindNode`, when `node is EndNodeData`, render a UI Toolkit `EnumField` initialized to `endNode.EndReason`. On value change, set `endNode.EndReason`, mark the graph dirty. Persistence is automatic (`EndReason` is `[SerializeField]` on `EndNodeData`, stored in the graph's `[SerializeReference]` node list).
- **Rationale**: Mirrors the existing direct-mutation pattern used for the choice section (edit the data object + `SetDirty`), avoiding `SerializedProperty` binding complexity for a single enum. The run-time log already reports the reason — no execution change needed.
- **Alternatives**: `SerializedProperty`/`PropertyField` binding (rejected: heavier, and `EndReason` isn't part of the universal base-node section).

### R-002 — Refusing a recursive SubGraph at edit time (US2)

- **Decision**: When the inspector's `TargetGraph` `ObjectField` changes, call `CycleDetector.Check(currentGraph, proposedTarget)`. If `HasCycle`, revert the field to its previous value and log `[GraphTest] Cycle refused: <path>`; otherwise accept and `SetDirty`.
- **Rationale**: A recursive target assigned via the inspector never passes through `HandleEdgeCreation` (which only checks on edge connection), so the edit-time guard must live where the assignment happens. `CycleDetector` (graphcore Editor) already implements the DFS — reuse it, no graphcore change. Runtime remains protected by the existing `GraphCycleException` in `EnterSubGraph`, caught by the window's `ExecuteGraph`.
- **Alternatives**: Only rely on runtime detection (rejected: FR-010 requires edit-time refusal). Modify graphcore to check on TargetGraph set (rejected: graphcore change).

### R-003 — Executing a SubGraph node from the window loop (US2)

- **Decision**: No special handling in `TestGraphEditorWindow.DrainLoop`. `BaseRunner` enters/exits sub-graphs transparently inside `Proceed()`; the window logs each visited node (including the child's). A null `TargetGraph` triggers `OnStuck` inside `EnterSubGraph`, which the window already surfaces as a stuck warning. A Choice node inside a sub-graph still pauses correctly (the `CurrentNode is ChoiceNodeData` check is graph-agnostic).
- **Rationale**: The runner is the single source of truth for traversal; the editor loop stays a thin driver. Satisfies FR-009 and FR-011 with zero new execution logic.

### R-004 — Typed condition comparison model (US3)

- **Decision**: `TestIntCondition` and `TestFloatCondition` carry a `ParameterKey`, a `ComparisonOperator` enum (`Equal, NotEqual, Less, LessOrEqual, Greater, GreaterOrEqual`), and an expected value. `TestStringCondition` carries a `ParameterKey`, an expected string, and a `negate` bool (equality / inequality). All read via `context.TryGet<T>` and return `false` with a `[GraphTest]` warning when the key is absent or mistyped — identical to `TestBoolCondition`.
- **Rationale**: An operator enum covers all realistic ordered comparisons with one small type per numeric kind; strings only need (in)equality. No expression language (YAGNI). The `ComparisonOperator` enum is a graphTest type (domain-specific), not graphcore.
- **Alternatives**: A single generic comparison type (rejected: Unity `[SerializeReference]`/inspector ergonomics favor one concrete type per value type, matching `TestBoolCondition`).

### R-005 — Generalizing the parameter panel (US3)

- **Decision**: Replace the bool-only `BuildParameterPanel` with a panel that lists **all** parameters (showing key, type, default) and an add-row containing a key `TextField`, a `ParameterType` `EnumField`, and a default-value `TextField`. `AddParameter(key, type, default)` stores a `ParameterData` with the chosen type; `RemoveParameter` is type-agnostic. The default value stays a string (parsed by `BaseContext.InitFromGraph`, which already handles all four types).
- **Rationale**: `ParameterData` already models `Type` + string `DefaultValue`, and `InitFromGraph` already parses Bool/Int/Float/String. The only gap is the editor panel, so the change is purely presentational. Backward-compatible: the existing `AddBoolParameter`/`RemoveBoolParameter` helpers are kept (delegating to the generalized methods) so feature-006 parameter tests stay green.
- **Alternatives**: Per-type default editor widgets (int field, float field) — deferred; a single string field with per-type parse-on-blur validation is sufficient and simplest.

### R-006 — Typed context keys vs. generic conditions/actions (Principle VI)

- **Decision**: Conditions/actions remain generic — they take `BaseContext` and use their serialized `ParameterKey` (data, set in the inspector), so no raw key literal appears at a C# call site. No mandatory change to `TestGameContext`. Optionally, sample typed properties (e.g. an `int Score`) plus `TestContextKeys` consts may be added to support a richer sample graph; if so, they follow the existing keys-class + `CreateCloneInstance()` pattern (already overridden in `TestGameContext`).
- **Rationale**: Principle VI targets call-site literals in C#; configurable condition/action keys are data, not literals — consistent with how `TestBoolCondition` already works. Keeps the feature minimal.

---

## Phase 1: Design & Contracts

### Data Model

See [data-model.md](data-model.md).

### Key Design Decisions

**D-001 — EndReason section** *(US1)*
`TestNodeInspectorView.BindNode` dispatches on `node is EndNodeData` → renders `EnumField` bound to `EndReason`; change handler mutates the node and `SetDirty`. No new files.

**D-002 — SubGraphNodeView** *(US2)*
`SubGraphNodeView : BaseNodeView`, one `"in"` (`Capacity.Multi`, `TestEdgeView`), one `"out"` (`Capacity.Single`). `TestGraphView`: "Add SubGraph Node" menu entry creates `SubGraphNodeData`; `CreateNodeView` adds `SubGraphNodeData.NodeTypeId → new SubGraphNodeView(...)`.

**D-003 — SubGraph inspector section** *(US2)*
When `node is SubGraphNodeData`: an `ObjectField` typed `BaseGraph` for `TargetGraph` (with the R-002 cycle guard on change) and a `Toggle` for `InheritParentContext`. Direct mutation + `SetDirty`.

**D-004 — Typed conditions & actions** *(US3)*
Six `ScriptableObject` types under `Runtime/Conditions` and `Runtime/Actions`, mirroring `TestBoolCondition`/`TestSetBoolAction`. Numeric conditions use a `ComparisonOperator` enum (new graphTest type). All conditions null-safe on missing/mistyped keys.

**D-005 — Generalized parameter panel** *(US3)*
`TestNodeInspectorView` parameter panel lists all parameter types and adds via (key, type, default). `AddBoolParameter`/`RemoveBoolParameter` retained as thin wrappers for backward compatibility.

### Agent Context

`CLAUDE.md` is updated to point at `specs/008-graphtest-authoring/plan.md`.

---

## Complexity Tracking

No constitution violations. No entries required.
