# Feature Specification: GraphCore Data Layer

**Feature Branch**: `001-data-layer`

**Created**: 2026-05-27

**Status**: Draft

**Input**: User description: "Je veux construire la couche de données de com.faolline.graphcore."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Define and Persist a Graph Asset (Priority: P1)

A game designer creates a new graph asset in the Unity Editor, adds nodes and edges,
configures conditions and actions on nodes, and saves the asset to disk. On reload,
the graph is fully restored with all its data intact.

**Why this priority**: Without a serializable graph container, no graph-based feature
in any ecosystem lib can function. This is the absolute foundation.

**Independent Test**: Create a `BaseGraph` ScriptableObject in an EditMode test,
populate it with all node/edge types, serialize it to disk, reload it, and assert
every field round-trips correctly. Delivers a fully usable graph asset on its own.

**Acceptance Scenarios**:

1. **Given** an empty `BaseGraph`, **When** `OnEnable` is called, **Then** `GraphId`
   is a non-empty, valid GUID string unique to this asset instance.
2. **Given** a `BaseGraph`, **When** nodes and edges are added and the asset is saved,
   **Then** all node/edge data survives a Unity domain reload without data loss.
3. **Given** a `BaseGraph`, **When** no `HistoryDepth` is set, **Then** it defaults to 20.
4. **Given** a `BaseGraph` with `HistoryDepth = 0`, **Then** history is treated as unlimited.
5. **Given** a `BaseGraph`, **When** `EntryNodeId` is set to a node's id, **Then** the
   graph knows its designated start point.

---

### User Story 2 - Nodes Carry Universal Lifecycle Hooks (Priority: P1)

Every node in any graph — regardless of type or downstream lib — automatically has
`EntryConditions`, `OnEnterActions`, and `OnExitActions` fields. A designer can attach
conditions and actions to any node without writing code.

**Why this priority**: This is the cross-cutting contract that makes the graph runtime
and every downstream lib work uniformly. Missing it breaks the entire execution model.

**Independent Test**: Instantiate each built-in node type and verify all three lifecycle
hook lists are present, non-null, serializable, and independently modifiable.

**Acceptance Scenarios**:

1. **Given** any `BaseNodeData` subclass, **When** inspected, **Then** `EntryConditions`,
   `OnEnterActions`, and `OnExitActions` are all accessible and non-null.
2. **Given** a `BaseNodeData` with an `OnEnterAction` list, **When** the asset is
   saved and reloaded, **Then** the action references survive serialization.
3. **Given** a node with `HasColorOverride = true` and a `NodeColor`, **When** the
   asset is reloaded, **Then** the color override is preserved.
4. **Given** a node with `IsCheckpoint = true`, **Then** it is persistently marked
   as a save point regardless of node type.

---

### User Story 3 - Built-in Node Types Cover All Graph Primitives (Priority: P2)

A designer building any graph-based system has access to start, statement, choice,
end, and sub-graph node types out of the box, each carrying their domain-specific data.

**Why this priority**: These concrete types give every ecosystem lib a common vocabulary.
They are needed before any lib can build on graphcore, but can be added after the
base data model is stable.

**Independent Test**: Instantiate each built-in type (`StartNodeData`, `StatementNodeData`,
`ChoiceNodeData`, `EndNodeData`, `SubGraphNodeData`), verify their unique fields are
present and serializable.

**Acceptance Scenarios**:

1. **Given** a `ChoiceNodeData`, **When** choices are added to its `Choices` list,
   **Then** each `BaseChoice` has a unique `Id` and an optional `Condition`.
2. **Given** an `EndNodeData`, **When** `EndReason` is set to `Cancelled`, **Then**
   the value is preserved after serialization.
3. **Given** a `SubGraphNodeData` with a `TargetGraph` reference and
   `InheritParentContext = true`, **Then** both fields survive a Unity reload.
4. **Given** any built-in node type, **When** a downstream lib subclasses it,
   **Then** all base fields are inherited without redeclaration.

---

### User Story 4 - Parameters Enable Graph-Level State (Priority: P2)

A designer declares typed parameters on a `BaseGraph` (Bool, Int, Float, String with
default values). At runtime these act as the graph's own variable store, readable and
writable by conditions and actions without any lib-specific code.

**Why this priority**: Parameters are needed for any condition that references graph
state (e.g., "has flag X been set?"). Required before the runtime can evaluate
conditions.

**Independent Test**: Add several `ParameterData` entries to a `BaseGraph`, save and
reload, verify keys, types, and default values are intact.

**Acceptance Scenarios**:

1. **Given** a `ParameterData` with `Key = "IsComplete"` and `Type = Bool`,
   **When** serialized, **Then** key, type, and `DefaultValue` round-trip exactly.
2. **Given** two `ParameterData` entries with the same key, **Then** the data layer
   does not prevent duplicates (enforcement is a runtime concern, not a data concern).
3. **Given** a `ParameterData` with `Type = Float` and `DefaultValue = "3.14"`,
   **Then** the string representation is preserved as-is in the data layer.

---

### Edge Cases

- A `BaseGraph` with an empty node list is valid and serializes without error.
- A `BaseEdgeData` with no `Condition` (null) is valid — condition is optional.
- A `BaseChoice` with no `Condition` (null) is valid — condition is optional.
- A `SubGraphNodeData` with `TargetGraph = null` is a valid (incomplete) state.
- `NodeColor` and `EdgeColor` are only meaningful when `HasColorOverride` is true;
  values when the flag is false are ignored but MUST still serialize cleanly.
