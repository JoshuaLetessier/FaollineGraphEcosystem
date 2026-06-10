# Public API Contract — code-first graph ergonomics (graphstandard 0.4.0 / gameflow 0.4.0)

## graphstandard Runtime — the fluent builder

```csharp
namespace Faolline.GraphStandard
{
    public sealed class GraphBuilder<TGraph> where TGraph : BaseGraph, new()
    {
        public GraphNodeBuilder AddStart(string title = null);
        public GraphNodeBuilder AddStatement(string title = null);
        public GraphNodeBuilder AddChoice(string title = null);
        public GraphNodeBuilder AddSubGraph(string title = null, BaseGraph target = null);
        public GraphNodeBuilder AddEnd(string title = null, EndReason reason = EndReason.Completed);

        public GraphBuilder<TGraph> Edge(GraphNodeBuilder from, GraphNodeBuilder to, string portName = "out");
        public TGraph Build();
    }

    public sealed class GraphNodeBuilder
    {
        public BaseNodeData Node { get; }
        public GraphNodeBuilder Title(string title);
        public GraphNodeBuilder At(float x, float y);
        public GraphNodeBuilder At(Vector2 position);
        public GraphNodeBuilder OnEnter(params BaseAction[] actions);
        public GraphNodeBuilder OnExit(params BaseAction[] actions);
        public GraphNodeBuilder When(params BaseCondition[] entryConditions);
        public GraphNodeBuilder Await(string signalName);
        public GraphNodeBuilder Wait(float seconds);
        public GraphNodeBuilder Checkpoint(bool value = true);
        public GraphNodeBuilder Choice(string title, BaseCondition condition = null);
        public GraphNodeBuilder AsEntry();
        public GraphNodeBuilder To(GraphNodeBuilder target, string portName = "out");
    }
}
```

**Example** (the escape-room flow, code-first):

```csharp
var b = new GraphBuilder<GameFlowGraph>();
var start = b.AddStart("Start").AsEntry();
var loadA = b.AddStatement("Load Room").OnEnter(loadRoom).Wait(2f);
var lever = b.AddStatement("Await lever").Await("lever");
var exit  = b.AddStatement("Await exit").Await("exit");
var win   = b.AddStatement("Win").OnEnter(loadWin).Await("menu");
start.To(loadA); loadA.To(lever); lever.To(exit); exit.To(win); win.To(start); // looping shell, no End
GameFlowGraph graph = b.Build();
```

**Contract**: ids are auto-GUID; positions auto-column unless `At`; entry is `AsEntry()` or the first Start.
`Edge` from a Choice node resolves a `portName` matching a choice **title** to that choice's id; unknown →
`[GraphStandard]` log + literal. The builder adds no runtime behavior — a built graph runs exactly as the
hand-assembled equivalent. An edge to an unknown node throws; `Build` with no resolvable entry leaves
`EntryNodeId` empty (the same invalid state a hand-built graph would have — caught by the runner/validator).

## graphstandard Editor — the persist utility

```csharp
namespace Faolline.GraphStandard.Editor
{
    public static class GraphAssetBuilder
    {
        // Writes `graph` to `path` and stores its nodes' enter/exit actions, entry conditions, and choice
        // conditions as SUB-ASSETS (only those not already persisted). Returns the saved graph.
        public static BaseGraph Save(BaseGraph graph, string path);
    }
}
```

## gameflow driver — the time-wait query (additive)

```csharp
public sealed class GraphFlowDriver : MonoBehaviour
{
    // … slice 1–3 members unchanged …
    public bool  IsWaitingForTime { get; }   // running && parked on a timed node
    public float WaitRemaining    { get; }   // seconds left while time-waiting, else 0 (never negative)
    public float WaitTotal        { get; }   // the timed node's duration while time-waiting, else 0
}
```

**Contract**: symmetric with `IsWaitingForSignal`/`CurrentAwaitSignal`. Computed driver-side from
`OnWaitingForTime` + accumulated `Tick`; no graphcore change. False/0 before boot, after end, and off a timed
node.

## Semver / compatibility

- graphstandard **0.3.0 → 0.4.0** (MINOR, additive: the builder + a new Editor assembly).
- gameflow **0.3.0 → 0.4.0** (MINOR, additive: the driver time query).
- graphcore **untouched** (code + README). All prior APIs unchanged; 661 EditMode + 9 PlayMode stay green.
