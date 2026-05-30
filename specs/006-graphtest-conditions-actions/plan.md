# Implementation Plan: GraphTest — Conditions, Actions & Checkpoints

**Branch**: `006-graphtest-conditions-actions` | **Date**: 2026-05-29 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/006-graphtest-conditions-actions/spec.md`

## Summary

Extend `com.faolline.graphTest` with concrete `BaseCondition` and `BaseAction` subclasses
(`TestBoolCondition`, `TestAlwaysTrueCondition`, `TestAlwaysFalseCondition`, `TestLogAction`,
`TestSetBoolAction`), a persistent runner session in `TestGraphEditorWindow` enabling GoBack and
GoBackToCheckpoint toolbar buttons, and a parameter panel embedded in the inspector that exposes
the graph's bool parameters. Two MINOR graphcore changes: a `public BaseNodeData CurrentNode`
property on `BaseRunner`, and null guards in the runner's condition/action iteration loops.

---

## Technical Context

**Language/Version**: C# 9 (Unity 6000.x)

**Primary Dependencies**: `com.faolline.graphTest` (feature 005), `com.faolline.graphcore`

**Storage**: Unity `ScriptableObject` assets — conditions and actions are `.asset` files
created in the project and assigned via the inspector

**Testing**: NUnit via Unity Test Runner (EditMode only)

**Target Platform**: Unity Editor

**Project Type**: Extension of an existing downstream lib — no new assembly definitions required

**Performance Goals**: GoBack/GoBackToCheckpoint respond in under 1 second for any graph with
fewer than 100 history entries

**Constraints**: No `MonoBehaviour`; no `UnityEvent`; `ScriptableObject` lifecycle (instances
via `CreateAssetMenu`); EditMode tests only; only bool parameters in scope for this feature

---

## Constitution Check

| Principle | Status | Notes |
|-----------|--------|-------|
| I – Foundation Stability | ✅ Pass | Two MINOR additive changes to graphcore (`BaseRunner.CurrentNode` property + null guards). No existing public API modified or removed. |
| II – Universal Abstractions Only | ✅ Pass | `TestBoolCondition`, `TestLogAction`, etc. are domain-specific test helpers — they stay in `com.faolline.graphTest`. |
| III – Specification-First | ✅ Pass | `spec.md` written and approved. |
| IV – Test-Driven Development | ✅ Pass | Red-Green-Refactor mandatory; tests confirmed failing via Coplay `run_tests` before each implementation. |
| V – Simplicity (YAGNI) | ✅ Pass | Only bool parameters exposed in the panel (int/float/string deferred). Condition/action implementations are the simplest possible. |
| VI – Cross-lib Compatibility | ✅ Pass | No dependency on other ecosystem libs. |

---

## Phase 0: Research

### R-001 — Graphcore API Gap: `BaseRunner.CurrentNode`

- **Decision**: Add `public BaseNodeData CurrentNode` to `BaseRunner` — peeks the graph stack and resolves the current node by ID.
- **Rationale**: After `GoBack()` and `GoBackToCheckpoint()`, the editor needs to know which node is now active in order to log it. `OnNodeEntered` fires inside `RestoreEntry`, but the lambda subscribed during `ExecuteGraph` is no longer in scope. A stable property is the minimal clean surface.
- **Alternatives considered**: Persistent `OnNodeEntered` delegate stored on the window — rejected (more complex lifecycle, teardown required).

### R-002 — Null Guards in `BaseRunner`

- **Decision**: Add `if (condition == null)` / `if (action == null)` guards with `Debug.LogWarning("[GraphCore] ...")` before each `Evaluate`/`Execute` call in `EnterCurrentNode` and `ExitAndAdvance`.
- **Rationale**: Deleted ScriptableObject assets leave null entries in serialized lists. The runner must skip nulls rather than throw `NullReferenceException`.
- **Alternatives considered**: Try-catch in `ExecuteGraph` — rejected (only catches the first null; remaining iterations still throw).

### R-003 — Runner Session Persistence

- **Decision**: Store `_activeRunner`, `_activeContext`, and `_hasActiveSession` as fields on `TestGraphEditorWindow`. Run always creates a fresh session. GoBack/GoBackToCheckpoint operate on the stored session.
- **Rationale**: The runner's history is preserved after reaching `Ended` state; `GoBack()` works from any state including `Ended`.

### R-004 — Parameter Panel Placement

- **Decision**: Embed the parameter panel inside `TestNodeInspectorView`. When `ClearInspector()` is called (no node selected) and a graph is loaded, show the bool parameter list with Add/Remove controls. When a node is selected, `BindNode` hides it.
- **Rationale**: The inspector is idle when nothing is selected — reusing that space avoids a third pane and a layout change.

---

## Phase 1: Design & Contracts

### Data Model

See [data-model.md](data-model.md).

---

### Project Structure

#### Documentation

```text
specs/006-graphtest-conditions-actions/
├── plan.md       ← this file
├── data-model.md ← Phase 1 output
└── tasks.md      ← Phase 2 output (/speckit-tasks)
```

#### Source Code

```text
com.faolline.graphTest/
├── Runtime/
│   ├── Conditions/
│   │   ├── TestBoolCondition.cs          ← new; BaseCondition; key + expectedValue
│   │   ├── TestAlwaysTrueCondition.cs    ← new; BaseCondition; always true
│   │   └── TestAlwaysFalseCondition.cs   ← new; BaseCondition; always false
│   └── Actions/
│       ├── TestLogAction.cs              ← new; BaseAction; logs configurable message
│       └── TestSetBoolAction.cs          ← new; BaseAction; writes bool to context
└── Editor/
    ├── Inspector/
    │   └── TestNodeInspectorView.cs      ← update: parameter panel in ClearInspector
    └── Window/
        └── TestGraphEditorWindow.cs      ← update: session fields, GoBack, GoBackToCheckpoint

com.faolline.graphcore/
└── Runtime/
    └── Execution/
        └── BaseRunner.cs                 ← add: CurrentNode property + null guards
```

---

### Key Design Decisions

**D-001 — ScriptableObject asset pattern for conditions/actions**
All types use `[CreateAssetMenu]` so developers create `.asset` instances in the Project window, then drag them into the inspector. Assignments are serialized as `[SerializeField]` / `[SerializeReference]` fields already defined on `BaseEdgeData` and `BaseNodeData`.

**D-002 — `TestBoolCondition` missing-key behavior**
When `parameterKey` is absent from the context, `Evaluate()` logs
`[GraphTest] Condition: parameter key '{key}' not found — evaluating to false` and returns `false`.

**D-003 — Parameter Panel lifecycle**
`TestNodeInspectorView.SetGraph(graph)` stores a reference to the loaded graph.
`ClearInspector()` rebuilds the parameter panel when `_graph != null`.
`BindNode()` calls `ClearInspector()` first (clearing the panel), then adds node fields on top.

**D-004 — GoBack/GoBackToCheckpoint logging**
```
GoBack():
  if !_hasActiveSession → Log "[GraphTest] No active session — click Run first."
  else:
    _activeRunner.GoBack()
    node = _activeRunner.CurrentNode
    Log node != null ? $"[GraphTest] GoBack → {node.NodeType}"
                     : "[GraphTest] GoBack — nothing to go back to."

GoBackToCheckpoint():
  same pattern with _activeRunner.GoBackToCheckpoint()
```

**D-005 — Run resets session**
Clicking Run always constructs a fresh `BaseRunner` and `BaseContext`, discarding the prior session. `_hasActiveSession = true` after `runner.Start()` succeeds.

---

## Complexity Tracking

No constitution violations. No entries required.
