# Data Model: GraphTest Verification Package

**Feature**: `005-graphtest-package` | **Date**: 2026-05-29

---

## Entities

### TestGraph

Inherits all fields from `BaseGraph` (graphcore). Adds only a Unity `[CreateAssetMenu]` attribute so the asset type is accessible from the Project window.

| Field | Type | Source | Notes |
|-------|------|--------|-------|
| `graphId` | `string` (GUID) | BaseGraph | Assigned once in `OnEnable`; never changed |
| `nodes` | `List<BaseNodeData>` | BaseGraph | `[SerializeReference]` — preserves polymorphism |
| `edges` | `List<BaseEdgeData>` | BaseGraph | `[SerializeReference]` |
| `parameters` | `List<ParameterData>` | BaseGraph | Typed context parameters |
| `entryNodeId` | `string` | BaseGraph | Id of the `StartNodeData` node |
| `historyDepth` | `int` | BaseGraph | Default 20; 0 = unlimited |

**Validation**: Every saved graph must have exactly one node with `NodeType == StartNodeData.NodeTypeId` for execution to succeed. Validation is advisory (logged error on Run) — the asset itself is always saveable.

---

### TestStatementNodeData

Inherits all fields from `StatementNodeData` → `BaseNodeData`. Adds one domain-specific field.

| Field | Type | Serialized | Notes |
|-------|------|-----------|-------|
| (all BaseNodeData fields) | — | ✅ | id, nodeType, position, conditions, actions, checkpoint, color |
| `_label` | `string` | ✅ | Public `Label { get; set; }`. Displayed and editable in the inspector panel. Defaults to empty string. |

**NodeTypeId**: `"graphtest/statement"` (distinct from `graphcore/statement` to avoid ambiguity)

**State transitions**: None — statement nodes have no branching; one input port `"in"`, one output port `"out"`.

---

### StartNodeData *(reused from graphcore)*

No custom fields. Used directly. `NodeTypeId = "graphcore/start"`.

---

### EndNodeData *(reused from graphcore)*

No custom fields. Used directly. `NodeTypeId = "graphcore/end"`.

---

### BaseEdgeData *(reused from graphcore)*

Used without subclassing. Fields sufficient for test package:

| Field | Type | Notes |
|-------|------|-------|
| `id` | `string` (GUID) | |
| `fromNodeId` | `string` | Output node |
| `toNodeId` | `string` | Input node |
| `portName` | `string` | Output port name |
| `condition` | `BaseCondition` | Optional; null = always traversable |

---

## Relationships

```
TestGraph
 ├── nodes: List<BaseNodeData>
 │    ├── [0..1] StartNodeData
 │    ├── [0..N] TestStatementNodeData
 │    └── [0..1] EndNodeData
 └── edges: List<BaseEdgeData>
      └── each edge: FromNodeId → ToNodeId (directed)
```

**Invariants**:
- A valid executable graph has exactly one `StartNodeData` and at least one `EndNodeData`.
- Edges are directed; cycles are rejected at draw time by `CycleDetector`.
- Each `BaseEdgeData` references nodes by `Id` (GUID); referential integrity is enforced by the editor (deleting a node removes its edges).

---

## Editor Visual Mapping

| Data Type | View Type | Port Layout |
|-----------|-----------|-------------|
| `StartNodeData` | `StartNodeView` | 0 inputs, 1 output (`"out"`) |
| `TestStatementNodeData` | `TestStatementNodeView` | 1 input (`"in"`), 1 output (`"out"`) |
| `EndNodeData` | `EndNodeView` | 1 input (`"in"`), 0 outputs |
| `BaseEdgeData` | `TestEdgeView` | (edge between any two compatible ports) |

All ports are typed `Port.Create<TestEdgeView>(...)` to ensure `HandleEdgeCreation` receives `BaseEdgeView` instances.

---

## Assembly References

| Assembly | References |
|----------|-----------|
| `com.faolline.graphTest.Runtime` | `com.faolline.graphcore.Runtime` |
| `com.faolline.graphTest.Editor` | `com.faolline.graphcore.Runtime`, `com.faolline.graphcore.Editor` |
| `com.faolline.graphTest.Tests.EditMode` | `com.faolline.graphTest.Runtime`, `com.faolline.graphTest.Editor`, `com.faolline.graphcore.Runtime`, `com.faolline.graphcore.Editor` |
