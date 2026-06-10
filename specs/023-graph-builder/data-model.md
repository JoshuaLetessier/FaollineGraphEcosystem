# Phase 1 — Data Model: code-first graph ergonomics

New types in `com.faolline.graphstandard` (namespace `Faolline.GraphStandard`) + additive members on
gameflow's driver. graphcore types are used, never modified.

## GraphBuilder&lt;TGraph&gt; (Runtime) — where TGraph : BaseGraph, new()

| Member | Returns | Description |
|--------|---------|-------------|
| `AddStart(string title = null)` | `GraphNodeBuilder` | adds a `StartNodeData` |
| `AddStatement(string title = null)` | `GraphNodeBuilder` | adds a `StatementNodeData` |
| `AddChoice(string title = null)` | `GraphNodeBuilder` | adds a `ChoiceNodeData` |
| `AddSubGraph(string title = null, BaseGraph target = null)` | `GraphNodeBuilder` | adds a `SubGraphNodeData` (target set) |
| `AddEnd(string title = null, EndReason reason = Completed)` | `GraphNodeBuilder` | adds an `EndNodeData` |
| `Edge(GraphNodeBuilder from, GraphNodeBuilder to, string portName = "out")` | `GraphBuilder<TGraph>` | adds an edge; for a Choice `from`, a `portName` matching a choice **title** is resolved to that choice's id |
| `Build()` | `TGraph` | assembles a fresh `TGraph`: adds nodes + edges, sets `EntryNodeId` (explicit `AsEntry`, else first Start) |

Ids auto-`Guid`; `Position` auto-assigned by add order when not set via `At`.

## GraphNodeBuilder (Runtime) — fluent node handle

Wraps the created `BaseNodeData`; every setter returns the same handle.

| Member | Description |
|--------|-------------|
| `Title(string)` | sets `BaseNodeData.Title` |
| `At(float x, float y)` / `At(Vector2)` | sets `Position` |
| `OnEnter(params BaseAction[])` | appends to `OnEnterActions` |
| `OnExit(params BaseAction[])` | appends to `OnExitActions` |
| `When(params BaseCondition[])` | appends to `EntryConditions` |
| `Await(string signalName)` | sets `AwaitSignalName` |
| `Wait(float seconds)` | sets `WaitDuration` |
| `Checkpoint(bool = true)` | sets `IsCheckpoint` |
| `Choice(string title, BaseCondition condition = null)` | adds a `BaseChoice` (id = Guid, Title = title) to a `ChoiceNodeData` |
| `AsEntry()` | designates this node as the graph entry |
| `To(GraphNodeBuilder target, string portName = "out")` | sugar for `builder.Edge(this, target, portName)` |
| `Node` | the underlying `BaseNodeData` (escape hatch) |

## GraphAssetBuilder (Editor, graphstandard) — static

| Member | Description |
|--------|-------------|
| `Save(BaseGraph graph, string path)` → `BaseGraph` | `CreateAsset(graph, path)`; `AddObjectToAsset` each node's enter/exit actions, entry conditions, and choice conditions that are not already assets; `SaveAssets`; returns the saved graph |

## GraphFlowDriver (gameflow) — added members

| Member | Description |
|--------|-------------|
| `IsWaitingForTime` (read-only) | running && `Runner.State == WaitingForTime` |
| `WaitRemaining` (read-only) | seconds left while time-waiting (`max(0, total − elapsed)`), else 0 |
| `WaitTotal` (read-only) | the timed node's duration while time-waiting, else 0 |

Internals: `OnWaitingForTime(node, duration)` → `_waitTotal = duration`, `_waitElapsed = 0`; `Tick(dt)` adds
`dt` to `_waitElapsed` while time-waiting.

## Validation / invariants

- **INV-1**: A graph built by `GraphBuilder` has exactly the added nodes (right types, titles), edges, entry
  node, and per-node await/wait/checkpoint/actions/conditions/choices.
- **INV-2**: The built graph's `Build()` result is of the requested `TGraph` type.
- **INV-3**: A built graph runs identically to a hand-assembled equivalent under a runner (builder adds no
  behavior).
- **INV-4**: `Edge` to an unknown node, or a graph with no entry, surfaces clearly (no silent half-graph).
- **INV-5**: `GraphAssetBuilder.Save` writes a graph asset whose attached actions/conditions are sub-assets;
  reloading yields them intact; an action already an asset is not double-added.
- **INV-6**: `IsWaitingForTime`/`WaitRemaining`/`WaitTotal` report the timed wait while parked, count down to
  0 as `Tick` is fed, never go negative, and are false/0 before boot, after end, and off a timed node.
- **INV-7**: graphcore untouched (code + README); 661 EditMode + 9 PlayMode stay green; graphstandard 0.4.0 +
  gameflow 0.4.0.
