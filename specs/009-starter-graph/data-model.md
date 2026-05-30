# Data Model: starterGraph

**Feature**: `009-starter-graph` | **Date**: 2026-05-30

All entities are new and live in `com.faolline.starterGraph`. They extend graphcore base types
(no graphcore change).

## Runtime

### StarterGraph
`BaseGraph` subclass with `[CreateAssetMenu]`. Holds nodes, edges, parameters (inherited).

### StarterContext / StarterContextKeys
- `StarterContext : BaseContext` — typed example properties: `Flag` (bool), `Score` (int), `Ratio` (float), `Label` (string), each `get`/`set` via `TryGet<T>`/`Set<T>` and a key from `StarterContextKeys`. Overrides `CreateCloneInstance()` → `new StarterContext()`.
- `StarterContextKeys` — `static class` with one `const string` per key (the only place the literals live).

### StarterChoice
`BaseChoice` subclass with `[SerializeField] string _label` → public `Label`. Inherits `Id`, `Condition`.

### StarterStatementNodeData
`StatementNodeData` subclass with `const string NodeTypeId`, an editable `Label`. Example domain node.

### Conditions (`BaseCondition` subclasses, `[CreateAssetMenu]`)

| Type | Fields | Evaluate |
|------|--------|----------|
| `StarterAlwaysTrueCondition` | — | true |
| `StarterAlwaysFalseCondition` | — | false |
| `StarterBoolCondition` | `ParameterKey`, `ExpectedValue: bool` | `TryGet<bool>` == expected; false+warn if absent |
| `StarterIntCondition` | `ParameterKey`, `Operator: ComparisonOperator`, `ExpectedValue: int` | compare; false+warn if absent/mistyped |
| `StarterFloatCondition` | `ParameterKey`, `Operator`, `ExpectedValue: float` | compare; false+warn if absent/mistyped |
| `StarterStringCondition` | `ParameterKey`, `ExpectedValue: string`, `Negate: bool` | (in)equality; false+warn if absent/mistyped |

### ComparisonOperator (enum)
`Equal, NotEqual, Less, LessOrEqual, Greater, GreaterOrEqual`.

### Actions (`BaseAction` subclasses, `[CreateAssetMenu]`)

| Type | Fields | Execute |
|------|--------|---------|
| `StarterLogAction` | `Message` | logs the message |
| `StarterSetBoolAction` | `ParameterKey`, `Value: bool` | `Set<bool>` |
| `StarterSetIntAction` | `ParameterKey`, `Value: int` | `Set<int>` |
| `StarterSetFloatAction` | `ParameterKey`, `Value: float` | `Set<float>` |
| `StarterSetStringAction` | `ParameterKey`, `Value: string` | `Set<string>` |

## Editor

### Node views (`BaseNodeView` subclasses)
- `StartNodeView` — one `out`.
- `EndNodeView` — one `in`.
- `StarterStatementNodeView` — `in` + `out`, shows label.
- `ChoiceNodeView` — one `in`; one output per choice, `portName = choice.Id`, displayed label = choice label; `RebuildPorts()` / `UpdateChoiceLabel()`.
- `SubGraphNodeView` — `in` + `out`, shows target graph name.

### StarterEdgeView
`BaseEdgeView` subclass — the typed edge for all port declarations.

### StarterGraphView (`BaseGraphView`)
`CreateNodeView` dispatch for all five node types + `StarterEdgeView`; context menu adds each node type; `GetChoiceView`, `RemoveChoiceEdges` (inherits `ReconnectNodeEdges`, LoadGraph data-safety).

### StarterNodeInspectorView (`BaseNodeInspectorView`)
Sections: label (Statement), EndReason `EnumField` (End), choice (add/remove/label/condition + live ports + reconnect), sub-graph (`ObjectField<BaseGraph>` target with cycle refusal + `Toggle` inherit), typed parameter panel (key + `ParameterType` enum + default; add/remove any type; bool wrappers kept), shared base-node section.

### StarterGraphEditorWindow (`BaseGraphEditorWindow`)
Toolbar Run / Choose / Continue / GoBack / GoBackToCheckpoint; drain loop pausing at `ChoiceNodeData`; `ChooseById`; per-asset multi-window via `OnOpenAsset` (focus existing or `CreateWindow`, titled by asset name).

### StarterSampleBuilder (editor menu)
Generates a parent + child `StarterGraph` exercising choices, conditions, actions, a checkpoint, a sub-graph, and typed parameters.

## Invariants
- Each choice output port `portName` == choice `Id`; routing via `ChooseById(choice.Id)`.
- Sub-graph `TargetGraph` cycles refused at edit time; runtime cycle → `GraphCycleException`.
- Typed context survives GoBack (snapshot/restore) for all four types; `CreateCloneInstance` returns `StarterContext`.
- Parameter keys referenced in code come only from `StarterContextKeys`.
- Loading/reloading never deletes graph data; reloaded edges reconnect to ports.
