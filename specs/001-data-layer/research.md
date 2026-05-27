# Research: GraphCore Data Layer

## Decision 1: Unity Serialization Strategy for Polymorphic Types

**Decision**: Use `[SerializeReference]` for managed-type polymorphic lists; use plain
`UnityEngine.Object` asset references for `ScriptableObject` fields.

**Rationale**:
- `BaseNodeData`, `BaseEdgeData`, and `BaseChoice` are `[System.Serializable]` plain C#
  classes (not ScriptableObjects). When stored in a `List<T>` on a ScriptableObject,
  Unity's default serializer loses subclass data unless `[SerializeReference]` is used.
  `[SerializeReference]` (introduced Unity 2019.3, stable in Unity 2020.1+) stores a
  managed reference with full type metadata, enabling polymorphic deserialization.
- `BaseAction` and `BaseCondition` ARE ScriptableObjects (`UnityEngine.Object` subclasses).
  Unity already serializes `UnityEngine.Object` references polymorphically — it stores the
  asset's GUID and fileID, not the C# type. `[SerializeReference]` would be incorrect here
  (it is not for UnityEngine.Object types). Standard fields (no attribute needed) suffice.

**Fields requiring `[SerializeReference]`**:
- `BaseGraph.Nodes` → `List<BaseNodeData>` (subtypes: Start, Statement, Choice, End, SubGraph)
- `BaseGraph.Edges` → `List<BaseEdgeData>` (extensible in future; apply now at no cost)
- `ChoiceNodeData.Choices` → `List<BaseChoice>` (libs subclass BaseChoice)

**Fields using plain `UnityEngine.Object` references**:
- `BaseNodeData.EntryConditions` → `List<BaseCondition>` (ScriptableObject assets)
- `BaseNodeData.OnEnterActions` → `List<BaseAction>` (ScriptableObject assets)
- `BaseNodeData.OnExitActions` → `List<BaseAction>` (ScriptableObject assets)
- `BaseEdgeData.Condition` → `BaseCondition` (nullable ScriptableObject asset)
- `BaseChoice.Condition` → `BaseCondition` (nullable ScriptableObject asset)
- `SubGraphNodeData.TargetGraph` → `BaseGraph` (ScriptableObject asset)

**Alternatives considered**:
- *JSON manual serialization*: Rejected — fragile, bypasses Unity's undo/redo and asset
  pipeline. Violates Principle V (simpler option is the native Unity mechanism).
- *ScriptableObject for BaseNodeData*: Rejected — each node would need its own .asset file.
  Hundreds of nodes per graph would produce hundreds of loose assets. `[Serializable]` embedded
  in the graph asset is far simpler to manage.

---

## Decision 2: BaseContext as a Zero-Field Placeholder

**Decision**: `BaseContext` is declared as `public abstract class BaseContext` with no fields
at the graphcore level.

**Rationale**: Graphcore encodes universals only (Principle II). A context that carries
speaker info (dialoguesystem), quest progress (questsystem), or game state (gameflow) is
domain-specific. The data layer only needs `BaseContext` to exist so `BaseAction.Execute`
and `BaseCondition.Evaluate` have a compilable signature. Each downstream lib subclasses
`BaseContext` to add its domain state.

**Alternatives considered**:
- *Adding common fields (graphId, currentNodeId) to BaseContext*: Deferred — these are
  runtime concerns, not data layer concerns. They belong in a `BaseRunner` class to be
  defined in a future runtime feature.
- *Generic `BaseAction<TContext>`*: Rejected — generics on ScriptableObjects interact poorly
  with Unity's serialization system (no generic ScriptableObject support pre-Unity 6).

---

## Decision 3: NodeType Const String Namespace Convention

**Decision**: Each concrete node type declares `public const string NodeTypeId` with a
`"graphcore/"` prefix (e.g., `"graphcore/start"`, `"graphcore/statement"`).

**Rationale**: The `NodeTypeId` const string on each class is the single source of truth
for type identity (FR-012, no magic strings). The `"graphcore/"` prefix prevents collision
with lib-defined types (`"dialoguesystem/line"`, `"questsystem/objective"`, etc.).
Using `const string` on the class means the value lives with its type — no lookup tables,
no registries, no runtime allocation.

**Alternatives considered**:
- *Enum*: Rejected — not extensible by downstream libs without modifying graphcore.
- *Type.FullName as NodeType*: Rejected — brittle under refactoring; renaming a class would
  corrupt serialized data that matches on the string.
- *Central registry*: Rejected — adds indirection with no benefit; violates Principle V.

---

## Decision 4: Unity Version and Assembly Structure

**Decision**: Target Unity 6.0 (Unity 6000.x). Runtime assembly only for this feature;
no Editor assembly (editor UI is a separate future feature).

**Assembly layout**:
- `Runtime/com.faolline.graphcore.Runtime.asmdef`
  - References: none (Unity Engine assemblies are implicit)
  - Platforms: Any
- `Tests/EditMode/com.faolline.graphcore.Tests.EditMode.asmdef`
  - References: `com.faolline.graphcore.Runtime`, `UnityEngine.TestRunner`,
    `UnityEditor.TestRunner`
  - Test platforms: EditMode only

**Rationale**: Unity 6.0 is the version in active use on this project. `[SerializeReference]`
is mature and fully production-ready in Unity 6. `package.json` must declare
`"unity": "6000.0"`. EditMode tests suffice for pure data serialization tests
(no Play mode lifecycle involved). The `BaseRunner` is headless — PlayMode tests
are never required per the constitution.

**Alternatives considered**:
- *Unity 2022.3 LTS*: Rejected — project is on Unity 6.0; targeting an older version
  would create false constraints.
- *PlayMode tests*: Rejected — constitution explicitly forbids them for core. EditMode
  covers all data layer needs.

---

## Decision 5: GraphId Guard — Assign Once, Never Reassign

**Decision**: `GraphId` is backed by `[SerializeField] private string _graphId`. `OnEnable`
assigns a new GUID only if `_graphId` is null or empty. The public property has only a getter.

**Rationale**: Unity calls `OnEnable` every time the ScriptableObject is loaded (domain
reload, import, play mode enter). The null/empty guard ensures the id is set on first
activation and then locked — removing any risk of id churn across reloads.

**Format**: `System.Guid.NewGuid().ToString("D")` — lowercase with hyphens,
e.g. `"550e8400-e29b-41d4-a716-446655440000"`. Consistent with Unity's own GUID practices.

**Alternatives considered**:
- *`[InitializeOnLoad]` static initializer*: Rejected — fires on editor load only, not
  when a ScriptableObject asset is first created at runtime.
- *`Reset()` method*: Rejected — `Reset()` is called by the Inspector "Reset" button and
  could re-assign the id unintentionally on existing assets.
