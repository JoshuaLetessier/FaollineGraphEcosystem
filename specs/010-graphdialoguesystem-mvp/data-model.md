# Data Model: graphdialoguesystem (MVP)

**Feature**: `010-graphdialoguesystem-mvp` | **Date**: 2026-05-31

All entities are new and live in `com.faolline.graphdialoguesystem`. They extend graphcore base types
(no graphcore change). Namespaces: runtime `Faolline.GraphDialogue`, editor `Faolline.GraphDialogue.Editor`,
optional adapter `Faolline.GraphDialogue.Localization.Unity`.

## Reused from graphcore (unchanged)

`BaseGraph`, `StartNodeData`, `ChoiceNodeData`, `EndNodeData` (+ `EndReason`), `SubGraphNodeData`,
`BaseNodeData` (Id, NodeType, Position, color, IsCheckpoint, EntryConditions, OnEnterActions,
OnExitActions), `BaseEdgeData` (Id, FromNodeId, ToNodeId, PortName, Condition), `BaseChoice`
(Id, Condition), `BaseCondition`, `BaseAction`, `BaseContext`, `ParameterData`/`ParameterType`,
`BaseRunner`, `INodeExecutor`, `NodeExecutorRegistry`, `RunnerState`, `GraphCycleException`, and the
editor bases `BaseGraphView`, `BaseNodeView`, `BaseEdgeView`, `BaseNodeInspectorView`,
`BaseGraphEditorWindow`, `CycleDetector`.

## Runtime

### DialogueGraph
`BaseGraph` subclass with `[CreateAssetMenu(menuName = "GraphDialogue/Dialogue Graph")]`. Holds nodes,
edges, parameters, `EntryNodeId`, `HistoryDepth` (all inherited). No new fields.

### DialogueContext / DialogueContextKeys (Principle VI)
- `DialogueContext : BaseContext` — typed example properties over the blackboard: `Flag` (bool),
  `Counter` (int), `Amount` (float), `Tag` (string), each via `TryGet<T>`/`Set<T>` and a key from
  `DialogueContextKeys`. Overrides `CreateCloneInstance()` → `new DialogueContext()`.
- `DialogueContextKeys` — `static class` with one `const string` per key (the only place the literals
  live). Conditions/actions reference these consts; no raw literals at call sites.

### DialogueLineNodeData
`StatementNodeData` subclass. `const string NodeTypeId = "graphdialogue/line"`.

| Field | Type | Notes |
|-------|------|-------|
| `SpeakerKey` | `string` | Logical speaker id (matches a `Speaker.SpeakerId`); not translated |
| `TextKey` | `string` | Localization key for the spoken line |
| `ExpressionKey` | `string` | Speaker expression key; default `"neutral"` |

### DialogueChoice
`BaseChoice` subclass. Inherits `Id` (GUID, used as the output port routing key) and `Condition`.

| Field | Type | Notes |
|-------|------|-------|
| `DisplayTextKey` | `string` | Localization key for the choice label |

### Speaker / SpeakerExpression
- `Speaker : ScriptableObject`, `[CreateAssetMenu(menuName = "GraphDialogue/Speaker")]`.

| Field | Type | Notes |
|-------|------|-------|
| `SpeakerId` | `string` | Logical id referenced by `DialogueLineNodeData.SpeakerKey` |
| `DisplayNameKey` | `string` | Localization key for the display name |
| `DisplayNameFallback` | `string` | Literal fallback when key unresolved |
| `Expressions` | `List<SpeakerExpression>` | key → presentation asset |
| `FallbackExpression` | `UnityEngine.Object` | used when an expression key is unknown |

  Method: `bool TryGetExpression(string key, out UnityEngine.Object asset)` — null-safe, falls back.

- `SpeakerExpression` (`[Serializable]`): `Key : string`, `Asset : UnityEngine.Object` (prefab/sprite).

### Conditions (`BaseCondition` subclasses, `[CreateAssetMenu]` under `GraphDialogue/Conditions`)

| Type | Fields | Evaluate |
|------|--------|----------|
| `AlwaysTrueCondition` | — | true |
| `AlwaysFalseCondition` | — | false |
| `BoolCondition` | `ParameterKey`, `ExpectedValue: bool` | `TryGet<bool>` == expected; false+warn if absent |
| `IntCondition` | `ParameterKey`, `Operator`, `ExpectedValue: int` | compare; false+warn if absent/mistyped |
| `FloatCondition` | `ParameterKey`, `Operator`, `ExpectedValue: float` | compare; false+warn if absent/mistyped |
| `StringCondition` | `ParameterKey`, `ExpectedValue: string`, `Negate: bool` | (in)equality; false+warn if absent/mistyped |

`ComparisonOperator` (enum): `Equal, NotEqual, Less, LessOrEqual, Greater, GreaterOrEqual`.

### Actions (`BaseAction` subclasses, `[CreateAssetMenu]` under `GraphDialogue/Actions`)

| Type | Fields | Execute |
|------|--------|---------|
| `LogAction` | `Message` | logs the message with `[GraphDialogue]` prefix |
| `SetBoolAction` | `ParameterKey`, `Value: bool` | `Set<bool>` |
| `SetIntAction` | `ParameterKey`, `Value: int` | `Set<int>` |
| `SetFloatAction` | `ParameterKey`, `Value: float` | `Set<float>` |
| `SetStringAction` | `ParameterKey`, `Value: string` | `Set<string>` |