- `SerializedPayload` may be an empty string or a valid JSON string; the data layer
  does not validate JSON — that is the responsibility of the consuming code.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `BaseGraph` MUST be a `ScriptableObject` with fields: `GraphId` (string GUID),
  `List<BaseNodeData>`, `List<BaseEdgeData>`, `List<ParameterData>`, `EntryNodeId` (string),
  and `HistoryDepth` (int, default 20).
- **FR-002**: `GraphId` MUST be assigned as a new GUID in `OnEnable` if it is null or empty,
  and MUST NOT be reassigned once set.
- **FR-003**: `BaseNodeData` MUST be serializable and MUST contain: `Id` (string GUID),
  `NodeType` (string), `Position` (Vector2), `SerializedPayload` (string),
  `List<BaseCondition> EntryConditions`, `List<BaseAction> OnEnterActions`,
  `List<BaseAction> OnExitActions`, `IsCheckpoint` (bool),
  `HasColorOverride` (bool), `NodeColor` (Color).
- **FR-004**: `BaseEdgeData` MUST contain: `Id` (string GUID), `FromNodeId` (string),
  `ToNodeId` (string), `PortName` (string), `Condition` (BaseCondition, nullable),
  `HasColorOverride` (bool), `EdgeColor` (Color).
- **FR-005**: `BaseChoice` MUST contain: `Id` (string GUID), `Condition` (BaseCondition, nullable).
  It MUST be subclassable by ecosystem libs.
- **FR-006**: `ParameterData` MUST contain: `Key` (string), `Type` (`ParameterType` enum with
  values `Bool`, `Int`, `Float`, `String`), `DefaultValue` (string).
- **FR-007**: Built-in concrete node types MUST exist: `StartNodeData`, `StatementNodeData`,
  `ChoiceNodeData` (with `List<BaseChoice> Choices`),
  `EndNodeData` (with `EndReason` enum: `Completed`, `Cancelled`, `Error`),
  `SubGraphNodeData` (with `BaseGraph TargetGraph` and `bool InheritParentContext`).
- **FR-008**: All built-in node types MUST be subclassable by ecosystem libs.
- **FR-009**: `BaseAction` MUST be an abstract `ScriptableObject` with an abstract
  `Execute(BaseContext)` method.
- **FR-010**: `BaseCondition` MUST be an abstract `ScriptableObject` with an abstract
  `Evaluate(BaseContext) → bool` method.
- **FR-011**: `BaseContext` MUST exist as a base class sufficient for `BaseAction.Execute`
  and `BaseCondition.Evaluate` to compile. It carries no semantic fields at the graphcore level.
- **FR-012**: All `NodeType` string identifiers for built-in types MUST be declared as
  `const string` fields on their respective classes — no magic strings.
- **FR-013**: The data layer MUST NOT reference any ecosystem lib (`dialoguesystem`,
  `gameflow`, `questsystem`, etc.) directly or transitively.
- **FR-014**: No `MonoBehaviour` MUST appear in the Runtime data layer.
  No `UnityEvent` — C# `Action<T>` only where event patterns are needed.

### Key Entities

- **BaseGraph**: The root container asset. Owns all nodes, edges, parameters, and
  graph-level metadata.
- **BaseNodeData**: Abstract base for all node data. Carries universal lifecycle hooks
  and visual metadata.
- **BaseEdgeData**: Represents a directed connection between two nodes, with optional
  condition gating.
- **BaseChoice**: An option within a `ChoiceNodeData`, optionally gated by a condition.
- **ParameterData**: A typed, named variable scoped to a single `BaseGraph`.
- **BaseAction**: Abstract executable unit attached to node lifecycle events.
- **BaseCondition**: Abstract boolean evaluator attached to edges, choices, or node entry.
- **BaseContext**: Minimal execution context passed to actions and conditions at runtime.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every field on every data type round-trips through Unity serialization
  without data loss (100% fidelity on save/reload).
- **SC-002**: All five built-in node types can be instantiated, populated, and
  serialized in an EditMode test with zero errors.
- **SC-003**: A downstream lib can subclass `BaseNodeData`, `BaseChoice`, `BaseAction`,
  and `BaseCondition` without modifying any graphcore file.
- **SC-004**: Zero references to ecosystem libs (`dialoguesystem`, `gameflow`,
  `questsystem`) exist in the data layer assembly.
- **SC-005**: All public types have XML `<summary>` documentation.
- **SC-006**: The data layer compiles with zero errors and zero warnings in a fresh
  Unity project that has no ecosystem libs installed.

## Assumptions

- `BaseContext` is a minimal placeholder at the graphcore level — ecosystem libs will
  subclass it to add their domain-specific runtime state (e.g., speaker, current quest).
- `SerializedPayload` is an opaque string from the data layer's perspective; validation
  and deserialization are the responsibility of node-type-specific editor/runtime code
  in downstream libs or graphcore's own editor layer.
- Unity's built-in `JsonUtility` or `Newtonsoft.Json` may be used by consumers of
  `SerializedPayload`, but the data layer itself does not depend on either.
- `BaseNodeData`, `BaseEdgeData`, and `BaseChoice` use `[System.Serializable]` and
  Unity's `SerializeReference` where polymorphism is required for serialization.
- `BaseAction` and `BaseCondition` as `ScriptableObject` assets are referenced by
  the node/edge data via plain `UnityEngine.Object` asset references (not `[SerializeReference]`,
  which is reserved for managed non-Unity objects). See research.md Decision 1.
- The data layer targets Unity 6.0 or later (Unity 6000.x). All Unity APIs used
  must be available and non-deprecated in Unity 6.
- `HistoryDepth` is stored as data but its enforcement is the responsibility of the
  runtime layer, not the data layer.

## Clarifications

### Session 2026-05-27

- Q: What Unity version does the project target? → A: Unity 6.0 or later (Unity 6000.x), not Unity 2022.3.
