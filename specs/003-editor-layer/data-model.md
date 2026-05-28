# Data Model: GraphCore Editor Layer

**Branch**: `003-editor-layer` | **Date**: 2026-05-28 | **Plan**: [plan.md](plan.md)

This document covers the editor-layer types introduced by this feature. All types live in
the `com.faolline.graphcore.Editor` assembly (`Faolline.GraphCore.Editor` namespace).
No editor type is referenced by the Runtime assembly.

---

## Types Overview

| Type | Kind | Extends | Purpose |
|------|------|---------|---------|
| `BaseGraphView` | `abstract class` (partial) | `GraphView` | Canvas; routes Unity callbacks to hooks |
| `BaseNodeView` | `abstract class` | `Node` | Visual node; owns color resolution |
| `BaseEdgeView` | `abstract class` | `Edge` | Visual edge; owns color resolution |
| `BaseGraphEditorWindow` | `abstract class` | `EditorWindow` | Window host for `BaseGraphView` |
| `CycleDetector` | `static class` | — | DFS cycle detection on asset dependency graph |
| `CycleDetectionResult` | `readonly struct` | — | Result of a `CycleDetector.Check` call |
| `GraphClipboardData` | `[Serializable] class` | — | Intermediate clipboard model for copy/paste |
| `NodeTypeColorRegistry` | `static class` | — | Global map of node type → `Color` |

---

## BaseGraphView

```
BaseGraphView : GraphView (partial)
├── _graph : BaseGraph                      (loaded asset, nullable)
├── _nodeViews : Dictionary<string, BaseNodeView>  (nodeId → view)
├── _isDirty : bool                         (true when canvas differs from asset)
│
├── LoadGraph(graph : BaseGraph) : void
├── SaveGraph() : void
├── CreateNodeView(node : BaseNodeData) : BaseNodeView   (abstract)
├── CreateEdgeView(edge : BaseEdgeData) : BaseEdgeView   (abstract)
│
├── OnNodeCreated(node : BaseNodeData) : void    (protected virtual, hook)
├── OnEdgeConnected(edge : BaseEdgeData) : void  (protected virtual, hook)
├── OnNodeDeleted(node : BaseNodeData) : void    (protected virtual, hook)
│
└── [partial] BaseGraphView.CopyPaste.cs
    ├── SerializeGraphElements(elements) : string   (override)
    └── UnserializeAndPaste(data, offset) : void    (override)
```

**Invariants**:
- `_isDirty` is set to `true` on node/edge creation, deletion, or connection.
  It is **not** set on node position change (move).
- `SaveGraph()` syncs all node `Position` values from canvas to `_graph.Nodes`, then
  calls `EditorUtility.SetDirty(_graph)` + `AssetDatabase.SaveAssets()`.
- `graphViewChanged` delegate is intercepted in the constructor to route to the three
  virtual hooks. Subclasses that further override `graphViewChanged` **must** call `base`.

---

## BaseNodeView

```
BaseNodeView : Node
├── NodeData : BaseNodeData              (read-only, set in constructor)
│
├── HasColorOverride : bool              (protected virtual, default false)
├── ColorOverride : Color                (protected virtual, default Color.gray)
│
├── ResolveColor() : Color               (sealed — calls the resolution chain)
└── OnBuildView() : void                 (abstract — subclass populates content area)
```

**Color resolution** (executed in `ResolveColor()`):
1. If `HasColorOverride` → return `ColorOverride`
2. Else if `NodeTypeColorRegistry.TryGet(NodeData.NodeType, out var c)` → return `c`
3. Else → return `GraphCoreDefaults.NodeGrey` (`#808080`)

`ResolveColor()` is called once in the constructor and once in `LoadGraph` so that
registered colors from `[InitializeOnLoad]` libs are always applied after registration.

---

## BaseEdgeView

```
BaseEdgeView : Edge
├── EdgeData : BaseEdgeData              (read-only, set in constructor)
│
├── HasColorOverride : bool              (protected virtual, default false)
├── ColorOverride : Color                (protected virtual, default Color.gray)
│
└── ResolveColor() : Color               (sealed — same three-step chain as BaseNodeView)
```

**Color resolution**: identical chain to `BaseNodeView`, using `EdgeData.HasColorOverride`
and `EdgeData.EdgeColor` for step 1, `NodeTypeColorRegistry` for step 2, and
`GraphCoreDefaults.NodeGrey` for step 3.

---

## BaseGraphEditorWindow

```
BaseGraphEditorWindow : EditorWindow
├── _graphView : BaseGraphView           (private, created in OnEnable)
├── _graph : BaseGraph                   (private, set via LoadGraph)
│
├── GraphView : BaseGraphView            (protected read-only property)
├── CreateGraphView() : BaseGraphView    (protected abstract — subclass provides instance)
│
├── LoadGraph(graph : BaseGraph) : void  (protected)
└── OnEnable() / OnDisable()             (Unity lifecycle — creates/removes GraphView)
```

