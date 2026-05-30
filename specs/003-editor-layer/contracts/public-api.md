# Public API: GraphCore Editor Layer

**Branch**: `003-editor-layer` | **Date**: 2026-05-28 | **Plan**: [../plan.md](../plan.md)

This document specifies the public C# interface surface of the
`com.faolline.graphcore.Editor` assembly. All types are in the
`Faolline.GraphCore.Editor` namespace unless noted.

Semver impact: **MINOR** (0.2.0 → 0.3.0) — new public API; no existing public API
is removed or broken.

---

## BaseGraphView

```csharp
namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Abstract canvas base class for GraphCore graph editors.
    /// Extend this class to create a custom node graph view for a downstream lib.
    /// Do not override <c>graphViewChanged</c> without calling <c>base.graphViewChanged</c>.
    /// </summary>
    public abstract partial class BaseGraphView : GraphView
    {
        // ── Lifecycle ─────────────────────────────────────────────────────────

        /// <summary>
        /// Loads a graph asset onto the canvas. Clears any existing visual elements
        /// before rebuilding from <paramref name="graph"/>.
        /// </summary>
        public void LoadGraph(BaseGraph graph);

        /// <summary>
        /// Syncs all canvas node positions to <c>BaseNodeData.Position</c>, then
        /// marks the asset dirty and saves it. No-op if no graph is loaded.
        /// </summary>
        public void SaveGraph();

        // ── Abstract factory methods ──────────────────────────────────────────

        /// <summary>
        /// Create and return a <see cref="BaseNodeView"/> for the given node data.
        /// Called once per node during <see cref="LoadGraph"/>.
        /// </summary>
        protected abstract BaseNodeView CreateNodeView(BaseNodeData node);

        /// <summary>
        /// Create and return a <see cref="BaseEdgeView"/> for the given edge data.
        /// Called once per edge during <see cref="LoadGraph"/>.
        /// </summary>
        protected abstract BaseEdgeView CreateEdgeView(BaseEdgeData edge);

        // ── Hooks (override in downstream lib subclasses) ─────────────────────

        /// <summary>
        /// Called after a new node has been added to the canvas and to the graph data.
        /// Override to react to node creation without replacing base behavior.
        /// </summary>
        protected virtual void OnNodeCreated(BaseNodeData node) { }

        /// <summary>
        /// Called after a new edge has been accepted and added to the canvas and graph data.
        /// <see cref="CycleDetector"/> has already approved the connection when this fires.
        /// Override to react to edge connection without replacing base behavior.
        /// </summary>
        protected virtual void OnEdgeConnected(BaseEdgeData edge) { }

        /// <summary>
        /// Called after a node has been removed from the canvas and graph data.
        /// Override to react to node deletion without replacing base behavior.
        /// </summary>
        protected virtual void OnNodeDeleted(BaseNodeData node) { }
    }
}
```

---

## BaseNodeView

```csharp
namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Abstract base for the visual representation of a <see cref="BaseNodeData"/>.
    /// Implement <see cref="OnBuildView"/> to populate the node content area.
    /// Override <see cref="HasColorOverride"/> and <see cref="ColorOverride"/> to
    /// apply a custom node background color.
    /// </summary>
    public abstract class BaseNodeView : Node
    {
        /// <summary>The data object this view represents. Never null after construction.</summary>
        public BaseNodeData NodeData { get; protected set; }

        /// <summary>
        /// When <c>true</c>, <see cref="ColorOverride"/> is used as the node background color.
        /// Default: <c>false</c>.
        /// </summary>
        protected virtual bool HasColorOverride => false;

        /// <summary>
        /// The background color applied when <see cref="HasColorOverride"/> is <c>true</c>.
        /// Default: <c>Color.gray</c>.
        /// </summary>
        protected virtual Color ColorOverride => Color.gray;

        /// <summary>
        /// Resolves the node background color using the three-step chain:
        /// override → lib type color → graphcore default grey.
        /// Sealed — do not override.
        /// </summary>
        public sealed Color ResolveColor();

        /// <summary>
        /// Called during construction after the base node chrome is built.
        /// Add custom UI elements (labels, fields, ports) here.
        /// </summary>
        protected abstract void OnBuildView();
    }
}
```

---

## BaseEdgeView

```csharp
namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Abstract base for the visual representation of a <see cref="BaseEdgeData"/> connection.
    /// Override <see cref="HasColorOverride"/> and <see cref="ColorOverride"/> to apply a
    /// custom edge color. Color resolution follows the same three-step chain as
    /// <see cref="BaseNodeView"/>.
    /// </summary>
    public abstract class BaseEdgeView : Edge
    {
        /// <summary>The data object this view represents. Never null after construction.</summary>
        public BaseEdgeData EdgeData { get; protected set; }

        /// <summary>
        /// When <c>true</c>, <see cref="ColorOverride"/> is used as the edge color.
        /// Default: <c>false</c>.
        /// </summary>
        protected virtual bool HasColorOverride => false;

        /// <summary>
        /// The color applied when <see cref="HasColorOverride"/> is <c>true</c>.
        /// Default: <c>Color.gray</c>.
        /// </summary>
        protected virtual Color ColorOverride => Color.gray;

        /// <summary>
        /// Resolves the edge color using the three-step chain:
        /// override → lib type color → graphcore default grey.
        /// Sealed — do not override.
        /// </summary>
        public sealed Color ResolveColor();
    }
}
```

