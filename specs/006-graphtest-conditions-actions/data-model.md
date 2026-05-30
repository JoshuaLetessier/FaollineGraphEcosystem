# Data Model: GraphTest — Conditions, Actions & Checkpoints

**Feature**: `006-graphtest-conditions-actions` | **Date**: 2026-05-29

---

## New Entities

### TestBoolCondition

| Field | Type | Serialized | Notes |
|-------|------|-----------|-------|
| `_parameterKey` | `string` | ✅ | Key to look up in `BaseContext` |
| `_expectedValue` | `bool` | ✅ | Value to compare against |

**Evaluate()**: `context.TryGet<bool>(_parameterKey, out var v) && v == _expectedValue`. Returns false (+ warning) when key is absent.

**CreateAssetMenu**: `"GraphTest/Conditions/Bool Condition"`

---

### TestAlwaysTrueCondition

No fields. `Evaluate()` always returns `true`.

**CreateAssetMenu**: `"GraphTest/Conditions/Always True"`

---

### TestAlwaysFalseCondition

No fields. `Evaluate()` always returns `false`.

**CreateAssetMenu**: `"GraphTest/Conditions/Always False"`

---

### TestLogAction

| Field | Type | Serialized | Notes |
|-------|------|-----------|-------|
| `_message` | `string` | ✅ | Text logged to console |

**Execute()**: `Debug.Log($"[GraphTest] Action: {_message}")`

**CreateAssetMenu**: `"GraphTest/Actions/Log Action"`

---

### TestSetBoolAction

| Field | Type | Serialized | Notes |
|-------|------|-----------|-------|
| `_parameterKey` | `string` | ✅ | Key to write in `BaseContext` |
| `_value` | `bool` | ✅ | Bool value to set |

**Execute()**: `context.Set<bool>(_parameterKey, _value)`

**CreateAssetMenu**: `"GraphTest/Actions/Set Bool Action"`

---

## Modified Entities

### BaseRunner *(graphcore)*

New property added:

| Member | Type | Notes |
|--------|------|-------|
| `CurrentNode` | `BaseNodeData` (readonly) | Peeks `_graphStack`, resolves node by `CurrentNodeId`. Returns null when stack is empty or `State == Idle`. |

Null guards added in `EnterCurrentNode` and `ExitAndAdvance` for `EntryConditions`, `OnEnterActions`, and `OnExitActions` iterations.

---

### TestNodeInspectorView *(graphTest Editor)*

New behaviour:
- `ClearInspector()` — when `_graph != null`, renders a bool parameter panel (foldout listing `_graph.Parameters` of type Bool, with Add/Remove buttons)
- `BindNode()` — unchanged except `ClearInspector()` is called first, which now shows the parameter panel briefly before node fields replace it

---

### TestGraphEditorWindow *(graphTest Editor)*

New fields:

| Field | Type | Notes |
|-------|------|-------|
| `_activeRunner` | `BaseRunner` | Null until first Run |
| `_activeContext` | `BaseContext` | Null until first Run |
| `_hasActiveSession` | `bool` | True after successful `runner.Start()` |

New toolbar buttons: **GoBack**, **GoBackToCheckpoint** (added via `PopulateToolbar` override).

---

## Relationships

```
TestBoolCondition / TestAlwaysTrueCondition / TestAlwaysFalseCondition
  └── assigned to: BaseEdgeData.Condition  (one per edge)
                   BaseNodeData.EntryConditions  (list, zero or more)

TestLogAction / TestSetBoolAction
  └── assigned to: BaseNodeData.OnEnterActions  (list, zero or more)
                   BaseNodeData.OnExitActions   (list, zero or more)
```

Condition and action assets are project-level `.asset` files — the same instance can be
shared across multiple edges/nodes. Modifications to the asset affect all references.