**Invariants**:
- `CreateGraphView()` is called exactly once per window instance, in `OnEnable`.
- The window toolbar provides a "Save" button that calls `_graphView.SaveGraph()`.

---

## CycleDetector

```
CycleDetector (static)
└── Check(root : BaseGraph, proposed : BaseGraph) : CycleDetectionResult
```

**Algorithm** (iterative DFS):
1. Treat `proposed` as already referenced by `root` (add `proposed.GraphId` to the
   initial adjacency set of `root`).
2. Starting from `proposed`, traverse all `SubGraphNodeData.TargetGraph` references.
3. Maintain a `HashSet<string> visited` and a `List<string> currentPath`.
4. If `root.GraphId` is encountered during traversal → cycle detected; return
   `CycleDetectionResult(true, currentPath)`.
5. If traversal completes without finding `root.GraphId` → no cycle;
   return `CycleDetectionResult(false, [])`.

**Edge cases**:
- `proposed == null` → return `CycleDetectionResult(false, [])` (no-op).
- `root == proposed` → return `CycleDetectionResult(true, [root.GraphId])` (self-cycle).
- `proposed` has no `SubGraphNodeData` nodes → return `CycleDetectionResult(false, [])`.

---

## CycleDetectionResult

```
CycleDetectionResult (readonly struct)
├── HasCycle : bool
└── CyclePath : IReadOnlyList<string>    (GraphId sequence, empty if no cycle)
```

Produced only by `CycleDetector.Check`. Consumed by `BaseGraphView.OnEdgeConnected`
to decide whether to refuse the connection and which error message to display.

---

## GraphClipboardData

```
GraphClipboardData ([Serializable])
├── Nodes : List<string>   (JSON-serialized BaseNodeData, one per node)
└── Edges : List<string>   (JSON-serialized BaseEdgeData for intra-selection edges only)
```

Used as the intermediate clipboard model in `SerializeGraphElements` (serializes to JSON
string) and `UnserializeAndPaste` (deserializes, then reassigns GUIDs before creating
new canvas elements).

**GUID reassignment contract** (enforced in `UnserializeAndPaste`):
1. Deserialize all `Nodes` → assign `Guid.NewGuid().ToString("D")` to each `BaseNodeData.Id`.
2. Build `oldIdToNewId : Dictionary<string, string>`.
3. Remap each `BaseEdgeData.FromNodeId` and `ToNodeId` via `oldIdToNewId`.
4. Assign `Guid.NewGuid().ToString("D")` to each `BaseEdgeData.Id`.
5. Add remapped nodes and edges to `_graph` and to the canvas.

---

## NodeTypeColorRegistry

```
NodeTypeColorRegistry (static)
├── _colors : Dictionary<string, Color>   (private static, initialized once)
│
├── Register(nodeType : string, color : Color) : void
├── TryGet(nodeType : string, out color : Color) : bool
└── Clear() : void   (test-only, clears all registrations)
```

**Thread safety**: Writes (`Register`) occur only in `[InitializeOnLoad]` static
constructors, which run on the main Unity thread before any `BaseNodeView` is constructed.
No lock is needed.

**Collision**: if `Register` is called twice with the same `nodeType`, the second call
replaces the first (same semantics as `NodeExecutorRegistry`).

---

## GraphCoreDefaults (static constants)

```
GraphCoreDefaults (static)
└── NodeGrey : Color   = new Color(0.502f, 0.502f, 0.502f)  // #808080
```

Single source of truth for the fallback color. Both `BaseNodeView.ResolveColor()` and
`BaseEdgeView.ResolveColor()` reference this constant.

---

## State Transitions

### BaseGraphView lifecycle

```
[No graph loaded]
    │ LoadGraph(graph)
    ▼
[Graph loaded — clean]
    │ User adds/deletes/connects node or edge
    ▼
[Graph loaded — dirty]   ←──── further edits
    │ SaveGraph()
    ▼
[Graph loaded — clean]
```

Node moves do **not** transition to dirty (FR-003). They are captured on `SaveGraph`
by reading current canvas positions.

### CycleDetector call flow

```
User drags edge between two ports
    │
    ▼ OnEdgeConnected (fires via graphViewChanged)
    │
    ▼ CycleDetector.Check(root=_graph, proposed=targetGraph)
    │
    ├── HasCycle = false → accept edge, add to _graph.Edges, set _isDirty
    │
    └── HasCycle = true  → remove edge from canvas, Debug.LogError("[GraphCore] Cycle: …")
```
