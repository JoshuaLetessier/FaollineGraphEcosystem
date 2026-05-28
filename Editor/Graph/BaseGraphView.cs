using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Abstract canvas base class for GraphCore graph editors.
    /// Extend this class to create a custom node graph view for a downstream lib.
    /// Do not override <c>graphViewChanged</c> without calling <c>base.graphViewChanged</c>.
    /// </summary>
    public abstract partial class BaseGraphView : GraphView
    {
        private static readonly string UssName = "GraphCoreEditor";

        private BaseGraph _graph;
        private readonly Dictionary<string, BaseNodeView> _nodeViews = new Dictionary<string, BaseNodeView>();

        // Tracks whether the canvas has structural changes not yet saved to disk.
        // Node position changes are captured on SaveGraph(), not tracked here.
        private bool _isDirty;

        /// <summary>
        /// True when the canvas has unsaved structural changes. Cleared by <see cref="SaveGraph"/>.
        /// </summary>
        public bool IsDirty => _isDirty;

        protected BaseGraphView()
        {
            LoadStyleSheet();
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            graphViewChanged = OnGraphViewChanged;
            serializeGraphElements = OnSerializeGraphElements;
            unserializeAndPaste = OnUnserializeAndPaste;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Loads a graph asset onto the canvas. Clears any existing visual elements
        /// before rebuilding from <paramref name="graph"/>.
        /// </summary>
        public void LoadGraph(BaseGraph graph)
        {
            _graph = graph;
            _isDirty = false;

            DeleteElements(graphElements.ToList());
            _nodeViews.Clear();

            if (_graph == null)
                return;

            foreach (var nodeData in _graph.Nodes)
            {
                var view = CreateNodeView(nodeData);
                if (view == null) continue;
                view.SetPosition(new Rect(nodeData.Position, Vector2.zero));
                AddElement(view);
                _nodeViews[nodeData.Id] = view;
            }

            foreach (var edgeData in _graph.Edges)
            {
                var view = CreateEdgeView(edgeData);
                if (view == null) continue;
                AddElement(view);
            }
        }

        /// <summary>
        /// Syncs all canvas node positions to <c>BaseNodeData.Position</c>, then marks
        /// the asset dirty and saves it to disk. No-op if no graph is loaded.
        /// </summary>
        public void SaveGraph()
        {
            if (_graph == null)
                return;

            foreach (var kvp in _nodeViews)
            {
                var rect = kvp.Value.GetPosition();
                kvp.Value.NodeData.Position = rect.position;
            }

            EditorUtility.SetDirty(_graph);
            AssetDatabase.SaveAssets();
            _isDirty = false;
        }

        // ── Abstract factory methods ──────────────────────────────────────────

        /// <summary>
        /// Create and return a <see cref="BaseNodeView"/> for the given node data.
        /// </summary>
        protected abstract BaseNodeView CreateNodeView(BaseNodeData node);

        /// <summary>
        /// Create and return a <see cref="BaseEdgeView"/> for the given edge data.
        /// </summary>
        protected abstract BaseEdgeView CreateEdgeView(BaseEdgeData edge);

        // ── Hooks ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Called after a new node has been added to the canvas and to the graph data.
        /// </summary>
        protected virtual void OnNodeCreated(BaseNodeData node) { }

        /// <summary>
        /// Called after a new edge has been accepted and added to the canvas and graph data.
        /// CycleDetector has already approved the connection when this fires.
        /// </summary>
        protected virtual void OnEdgeConnected(BaseEdgeData edge) { }

        /// <summary>
        /// Called after a node has been removed from the canvas and graph data.
        /// </summary>
        protected virtual void OnNodeDeleted(BaseNodeData node) { }

        // ── GraphViewChanged routing ──────────────────────────────────────────

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (change.elementsToRemove != null)
                HandleRemovals(change.elementsToRemove);

            if (change.edgesToCreate != null)
                HandleEdgeCreation(change.edgesToCreate);

            // Node position changes (movedElements) are intentionally NOT written to
            // _graph here — sync is deferred to SaveGraph() per FR-003.

            return change;
        }

        private void HandleRemovals(List<GraphElement> elements)
        {
            foreach (var element in elements)
            {
                if (element is BaseNodeView nodeView && nodeView.NodeData != null)
                {
                    var nodeData = nodeView.NodeData;

                    // Remove all edges attached to this node
                    var edgesToRemove = new List<BaseEdgeData>();
                    foreach (var kvp in _nodeViews)
                    {
                        // collect edges from graph whose FromNodeId or ToNodeId matches
                    }

                    if (_graph != null)
                    {
                        // Remove edges from graph that reference this node
                        var edgeList = new List<BaseEdgeData>();
                        foreach (var e in _graph.Edges)
                        {
                            if (e.FromNodeId == nodeData.Id || e.ToNodeId == nodeData.Id)
                                edgeList.Add(e);
                        }
                        foreach (var e in edgeList)
                            _graph.RemoveEdge(e);

                        _graph.RemoveNode(nodeData);
                    }

                    _nodeViews.Remove(nodeData.Id);
                    _isDirty = true;
                    OnNodeDeleted(nodeData);
                }
                else if (element is BaseEdgeView edgeView && edgeView.EdgeData != null)
                {
                    _graph?.RemoveEdge(edgeView.EdgeData);
                    _isDirty = true;
                }
            }
        }

        private void HandleEdgeCreation(List<Edge> edges)
        {
            foreach (var edge in edges)
            {
                if (edge is BaseEdgeView edgeView)
                {
                    // Retrieve connected node data
                    BaseNodeData fromNode = null;
                    BaseNodeData toNode = null;

                    if (edge.output?.node is BaseNodeView outNode)
                        fromNode = outNode.NodeData;
                    if (edge.input?.node is BaseNodeView inNode)
                        toNode = inNode.NodeData;

                    if (fromNode == null || toNode == null) continue;

                    // Build edge data
                    var edgeData = new BaseEdgeData();
                    edgeData.Id = System.Guid.NewGuid().ToString("D");
                    edgeData.FromNodeId = fromNode.Id;
                    edgeData.ToNodeId = toNode.Id;
                    edgeData.PortName = edge.output?.portName ?? string.Empty;

                    // CycleDetector: check every edge connection without exception (FR-011)
                    BaseGraph targetGraph = null;
                    if (toNode is SubGraphNodeData subNode)
                        targetGraph = subNode.TargetGraph;

                    var cycleResult = CycleDetector.Check(_graph, targetGraph);
                    if (cycleResult.HasCycle)
                    {
                        var path = string.Join(" → ", cycleResult.CyclePath);
                        Debug.LogError($"[GraphCore] Cycle detected: {path}");
                        // Edge refused — remove it from the change list so it won't be added
                        edges.Remove(edge);
                        continue;
                    }

                    edgeView.EdgeData = edgeData;
                    _graph?.AddEdge(edgeData);
                    _isDirty = true;
                    OnEdgeConnected(edgeData);
                }
            }
        }

        private void LoadStyleSheet()
        {
            var guids = AssetDatabase.FindAssets($"{UssName} t:StyleSheet");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith($"{UssName}.uss"))
                {
                    var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                    if (styleSheet != null)
                    {
                        styleSheets.Add(styleSheet);
                        break;
                    }
                }
            }
        }
    }
}
