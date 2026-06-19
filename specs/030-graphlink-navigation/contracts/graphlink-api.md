# Public API & Behavioural Contracts — GraphLink

## 1. `Faolline.GraphCore.GraphLinkNodeData` (Runtime)

```csharp
public sealed class GraphLinkNodeData : BaseNodeData
{
    public const string NodeTypeId = "graphcore/graph-link";
    public BaseGraph TargetGraph { get; set; }   // serialized; may be null
    public string    Note        { get; set; }   // optional author label
}
```

**Contract**: a `GraphLinkNodeData` is a non-executing annotation. It has no runtime side effects.

## 2. Runtime contract — `BaseRunner` (modified)

- A `GraphLinkNodeData` is **never executed**: its `OnEnter`/`OnExit` actions, executor, and `TargetGraph`
  are not touched by the runner.
- **Off the execution path** (not wired into any edge): never entered — fully inert.
- **On the execution path** (wired in): entering it is an immediate **pass-through** — the runner advances
  to its single outgoing edge without entering `NodeReady` (no pause), without raising any event, without
  throwing. With no outgoing edge it ends like a dead-end (`Ended` / `EndReason.Completed`).
- **Invariant**: for any graph, a run is observably identical whether GraphLink nodes are present off-path or
  absent. A graph with a GraphLink wired on-path completes with the same terminal outcome as the same graph
  with that node spliced out.

## 3. Editor navigation — `Faolline.GraphCore.Editor.GraphEditorWindowRegistry`

```csharp
public static class GraphEditorWindowRegistry
{
    public static void Register(Type graphType, Action<BaseGraph> opener);
    public static bool TryGetOpener(Type graphType, out Action<BaseGraph> opener);
    public static void Open(BaseGraph graph);   // resolve + invoke, or graceful fallback
    public static void Clear();                  // test hook
}
```

**Contract**:
- `Register` associates a concrete graph type with the action that opens that graph in its window. Intended to
  be called from a downstream lib editor in an `[InitializeOnLoadMethod]`. Null `graphType`/`opener` is ignored
  with a `[GraphCore]` warning.
- `Open(graph)` resolves an opener for `graph.GetType()` (walking base types) and invokes it. If `graph` is
  null or no opener is registered, it falls back to `Selection.activeObject = graph` +
  `EditorGUIUtility.PingObject(graph)` and logs a clear `[GraphCore]` diagnostic. **Never throws.**
- graphcore registers nothing itself and names no downstream type — the libs populate the registry.

## 4. Editor view — `GraphLinkNodeView`

- Renders distinctly from executable nodes (USS), showing `"<Kind>: <Name>"` (Kind derived generically from the
  target graph type name, Name from its display/asset name) or a clear "(missing target)" label when null.
- **Double-click** (`MouseDownEvent`, `clickCount == 2`) → `GraphEditorWindowRegistry.Open(node.TargetGraph)`.

## 5. Downstream registration (one line per lib editor)

```csharp
// e.g. in com.faolline.graphquest/Editor
[InitializeOnLoadMethod]
static void RegisterEditorWindow() =>
    GraphEditorWindowRegistry.Register(typeof(QuestGraph), g => /* open the Quest editor window with g */ );
```

## 6. Versioning

- graphcore: **MINOR** (new built-in node type + new public editor APIs).
- graphquest / graphdialoguesystem / graphgameflow: **PATCH** (one registration line each), graphcore floor
  aligned to the new graphcore version per the floor-alignment convention.
