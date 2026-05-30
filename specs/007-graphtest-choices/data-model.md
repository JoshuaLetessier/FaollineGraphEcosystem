# Data Model: GraphTest — Choices & ChooseById

**Feature**: `007-graphtest-choices` | **Date**: 2026-05-29

---

## New Entities

### TestChoice

Inherits `Id` and `Condition` from `BaseChoice` (graphcore). Adds one field.

| Field | Type | Serialized | Notes |
|-------|------|-----------|-------|
| (inherited) `Id` | `string` (GUID) | ✅ | Assigned on creation; used as the output port's `portName` for routing |
| (inherited) `Condition` | `BaseCondition` | ✅ | Optional; null = always available |
| `_label` | `string` | ✅ | Human-readable choice text; public `Label { get; set; }` |

`[Serializable]`. Stored polymorphically in `ChoiceNodeData.Choices` via `[SerializeReference]`.

---

## Reused Entities (graphcore — no change)

### ChoiceNodeData

| Member | Type | Notes |
|--------|------|-------|
| `NodeTypeId` | `const string` | `"graphcore/choice"` |
| `Choices` | `List<BaseChoice>` | `[SerializeReference]`; holds `TestChoice` instances |

### BaseRunner.ChooseById(string id)

Selects the outgoing edge whose `Id` or `PortName` equals `id`, bypassing condition evaluation, then advances. Used with `choice.Id` as the argument.

---

## New / Modified Editor Entities

### ChoiceNodeView *(new)*

| Aspect | Detail |
|--------|--------|
| Base | `BaseNodeView` |
| Input port | one `"in"`, `Port.Capacity.Multi`, typed `TestEdgeView` |
| Output ports | one per choice; `portName = choice.Id`; displayed label = `choice.Label` |
| `RebuildPorts()` | clears and regenerates output ports from the node's `Choices` list |

### TestNodeInspectorView *(modified)*

New behaviour when the selected node is a `ChoiceNodeData`:
- "Add Choice" button → appends a `TestChoice` (new GUID, default label), triggers `ChoiceNodeView.RebuildPorts()`
- Per-choice row: label `TextField`, condition `ObjectField<BaseCondition>`, Remove button
- Remove → drops the choice from `Choices`, rebuilds ports, removes any edge bound to that choice's port

### TestGraphEditorWindow *(modified)*

| Field | Type | Notes |
|-------|------|-------|
| `_waitingForChoice` | `bool` | True while execution is paused at a choice node |
| `_waitingChoiceNode` | `ChoiceNodeData` | The node awaiting selection |

New public methods: `Choose(string choiceId)`, and the Choose toolbar button handler that lists condition-passing choices.

---

## Relationships

```
ChoiceNodeData
 └── Choices: List<BaseChoice>   ([SerializeReference], holds TestChoice)
       └── each TestChoice
             ├── Id        → output port portName → BaseEdgeData.PortName → ChooseById(Id)
             ├── Label     → displayed port text + inspector field
             └── Condition → filters availability at runtime (null = always)

ChoiceNodeData (view: ChoiceNodeView)
 └── output port[i].portName == Choices[i].Id
       └── edge from port[i] → target node   (PortName == Choices[i].Id)
```

**Invariants**:
- Each choice's output port `portName` equals that choice's `Id`.
- Removing a choice removes its port and any edge whose `PortName` matches the choice `Id`.
- At runtime, only choices with a passing (or null) condition are offered; selecting one routes via `ChooseById(choice.Id)`.
