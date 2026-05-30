# Data Model: GraphTest — Editor Authoring Gaps

**Feature**: `008-graphtest-authoring` | **Date**: 2026-05-30

---

## Reused Entities (graphcore — no change)

### EndNodeData

| Member | Type | Notes |
|--------|------|-------|
| `EndReason` | `EndReason` (enum) | Completed / Cancelled / Error; `[SerializeField]`, already serialized. US1 makes it editable. |

### SubGraphNodeData

| Member | Type | Notes |
|--------|------|-------|
| `NodeTypeId` | `const string` | `"graphcore/subgraph"` |
| `TargetGraph` | `BaseGraph` | The graph to invoke; null = unlinked. |
| `InheritParentContext` | `bool` | True = sub-graph receives the parent context. |

### ParameterData / ParameterType

| Member | Type | Notes |
|--------|------|-------|
| `Key` | `string` | Parameter name. |
| `Type` | `ParameterType` | Bool / Int / Float / String. |
| `DefaultValue` | `string` | Parsed per type by `BaseContext.InitFromGraph`. |

### BaseContext / CycleDetector

- `Set<T>` / `TryGet<T>` support bool/int/float/string. `InitFromGraph` parses all four.
- `CycleDetector.Check(root, proposed)` → `CycleDetectionResult { HasCycle, CyclePath }` (graphcore Editor).

---

## New Entities (graphTest)

### US1 — (no new type)

EndReason editing is inspector-only: an `EnumField` bound to `EndNodeData.EndReason`.

### US2 — SubGraphNodeView *(new, editor)*

| Aspect | Detail |
|--------|--------|
| Base | `BaseNodeView` |
| Input port | one `"in"`, `Capacity.Multi`, typed `TestEdgeView` |
| Output port | one `"out"`, `Capacity.Single`, typed `TestEdgeView` |
| Title | "SubGraph" |

### US3 — ComparisonOperator *(new, runtime enum)*

`Equal, NotEqual, Less, LessOrEqual, Greater, GreaterOrEqual` — used by numeric conditions.

### US3 — Typed conditions *(new, `BaseCondition` subclasses)*

| Type | Fields | Evaluate |
|------|--------|----------|
| `TestIntCondition` | `ParameterKey: string`, `Operator: ComparisonOperator`, `ExpectedValue: int` | `TryGet<int>` then compare; false + warn if absent/mistyped |
| `TestFloatCondition` | `ParameterKey: string`, `Operator: ComparisonOperator`, `ExpectedValue: float` | `TryGet<float>` then compare; false + warn if absent/mistyped |
| `TestStringCondition` | `ParameterKey: string`, `ExpectedValue: string`, `Negate: bool` | `TryGet<string>` then (in)equality; false + warn if absent |

`[CreateAssetMenu]` on each, like `TestBoolCondition`.

### US3 — Typed actions *(new, `BaseAction` subclasses)*

| Type | Fields | Execute |
|------|--------|---------|
| `TestSetIntAction` | `ParameterKey: string`, `Value: int` | `context.Set<int>(key, value)` |
| `TestSetFloatAction` | `ParameterKey: string`, `Value: float` | `context.Set<float>(key, value)` |
| `TestSetStringAction` | `ParameterKey: string`, `Value: string` | `context.Set<string>(key, value)` |

`[CreateAssetMenu]` on each, like `TestSetBoolAction`.

---

## Modified Editor Entities (graphTest)

### TestNodeInspectorView *(modified)*

- **US1**: when `node is EndNodeData` → render `EnumField` for `EndReason` (mutate + `SetDirty`).
- **US2**: when `node is SubGraphNodeData` → `ObjectField`(`BaseGraph`) for `TargetGraph` with a cycle guard on change (revert + log if `CycleDetector.Check` reports a cycle), and a `Toggle` for `InheritParentContext`.
- **US3**: parameter panel generalized — lists all parameter types; add-row has key field + `ParameterType` `EnumField` + default-value field; `AddParameter(key, type, default)` / type-agnostic remove. `AddBoolParameter`/`RemoveBoolParameter` kept as wrappers.

### TestGraphView *(modified)*

- "Add SubGraph Node" context-menu entry → creates `SubGraphNodeData`.
- `CreateNodeView`: `SubGraphNodeData.NodeTypeId → new SubGraphNodeView(...)`.

---

## Relationships & Invariants

```
EndNodeData.EndReason        → inspector EnumField → logged "Graph ended: {reason}"
SubGraphNodeData.TargetGraph → inspector ObjectField (cycle-guarded) → runner descends into it
ParameterData.Type/Default   → parameter panel → BaseContext.InitFromGraph seeds context
TestInt/Float/StringCondition.ParameterKey → context.TryGet<T> → routing / choice filtering
TestSetInt/Float/StringAction.ParameterKey → context.Set<T> → downstream conditions observe it
```

**Invariants**:
- Assigning a `TargetGraph` that references the current graph (directly/transitively) is refused at edit time; runtime additionally throws `GraphCycleException`.
- Typed conditions never throw on missing/mistyped keys — they return false and warn.
- Parameter default values are stored as strings and parsed per type; parse failures fall back to `default(T)` with a warning (existing `InitFromGraph` behavior).