### Execution

- `DialogueLineExecutor : INodeExecutor` — `NodeType => DialogueLineNodeData.NodeTypeId`. On `Execute`,
  records/exposes the current line's speaker + resolved text for the player to emit (no side effects on
  the blackboard beyond what enter-actions do). `Undo` = default no-op.
- `DialogueExecutorRegistryFactory` — static helper building a `NodeExecutorRegistry` with the line
  executor registered (extensible later).

### Playback

- `DialoguePlayer` — wraps a `BaseRunner`. Constructed with a `DialogueGraph`, a `DialogueContext`, an
  `ILocalizationProvider`, and a speaker lookup (id → `Speaker`). Methods: `Start()`, `Advance()`
  (→ `Proceed`), `Choose(string choiceId)` (→ `ChooseById`), `Back()` (→ `GoBack`),
  `BackToCheckpoint()` (→ `GoBackToCheckpoint`). Events: `OnLine`, `OnChoices`, `OnEnded`, `OnStuck`.
- `DialogueStep` (abstract: `NodeId`) → `LineStep`, `ChoiceStep`, `EndStep`.

| Step | Fields |
|------|--------|
| `LineStep` | `NodeId`, `SpeakerId`, `ResolvedSpeakerName`, `ResolvedText`, `ExpressionKey` |
| `ChoiceStep` | `NodeId`, `IReadOnlyList<ChoiceOption> Options` |
| `EndStep` | `NodeId`, `EndReason` |

- `ChoiceOption` — `ChoiceId`, `ResolvedLabel`, `Available` (bool: condition passes against context).

### Localization

- `ILocalizationProvider` — `string Resolve(string key, string locale)`; `string CurrentLocale { get; }`;
  resolving an absent key returns a defined fallback and logs `[GraphDialogue]` (never empty).
- `CsvLocalizationProvider : ILocalizationProvider` — built from CSV text (`Key,locale,…`); in-memory
  `key → locale → text`. No external dependency.
- `LocalizationSettings` — selects the active `ILocalizationProvider` + current locale; exposes a safe
  default provider when unconfigured.
- `LocalizationContext` (static ambient accessor) — current provider for resolution helpers; defaults
  safely.
- `UnityLocalizationProvider : ILocalizationProvider` — **optional adapter assembly**; resolves through
  `com.unity.localization` String Tables. Compiled only when the package is present.

## Editor

### Node views (`BaseNodeView` subclasses)
- `StartNodeView` — one `out`.
- `EndNodeView` — one `in`.
- `DialogueLineNodeView` — `in` + `out`; shows speaker + (key/preview) text in the body.
- `ChoiceNodeView` — one `in`; one output per choice, `portName = choice.Id`, displayed label = choice
  label/key; `RebuildPorts()` / `UpdateChoiceLabel()` (mirrors starterGraph).
- `SubGraphNodeView` — `in` + `out`; shows target graph name.

### DialogueEdgeView
`BaseEdgeView` subclass — typed edge used in all port declarations.

### DialogueGraphView (`BaseGraphView`)
`CreateNodeView` dispatch for the five node types + `DialogueEdgeView`; context menu adds Start, Line,
Choice, SubGraph, End; `GetChoiceView`, `RemoveChoiceEdges`; inherits `ReconnectNodeEdges` + LoadGraph
data-safety; `OnNodeCreated` auto-sets entry node on first Start.

### DialogueNodeInspectorView (`BaseNodeInspectorView`)
Sections: line (SpeakerKey field + TextKey field + ExpressionKey) for `DialogueLineNodeData`; choice
(add/remove/label(DisplayTextKey)/condition + live ports + reconnect) for `ChoiceNodeData`; EndReason
`EnumField` for `EndNodeData`; sub-graph (`ObjectField<BaseGraph>` target with cycle refusal + inherit
`Toggle`) for `SubGraphNodeData`; typed parameter panel (key + `ParameterType` + default); shared
base-node section (checkpoint, color, entry conditions, enter/exit actions).

### DialogueGraphEditorWindow (`BaseGraphEditorWindow`)
Toolbar Run / Choose / Continue / GoBack / GoBackToCheckpoint; drain loop pausing at `ChoiceNodeData`;
`ChooseById`; per-asset multi-window via `OnOpenAsset` (focus existing or `CreateWindow`, titled by
asset name). Run uses a `DialogueContext` + the line executor registry; logs resolved lines.

### DialogueSampleBuilder (editor menu)
Generates a parent + child `DialogueGraph` exercising lines with speakers, a choice with a gated option,
inline conditions/actions, a checkpoint, a sub-dialogue, typed parameters, and a small CSV table for two
locales.

## Invariants

- Each choice output port `portName` == choice `Id`; routing via `ChooseById(choice.Id)`.
- Sub-graph `TargetGraph` cycles refused at edit time (`CycleDetector`); runtime cycle →
  `GraphCycleException`.
- Typed context survives GoBack (snapshot/restore) for all four types; `CreateCloneInstance` returns
  `DialogueContext`.
- Context keys referenced in code come only from `DialogueContextKeys`.
- Loading/reloading never deletes graph data; reloaded edges reconnect to ports.
- Text resolution never yields empty/broken output: a missing key returns the defined fallback +
  warning.
- Runtime core compiles and runs with **no** `com.unity.localization`; the Unity adapter is isolated.
