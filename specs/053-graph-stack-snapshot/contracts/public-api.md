# Public API contract — 053

Additions only. No existing member's signature changes. Names follow the D5 candidates (adopted provisionally; renamable before merge).

## com.faolline.graphcore 0.44.0 (`Faolline.GraphCore`, assembly `com.faolline.graphcore.Runtime`)

```csharp
/// How a frame holds its values relative to the frame that entered it.
public enum GraphRunContextMode { Shared = 0, Scoped = 1, Fresh = 2 }

/// Immutable description of one execution-stack level.
public sealed class GraphRunFrame
{
    public GraphRunFrame(string graphId, string nodeId, GraphRunContextMode contextMode = GraphRunContextMode.Shared);
    public string GraphId { get; }
    public string NodeId { get; }
    public GraphRunContextMode ContextMode { get; }
}

public enum GraphRunPathErrorReason
{
    NullRootGraph, EmptyPath, RootGraphMismatch, RepeatedGraph, NodeMissing,
    NotASubGraphNode, SubGraphTargetMissing, SubGraphTargetMismatch, ContextModeMismatch
}

public sealed class GraphRunPathError
{
    public GraphRunPathErrorReason Reason { get; }
    public int FrameIndex { get; }               // -1 for EmptyPath
    public int DeepestValidFrameIndex { get; }   // -1 when no prefix is valid
    public string Message { get; }
    public override string ToString();           // = Message
}

public sealed class GraphRunPathException : InvalidOperationException
{
    public GraphRunPathError Error { get; }
}

public static class GraphRunPath
{
    /// Pure check: no state anywhere is modified. graphs = resolved BaseGraph per frame (root first) on success.
    public static bool TryResolve(BaseGraph rootGraph, IReadOnlyList<GraphRunFrame> path,
                                  out IReadOnlyList<BaseGraph> graphs, out GraphRunPathError error);
    public static bool TryResolve(BaseGraph rootGraph, IReadOnlyList<GraphRunFrame> path,
                                  out GraphRunPathError error);
}

public class BaseRunner
{
    // NEW
    /// Active levels, root first, deepest last. Empty before Start. Never throws.
    public IReadOnlyList<GraphRunFrame> GetExecutionPath();

    /// Rebuilds the whole stack from rootGraph down the saved path and re-enters ONLY the deepest frame's node.
    /// Throws ArgumentNullException (root/path/context null) or GraphRunPathException (path does not match the
    /// content) BEFORE modifying anything.
    public void StartFromPath(BaseGraph rootGraph, IReadOnlyList<GraphRunFrame> path,
                              BaseContext context, NodeExecutorRegistry registry);
}
```

## com.faolline.graphsave 0.11.0 (`Faolline.GraphSave`)

```csharp
[Serializable]
public class GraphRunSnapshot
{
    // existing: GraphId, CurrentNodeId (deepest frame), Variables, Collections, RaisedSignals,
    //           Capture(context, graphId, nodeId), Capture(runner, context), ApplyTo, Restore

    [Serializable]
    public class Frame
    {
        public string GraphId;
        public string NodeId;
        public GraphRunContextMode ContextMode;
    }

    /// NEW — execution path, root first. Empty for a snapshot without a stack.
    public List<Frame> Frames = new List<Frame>();

    /// NEW
    public bool HasStack { get; }          // Frames non-empty
    public string RootGraphId { get; }     // Frames[0].GraphId, else GraphId

    /// NEW — pure check against the root graph the caller will restore into.
    /// No stack: true for any non-null graph (nothing to check, as before).
    public bool TryValidate(BaseGraph rootGraph, out GraphRunPathError error);

    /// NEW — Frames mapped to graphcore frames (empty when no stack).
    public IReadOnlyList<GraphRunFrame> GetExecutionPath();
}
```

Behavior changes on existing members (signatures unchanged):

| Member | Before | After |
|---|---|---|
| `Capture(BaseRunner, BaseContext)` | top frame ids only | + `Frames` from `runner.GetExecutionPath()`; logs a `[GraphSave]` error (never throws) for each lossy fresh frame (D1-A) |
| `Restore(BaseRunner, BaseGraph, BaseContext, NodeExecutorRegistry)` | `ApplyTo` + `StartFrom(graph, CurrentNodeId)` | No stack: unchanged. Stack: `graph` is the ROOT; `TryValidate` first. On failure logs a `[GraphSave]` error and changes nothing; otherwise `ApplyTo` + `StartFromPath`. |
| `Capture(BaseContext, string, string)` | unchanged | unchanged (no stack) |

## com.faolline.graphgameflow 0.20.0 (`Faolline.GraphGameFlow`)

No new member. Behavior of an existing one:

| Member | Before | After |
|---|---|---|
| `GraphFlowDriver.Boot(GraphRunSnapshot, GameFlowContext, NodeExecutorRegistry)` | `ApplyTo` + `StartFrom(Graph, CurrentNodeId)` | No stack: unchanged. Stack: `Graph` is the ROOT; `snapshot.TryValidate(Graph)` before any context/runner is created. Mismatch logs a `[GraphGameFlow]` error and the driver stays inert, with the supplied context untouched. Otherwise `ApplyTo` + `StartFromPath`. |

`IGraphCatalog` (doc only): resolve `snapshot.RootGraphId`; only root graphs need to be resolvable.

## com.faolline.graphdialoguesystem 0.20.1

Documentation only: `DialoguePlayer.SaveState`/`RestoreFrom` and `DialogueSessionState` state that they restore a single level (the deepest node into the player's own graph), and point to `BaseRunner.GetExecutionPath`/`StartFromPath` as the way to lift that later (D4-A).
