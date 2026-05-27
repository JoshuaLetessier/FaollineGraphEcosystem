# Data Model: GraphCore Data Layer

## Overview

All types in this model live in the `Faolline.GraphCore` namespace inside the
`com.faolline.graphcore.Runtime` assembly. All serializable non-ScriptableObject
types use `[System.Serializable]`. Polymorphic list fields use `[SerializeReference]`.

---

## BaseGraph

**Kind**: `ScriptableObject`
**Namespace**: `Faolline.GraphCore`
**File**: `Runtime/Graph/BaseGraph.cs`

| Field | Type | Serialization | Notes |
|-------|------|---------------|-------|
| `_graphId` | `string` | `[SerializeField]` | Assigned once in `OnEnable` via `Guid.NewGuid().ToString("D")`. Public getter `GraphId` only. |
| `_nodes` | `List<BaseNodeData>` | `[SerializeReference]` | Polymorphic — stores any BaseNodeData subtype. |
| `_edges` | `List<BaseEdgeData>` | `[SerializeReference]` | Polymorphic — future-proofed. |
| `_parameters` | `List<ParameterData>` | `[SerializeField]` | Value-type list, no polymorphism needed. |
| `_entryNodeId` | `string` | `[SerializeField]` | Id of the designated start node. |
| `_historyDepth` | `int` | `[SerializeField]` | Default: `20`. `0` = unlimited. |

**Invariants**:
- `GraphId` is set once in `OnEnable` if null/empty; never overwritten.
- `HistoryDepth` defaults to `20` (field initializer).
- Lists are initialized to empty (not null) on construction.

**Public API**:
```
string GraphId { get; }
IReadOnlyList<BaseNodeData> Nodes { get; }
IReadOnlyList<BaseEdgeData> Edges { get; }
IReadOnlyList<ParameterData> Parameters { get; }
string EntryNodeId { get; set; }
int HistoryDepth { get; set; }
```

---

## BaseNodeData

**Kind**: `[System.Serializable]` abstract class
**Namespace**: `Faolline.GraphCore`
**File**: `Runtime/Nodes/BaseNodeData.cs`

| Field | Type | Serialization | Notes |
|-------|------|---------------|-------|
| `Id` | `string` | `[SerializeField]` | GUID string. Assigned by whoever creates the node. |
| `NodeType` | `string` | `[SerializeField]` | Set to the subclass's `NodeTypeId` const at construction. |
| `Position` | `Vector2` | `[SerializeField]` | Editor canvas position. |
| `SerializedPayload` | `string` | `[SerializeField]` | Opaque JSON string. Data layer does not validate. |
| `EntryConditions` | `List<BaseCondition>` | `[SerializeField]` | ScriptableObject asset refs. Non-null, may be empty. |
| `OnEnterActions` | `List<BaseAction>` | `[SerializeField]` | ScriptableObject asset refs. Non-null, may be empty. |
| `OnExitActions` | `List<BaseAction>` | `[SerializeField]` | ScriptableObject asset refs. Non-null, may be empty. |
| `IsCheckpoint` | `bool` | `[SerializeField]` | Marks node as a save point. |
| `HasColorOverride` | `bool` | `[SerializeField]` | When false, `NodeColor` is ignored. |
| `NodeColor` | `Color` | `[SerializeField]` | Editor display color. Only meaningful when `HasColorOverride` is true. |

**Invariants**:
- `EntryConditions`, `OnEnterActions`, `OnExitActions` initialized to `new List<T>()` (never null).
- Subclasses MUST set `NodeType` to their `NodeTypeId` const in their constructor.

---

## BaseEdgeData

**Kind**: `[System.Serializable]` class
**Namespace**: `Faolline.GraphCore`
**File**: `Runtime/Edges/BaseEdgeData.cs`

| Field | Type | Serialization | Notes |
|-------|------|---------------|-------|
| `Id` | `string` | `[SerializeField]` | GUID string. |
| `FromNodeId` | `string` | `[SerializeField]` | Source node id. |
| `ToNodeId` | `string` | `[SerializeField]` | Target node id. |
| `PortName` | `string` | `[SerializeField]` | Output port identifier on the source node. |
| `Condition` | `BaseCondition` | `[SerializeField]` | Nullable ScriptableObject asset ref. |
| `HasColorOverride` | `bool` | `[SerializeField]` | |
| `EdgeColor` | `Color` | `[SerializeField]` | Only meaningful when `HasColorOverride` is true. |

---

## BaseChoice

**Kind**: `[System.Serializable]` abstract class
**Namespace**: `Faolline.GraphCore`
**File**: `Runtime/Choices/BaseChoice.cs`

| Field | Type | Serialization | Notes |
|-------|------|---------------|-------|
| `Id` | `string` | `[SerializeField]` | GUID string. |
| `Condition` | `BaseCondition` | `[SerializeField]` | Nullable ScriptableObject asset ref. |

**Extension point**: Libs subclass `BaseChoice` to add domain fields
(e.g., `DialogueChoice` adds `LocalizedText`).

