using System;
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
        /// <summary>
        /// Fired when exactly one <see cref="BaseNodeView"/> with non-null NodeData is selected.
        /// </summary>
        public event Action<BaseNodeData> NodeSelected;

        /// <summary>
        /// Fired when the selection is cleared, becomes empty, or contains more than one node.
        /// </summary>
        public event Action SelectionCleared;

        private static readonly string UssName = "GraphCoreEditor";

        private BaseGraph _graph;
        private readonly Dictionary<string, BaseNodeView> _nodeViews = new Dictionary<string, BaseNodeView>();

        /// <summary>The graph currently loaded on this canvas. Null when no graph is loaded.</summary>
        protected BaseGraph Graph => _graph;

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

        // ── Selection overrides ───────────────────────────────────────────────

        /// <inheritdoc/>
        public override void AddToSelection(ISelectable selectable)
        {
            base.AddToSelection(selectable);
            NotifySelectionChanged();
        }

        /// <inheritdoc/>
        public override void RemoveFromSelection(ISelectable selectable)
        {
            base.RemoveFromSelection(selectable);
            NotifySelectionChanged();
        }

        /// <inheritdoc/>
        public override void ClearSelection()
        {
            base.ClearSelection();
            SelectionCleared?.Invoke();
        }

        private void NotifySelectionChanged()
        {
            int nodeCount = 0;
            BaseNodeData lastData = null;

            foreach (var item in selection)
            {
                if (item is BaseNodeView nv && nv.NodeData != null)
                {
                    nodeCount++;
                    lastData = nv.NodeData;
                }
            }

            if (nodeCount == 1)
                NodeSelected?.Invoke(lastData);
            else
                SelectionCleared?.Invoke();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Loads a graph asset onto the canvas. Clears any existing visual elements
        /// before rebuilding from <paramref name="graph"/>.
        /// </summary>
        public void LoadGraph(BaseGraph graph)
        {
            // Suppress change-tracking for the whole load. Clearing the old visual elements via
            // DeleteElements fires graphViewChanged → HandleRemovals, which would otherwise delete
            // node/edge DATA from the just-assigned graph (Unity may defer element removal by a step,
            // so the removal callback can land on the wrong graph and wipe it). Rebuilding is also
            // purely programmatic, so it must not be tracked either.
            graphViewChanged = null;
            try
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
                    view.TitleChanged += OnNodeTitleChanged;
                    AddElement(view);
                    _nodeViews[nodeData.Id] = view;
                }

                foreach (var edgeData in _graph.Edges)
                {
                    var view = CreateEdgeView(edgeData);
                    if (view == null) continue;
                    ConnectEdgeView(view, edgeData);
                    AddElement(view);
                }
            }
            finally
            {
                graphViewChanged = OnGraphViewChanged;
            }
        }

        /// <summary>
        /// Rebuilds and reconnects every edge view touching <paramref name="nodeId"/> from the graph
        /// data. Call after a node view regenerates its ports (e.g. a Choice node adding/removing a
        /// choice) so edges bound to surviving ports are reconnected rather than left orphaned.
        /// </summary>
        public void ReconnectNodeEdges(string nodeId)
        {
            if (_graph == null) return;
            if (!_nodeViews.ContainsKey(nodeId)) return;

            var stale = new List<Edge>();
            foreach (var el in edges.ToList())
            {
                if (el is BaseEdgeView bev && bev.EdgeData != null
                    && (bev.EdgeData.FromNodeId == nodeId || bev.EdgeData.ToNodeId == nodeId))
                    stale.Add(el);
            }
            foreach (var e in stale)
            {
                e.output?.Disconnect(e);
                e.input?.Disconnect(e);
                RemoveElement(e);
            }

            foreach (var edgeData in _graph.Edges)
            {
                if (edgeData.FromNodeId != nodeId && edgeData.ToNodeId != nodeId) continue;
                var view = CreateEdgeView(edgeData);
                if (view == null) continue;
                ConnectEdgeView(view, edgeData);
                AddElement(view);
            }
        }

        /// <summary>
        /// Reconnects a reloaded <paramref name="edgeView"/> to the source/target node ports so it
        /// renders on the canvas and tracks node movement. The source port is matched by
        /// <see cref="BaseEdgeData.PortName"/> (which equals the choice Id for Choice nodes); the
        /// target uses the node's first input port. No-op if either endpoint cannot be resolved.
        /// </summary>
        private void ConnectEdgeView(BaseEdgeView edgeView, BaseEdgeData edgeData)
        {
            if (edgeData == null) return;
            if (!_nodeViews.TryGetValue(edgeData.FromNodeId, out var fromView)) return;
            if (!_nodeViews.TryGetValue(edgeData.ToNodeId, out var toView)) return;

            var outputPort = FindPort(fromView.outputContainer, edgeData.PortName);
            var inputPort  = FindPort(toView.inputContainer, null);
            if (outputPort == null || inputPort == null) return;

            edgeView.output = outputPort;
            edgeView.input  = inputPort;
            outputPort.Connect(edgeView);
            inputPort.Connect(edgeView);
        }

        /// <summary>
        /// Returns the port in <paramref name="container"/> whose <c>portName</c> equals
        /// <paramref name="portName"/>. When <paramref name="portName"/> is null/empty, returns the
        /// first port (used for single-input nodes). Returns null when no match is found.
        /// </summary>
        private static UnityEditor.Experimental.GraphView.Port FindPort(VisualElement container, string portName)
        {
            UnityEditor.Experimental.GraphView.Port first = null;
            foreach (var child in container.Children())
            {
                if (child is UnityEditor.Experimental.GraphView.Port port)
                {
                    if (first == null) first = port;
                    if (port.portName == portName) return port;
                }
            }
            return string.IsNullOrEmpty(portName) ? first : null;
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

        /// <summary>
        /// Re-applies the resolved color to every node view currently on the canvas.
        /// Call when node data is modified externally (e.g. from the Inspector).
        /// </summary>
        public void RefreshNodeColors()
        {
            foreach (var view in _nodeViews.Values)
                view.RefreshColor();
        }

        // ── Port compatibility ────────────────────────────────────────────────

        /// <summary>
        /// Returns all ports that can receive a connection from <paramref name="startPort"/>.
        /// Allows connections between ports of opposite directions on different nodes.
        /// Override to add domain-specific type constraints.
        /// </summary>
        public override List<UnityEditor.Experimental.GraphView.Port> GetCompatiblePorts(
            UnityEditor.Experimental.GraphView.Port startPort,
            UnityEditor.Experimental.GraphView.NodeAdapter nodeAdapter)
        {
            var result = new List<UnityEditor.Experimental.GraphView.Port>();
            foreach (var port in ports.ToList())
            {
                if (port.direction != startPort.direction && port.node != startPort.node)
                    result.Add(port);
            }
            return result;
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

        // ── Protected helpers for subclasses ─────────────────────────────────

        /// <summary>
        /// Adds <paramref name="nodeData"/> to the loaded graph and the canvas.
        /// Assigns a GUID to <paramref name="nodeData"/> if <see cref="BaseNodeData.Id"/> is empty.
        /// Sets the canvas position to <paramref name="position"/>.
        /// No-op if no graph is currently loaded.
        /// </summary>
        protected void AddNodeToCanvas(BaseNodeData nodeData, Vector2 position)
        {
            if (_graph == null) return;

            if (string.IsNullOrEmpty(nodeData.Id))
                nodeData.Id = System.Guid.NewGuid().ToString("D");

            nodeData.Position = position;

            _graph.AddNode(nodeData);

            var view = CreateNodeView(nodeData);
            if (view == null) return;

            view.SetPosition(new Rect(position, Vector2.zero));
            view.TitleChanged += OnNodeTitleChanged;
            AddElement(view);
            _nodeViews[nodeData.Id] = view;
            _isDirty = true;
            OnNodeCreated(nodeData);
        }

        // Inline title edits on any node view mark the canvas dirty so they persist on save.
        private void OnNodeTitleChanged()
        {
            _isDirty = true;
            if (_graph != null) EditorUtility.SetDirty(_graph);
        }

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