---

## BaseGraphEditorWindow

```csharp
namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Abstract base for Unity <see cref="EditorWindow"/> instances that host a
    /// <see cref="BaseGraphView"/> canvas. Implement <see cref="CreateGraphView"/> to
    /// supply the concrete view type for the downstream lib.
    /// </summary>
    public abstract class BaseGraphEditorWindow : EditorWindow
    {
        /// <summary>The canvas hosted in this window. Available after <c>OnEnable</c>.</summary>
        protected BaseGraphView GraphView { get; private set; }

        /// <summary>
        /// Called once in <c>OnEnable</c>. Return the concrete <see cref="BaseGraphView"/>
        /// subclass for this window.
        /// </summary>
        protected abstract BaseGraphView CreateGraphView();

        /// <summary>
        /// Loads <paramref name="graph"/> into <see cref="GraphView"/>. Safe to call
        /// before <c>OnEnable</c> completes — load is deferred to the next frame if needed.
        /// </summary>
        protected void LoadGraph(BaseGraph graph);
    }
}
```

---

## CycleDetector

```csharp
namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Stateless utility that detects cycles in the <see cref="BaseGraph"/> asset
    /// dependency graph by performing an iterative DFS over
    /// <see cref="SubGraphNodeData.TargetGraph"/> references.
    /// </summary>
    public static class CycleDetector
    {
        /// <summary>
        /// Checks whether adding a reference from <paramref name="root"/> to
        /// <paramref name="proposed"/> would create a cycle in the dependency graph.
        /// </summary>
        /// <param name="root">The graph that would contain the new SubGraph reference.</param>
        /// <param name="proposed">The graph that would be referenced by the new SubGraph node.</param>
        /// <returns>
        /// A <see cref="CycleDetectionResult"/> indicating whether a cycle was found
        /// and, if so, the sequence of GraphIds that form the cycle.
        /// </returns>
        public static CycleDetectionResult Check(BaseGraph root, BaseGraph proposed);
    }

    /// <summary>
    /// Immutable result of a <see cref="CycleDetector.Check"/> call.
    /// </summary>
    public readonly struct CycleDetectionResult
    {
        /// <summary><c>true</c> if the proposed connection would form a cycle.</summary>
        public bool HasCycle { get; }

        /// <summary>
        /// The sequence of <see cref="BaseGraph.GraphId"/> values that form the cycle,
        /// in DFS traversal order. Empty when <see cref="HasCycle"/> is <c>false</c>.
        /// </summary>
        public IReadOnlyList<string> CyclePath { get; }
    }
}
```

---

## NodeTypeColorRegistry

```csharp
namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Global registry mapping node type strings to display colors.
    /// Downstream libs register colors in <c>[InitializeOnLoad]</c> static constructors.
    /// </summary>
    public static class NodeTypeColorRegistry
    {
        /// <summary>
        /// Registers a display <paramref name="color"/> for the given <paramref name="nodeType"/>.
        /// If the type is already registered, the new color replaces the existing one.
        /// </summary>
        public static void Register(string nodeType, Color color);

        /// <summary>
        /// Attempts to retrieve the registered color for <paramref name="nodeType"/>.
        /// </summary>
        /// <returns><c>true</c> if a color is registered; <c>false</c> otherwise.</returns>
        public static bool TryGet(string nodeType, out Color color);

        /// <summary>
        /// Removes all registered colors. Intended for use in tests only.
        /// </summary>
        public static void Clear();
    }
}
```

---

## GraphCoreDefaults

```csharp
namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Shared constants for the GraphCore editor layer.
    /// </summary>
    public static class GraphCoreDefaults
    {
        /// <summary>Fallback node/edge color when no override or type color is registered.</summary>
        public static readonly Color NodeGrey = new Color(0.502f, 0.502f, 0.502f); // #808080
    }
}
```

---

## Assembly Definition

```json
// Editor/com.faolline.graphcore.Editor.asmdef
{
    "name": "com.faolline.graphcore.Editor",
    "rootNamespace": "Faolline.GraphCore.Editor",
    "references": [
        "com.faolline.graphcore.Runtime"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

**Note**: The assembly is `includePlatforms: ["Editor"]` — it is never compiled into
runtime builds. It references `com.faolline.graphcore.Runtime` but the Runtime assembly
does **not** reference the Editor assembly (one-way dependency).
