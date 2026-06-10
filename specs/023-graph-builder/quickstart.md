# Quickstart — building graphs in code

## Build a graph fluently (graphstandard)

```csharp
using Faolline.GraphStandard;

var b = new GraphBuilder<GameFlowGraph>();
var start = b.AddStart().AsEntry();
var room  = b.AddStatement("Load Room").OnEnter(loadRoomAction).Wait(2f);   // timed intro
var lever = b.AddStatement("Await lever").Await("lever");
var exit  = b.AddStatement("Await exit").Await("exit");
var end   = b.AddEnd("Done");

start.To(room); room.To(lever); lever.To(exit); exit.To(end);
GameFlowGraph graph = b.Build();      // ready to assign to a GraphFlowDriver or persist
```

No GUID ids, no `AddNode`/`AddEdge`. `AddX` returns a node handle; `OnEnter`/`When`/`Await`/`Wait`/
`Checkpoint`/`Choice` configure it; `To` / `Edge` wire it; `Build()` returns the typed graph.

**Choices**: `var c = b.AddChoice("Branch"); c.Choice("Win", winCond); c.Choice("Lose"); b.Edge(c, winNode, "Win");`
— wire a choice edge by its title.

## Persist it as an asset with sub-asset actions (editor)

```csharp
using Faolline.GraphStandard.Editor;

GraphAssetBuilder.Save(graph, "Assets/MyGame/EscapeFlow.asset");
// the LoadScene actions (and any conditions) are stored as sub-assets → the asset is self-contained
```

## Looping game-shell (a supported pattern)

A game shell never "ends" — model it as a **cyclic Linear graph with no End node**:

```
Start → MenuWait(await "start") → … → WinLoad(await "menu") → MenuReturn(load Menu) ─┐
  └──────────────────────────── back to MenuWait ───────────────────────────────────┘
```

The runner follows the single outgoing edge on each advance and loops forever; the flow stays running (no
`OnEnded`). For a forever-looping shell, set a small `BaseGraph.HistoryDepth` (GoBack across the loop isn't
meaningful).

## Drive a synced countdown over a timed node (gameflow)

```csharp
var flow = GraphFlowDriver.Active;
if (flow.IsWaitingForTime)
    countdownLabel.text = $"{flow.WaitRemaining:0.0}s / {flow.WaitTotal:0.0}s";
```

Symmetric with `IsWaitingForSignal` / `CurrentAwaitSignal`.