---

## ParameterData

**Kind**: `[System.Serializable]` class
**Namespace**: `Faolline.GraphCore`
**File**: `Runtime/Parameters/ParameterData.cs`

| Field | Type | Serialization | Notes |
|-------|------|---------------|-------|
| `Key` | `string` | `[SerializeField]` | Variable name. Uniqueness enforced by runtime, not data layer. |
| `Type` | `ParameterType` | `[SerializeField]` | Enum: `Bool`, `Int`, `Float`, `String`. |
| `DefaultValue` | `string` | `[SerializeField]` | String representation of the typed default value. |

### ParameterType Enum

**File**: `Runtime/Parameters/ParameterType.cs`

```
Bool   = 0
Int    = 1
Float  = 2
String = 3
```

---

## BaseAction

**Kind**: `ScriptableObject` abstract class
**Namespace**: `Faolline.GraphCore`
**File**: `Runtime/Actions/BaseAction.cs`

| Member | Signature | Notes |
|--------|-----------|-------|
| `Execute` | `public abstract void Execute(BaseContext context)` | Called by the runtime at node enter/exit. |

**Extension**: Libs subclass `BaseAction` to implement domain behavior.

---

## BaseCondition

**Kind**: `ScriptableObject` abstract class
**Namespace**: `Faolline.GraphCore`
**File**: `Runtime/Conditions/BaseCondition.cs`

| Member | Signature | Notes |
|--------|-----------|-------|
| `Evaluate` | `public abstract bool Evaluate(BaseContext context)` | Returns true if the condition passes. |

---

## BaseContext

**Kind**: Abstract class (not a ScriptableObject — instantiated at runtime)
**Namespace**: `Faolline.GraphCore`
**File**: `Runtime/Graph/BaseContext.cs`

No fields at the graphcore level. Exists solely so `BaseAction.Execute` and
`BaseCondition.Evaluate` have a compilable, subclassable context parameter.

---

## Built-in Node Types

All built-in types are concrete subclasses of `BaseNodeData`, all in `Faolline.GraphCore`.

### StartNodeData

**File**: `Runtime/Nodes/StartNodeData.cs`

```
public const string NodeTypeId = "graphcore/start";
```

No additional fields. Marks the graph's entry point.

---

### StatementNodeData

**File**: `Runtime/Nodes/StatementNodeData.cs`

```
public const string NodeTypeId = "graphcore/statement";
```

No additional fields beyond `BaseNodeData`. Content is expressed via `SerializedPayload`.

---

### ChoiceNodeData

**File**: `Runtime/Nodes/ChoiceNodeData.cs`

```
public const string NodeTypeId = "graphcore/choice";
```

| Field | Type | Serialization | Notes |
|-------|------|---------------|-------|
| `Choices` | `List<BaseChoice>` | `[SerializeReference]` | Polymorphic — libs use subclasses of BaseChoice. Non-null. |

---

### EndNodeData

**File**: `Runtime/Nodes/EndNodeData.cs`

```
public const string NodeTypeId = "graphcore/end";
```

| Field | Type | Serialization | Notes |
|-------|------|---------------|-------|
| `EndReason` | `EndReason` | `[SerializeField]` | Default: `Completed`. |

### EndReason Enum

**File**: `Runtime/Nodes/EndReason.cs`

```
Completed = 0
Cancelled = 1
Error     = 2
```

---

### SubGraphNodeData

**File**: `Runtime/Nodes/SubGraphNodeData.cs`

```
public const string NodeTypeId = "graphcore/subgraph";
```

| Field | Type | Serialization | Notes |
|-------|------|---------------|-------|
| `TargetGraph` | `BaseGraph` | `[SerializeField]` | ScriptableObject asset reference. Nullable (incomplete state). |
| `InheritParentContext` | `bool` | `[SerializeField]` | When true, the sub-graph execution receives the parent context. |

---

## Type Relationships

```
BaseGraph (ScriptableObject)
  ├── Nodes: List<BaseNodeData>           [SerializeReference]
  │     ├── StartNodeData
  │     ├── StatementNodeData
  │     ├── ChoiceNodeData
  │     │     └── Choices: List<BaseChoice> [SerializeReference]
  │     │           └── BaseChoice (extensible by libs)
  │     ├── EndNodeData
  │     └── SubGraphNodeData
  │           └── TargetGraph: BaseGraph   (asset ref)
  ├── Edges: List<BaseEdgeData>           [SerializeReference]
  │     └── Condition: BaseCondition      (asset ref, nullable)
  └── Parameters: List<ParameterData>

BaseNodeData (all subtypes)
  ├── EntryConditions: List<BaseCondition> (asset refs)
  ├── OnEnterActions:  List<BaseAction>    (asset refs)
  └── OnExitActions:   List<BaseAction>    (asset refs)

BaseAction  (ScriptableObject) → Execute(BaseContext)
BaseCondition (ScriptableObject) → Evaluate(BaseContext) → bool
BaseContext (abstract class, no fields at graphcore level)
```
