# Phase 1 — Data Model: gameflow editor authoring

New types live in `com.faolline.graphgameflow` (runtime namespace `Faolline.GraphGameFlow`, editor namespace
`Faolline.GraphGameFlow.Editor`). graphcore types are subclassed, never modified.

## GameFlowGraph (Runtime) : BaseGraph

The creatable gameflow graph asset; what the editor window targets and the driver runs.

| Member | Kind | Description |
|--------|------|-------------|
| *(none added)* | — | Trivial subclass; behavior is `BaseGraph`'s. |
| class attribute | `[CreateAssetMenu(menuName = "GraphGameFlow/Game Flow Graph", fileName = "NewGameFlowGraph")]` | Makes it appear under Assets ▸ Create ▸ GraphGameFlow. |

## LoadSceneAction (Runtime) — MODIFIED

Add `[CreateAssetMenu(menuName = "GraphGameFlow/Actions/Load Scene", fileName = "NewLoadSceneAction")]`. No
behavior change.

## GameFlowGraphEditorWindow (Editor) : BaseGraphEditorWindow

| Member | Kind | Description |
|--------|------|-------------|
| `Open()` | `[MenuItem("Faolline/Open GraphGameFlow Editor")]` static | Opens an empty window. |
| `OnOpenAsset(int, int)` | `[OnOpenAsset]` static | Opens/focuses a window for a double-clicked `GameFlowGraph`. |
| `CreateGraphView()` | override | `new GameFlowGraphView()`. |
| `CreateNodeInspectorView()` | override | `new GameFlowNodeInspectorView()` (split-pane layout). |
| `OnGraphLoaded(BaseGraph)` | override | Wire the inspector's graph + graph-view references. |
| `PopulateToolbar(Toolbar)` | override | Adds a **Validate** button (`GraphValidator`); Save is provided by the base. |

## GameFlowGraphView (Editor) : BaseGraphView

| Member | Kind | Description |
|--------|------|-------------|
| `CreateNodeView(BaseNodeData)` | override | switch on `NodeType` → the matching gameflow node view (Start/Statement/Choice/SubGraph/End); `_ => null`. |
| `CreateEdgeView(BaseEdgeData)` | override | `new GameFlowEdgeView(edge)`. |
| `OnNodeCreated(BaseNodeData)` | override | designate the first `StartNodeData` as `EntryNodeId` (mirror starter). |
| `BuildContextualMenu(...)` | override | right-click "Add Start/Statement/Choice/SubGraph/End Node" at the cursor. |
| Choice helpers | methods | `GetChoiceView`, `RemoveChoiceEdges` — mirrored from starter for choice-port rebuilds. |

## Node views (Editor) — mirror starterGraph

`StartNodeView`, `StatementNodeView`, `ChoiceNodeView`, `SubGraphNodeView`, `EndNodeView` — each a
`BaseNodeView` subclass over the corresponding universal node type, styled via the shared USS (no inline CSS).
`GameFlowEdgeView : BaseEdgeView`.

## GameFlowNodeInspectorView (Editor) : BaseNodeInspectorView

| Section | Source | Description |
|---------|--------|-------------|
| Node Properties | `AddBaseNodeSection` (graphcore) | title, checkpoint, color, entry conditions, **on-enter / on-exit actions** (← drop a `LoadSceneAction` here). |
| **Flow** | NEW (gameflow) | bound PropertyFields for `_awaitSignal` (await-signal name) and `_waitDuration` (timed wait). |
| End | mirror starter | `EndReason` enum field (for `EndNodeData`). |
| SubGraph | mirror starter | target-graph object field (cycle-checked) + inherit-context toggle (for `SubGraphNodeData`). |
| Choices | mirror starter | per-choice label + condition + add/remove (for `ChoiceNodeData`). |
| `SetGraph` / `SetGraphView` | methods | wired in `OnGraphLoaded`, as starter does. |

## GameFlowSampleBuilder (Editor)

`[MenuItem("Faolline/GraphGameFlow/Create Reference Scene-Flow Sample")]` static. Builds:

```
GameFlowGraph (unique asset path)
  StartNode("start")  [EntryNodeId]
    → Statement("Load A")  OnEnterActions: LoadSceneAction{ SceneName="A", Single }   (sub-asset)
      → Statement("Wait")  AwaitSignalName = "advance"
        → Statement("Load B")  OnEnterActions: LoadSceneAction{ SceneName="B", Single } (sub-asset)
          → EndNode("end", Completed)
```

Saves, pings the asset. Helpers mirror starter (`NewId` GUID, `Edge`, `Sub<T>` via `AddObjectToAsset`).

## Validation / invariants

- **INV-1**: `GameFlowGraph` is assignable to `GraphFlowDriver` unchanged (IS-A `BaseGraph`).
- **INV-2**: `GameFlowGraph` and `LoadSceneAction` each carry `[CreateAssetMenu]`.
- **INV-3**: The sample graph has exactly: 1 start, 3 statements (Load A / Wait / Load B), 1 end; 4 edges; the
  Wait node's `AwaitSignalName == "advance"`; the two Load nodes each carry one `LoadSceneAction` (A, B).
- **INV-4**: Driving the sample with a `GraphFlowDriver` + recording loader records A, parks on the await,
  then records B and ends after `RaiseSignal("advance")`.
- **INV-5**: graphcore/graphstandard untouched; slice-1 runtime untouched; 654 EditMode + 8 PlayMode stay
  green; gameflow 0.1.0 → 0.2.0.
