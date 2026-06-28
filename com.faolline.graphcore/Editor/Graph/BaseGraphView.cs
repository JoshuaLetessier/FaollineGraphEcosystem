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
        private readonly Dictionary<string, BaseGroupView> _groupViews = new Dictionary<string, BaseGroupView>();

        /// <summary>The graph currently loaded on this canvas. Null when no graph is loaded.</summary>
        protected BaseGraph Graph => _graph;

        private bool _isDirty;
        private bool _rerouteScheduled;

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

            InitRunCursor();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Loads a graph asset onto the canvas. Clears any existing visual elements
        /// before rebuilding from <paramref name="graph"/>.
        /// </summary>
        public void LoadGraph(BaseGraph graph)
        {
            graphViewChanged = null;
            try
            {
                _graph = graph;
                _isDirty = false;

                DeleteElements(graphElements.ToList());
                _nodeViews.Clear();
                _groupViews.Clear();

                if (_graph == null)
                    return;

                foreach (var nodeData in _graph.Nodes)
                {
                    var view = ResolveNodeView(nodeData);
                    if (view == null) continue;
                    view.SetPosition(new Rect(nodeData.Position, Vector2.zero));
                    view.TitleChanged += OnNodeTitleChanged;
                    AddElement(view);
                    _nodeViews[nodeData.Id] = view;
                    RerouteEdgesWhenMoved(view);
                }

                foreach (var edgeData in _graph.Edges)
                {
                    var view = CreateEdgeView(edgeData);
                    if (view == null) continue;
                    ConnectEdgeView(view, edgeData);
                    if (view is BaseEdgeView bev)
                        bev.DataChanged = () => { _isDirty = true; EditorUtility.SetDirty(_graph); };
                    AddElement(view);
                }

                foreach (var groupData in _graph.Groups)
                {
                    var groupView = new BaseGroupView(groupData);
                    groupView.DataChanged = () => { _isDirty = true; EditorUtility.SetDirty(_graph); };
                    WireGroupCollapseCallback(groupView);
                    AddElement(groupView);
                    _groupViews[groupData.Id] = groupView;

                    foreach (var nodeId in groupData.NodeIds)
                    {
                        if (_nodeViews.TryGetValue(nodeId, out var nv))
                            groupView.AddElement(nv);
                    }

                    if (groupData.IsCollapsed)
                    {
                        groupView.ApplyCollapsedVisual(true);
                        SetGroupNodesVisible(groupData, false);
                    }
                }
            }
            finally
            {
                graphViewChanged = OnGraphViewChanged;
            }

            RefreshRunCursor();
            RefreshAllEdgeColors();
        }

        /// <summary>
        /// Rebuilds the canvas from the graph data — recreating node and edge views and re-running edge routing —
        /// while preserving the current layout and the viewport. No-op when no graph is loaded.
        /// </summary>
        public void ReloadView()
        {
            if (_graph == null)
                return;

            SyncCanvasToData();
            var viewPos = viewTransform.position;
            var viewScale = viewTransform.scale;
            LoadGraph(_graph);
            UpdateViewTransform(viewPos, viewScale);
        }

        /// <summary>
        /// Syncs all canvas node positions to <c>BaseNodeData.Position</c>, then marks
        /// the asset dirty and saves it to disk. No-op if no graph is loaded.
        /// </summary>
        public void SaveGraph()
        {
            AutoSave(writeToDisk: true);
            ReloadView();
        }

        /// <summary>
        /// Persists the canvas without reloading it — for auto-save on window/editor close or before a domain
        /// reload. No-op if no graph is loaded.
        /// </summary>
        public void AutoSave(bool writeToDisk)
        {
            if (_graph == null)
                return;

            SyncCanvasToData();
            EditorUtility.SetDirty(_graph);
            if (writeToDisk)
                AssetDatabase.SaveAssets();
            _isDirty = false;
        }

        /// <summary>
        /// Re-applies the resolved color to every node view currently on the canvas.
        /// </summary>
        public void RefreshNodeColors()
        {
            foreach (var view in _nodeViews.Values)
                view.RefreshColor();
        }

        // ── Layout sync ──────────────────────────────────────────────────────

        private void SyncCanvasToData()
        {
            foreach (var kvp in _nodeViews)
            {
                var rect = kvp.Value.GetPosition();
                kvp.Value.NodeData.Position = rect.position;
            }

            foreach (var kvp in _groupViews)
            {
                var data = kvp.Value.GroupData;
                var rect = kvp.Value.GetPosition();
                data.Position = rect.position;
                if (!data.IsCollapsed)
                    data.Size = rect.size;
                data.NodeIds.Clear();
                foreach (var child in kvp.Value.containedElements)
                    if (child is BaseNodeView nv && nv.NodeData != null)
                        data.NodeIds.Add(nv.NodeData.Id);
            }
        }

        // ── Abstract factory methods ─────────────────────────────────────────

        /// <summary>
        /// Create and return a <see cref="BaseNodeView"/> for the given node data.
        /// </summary>
        protected abstract BaseNodeView CreateNodeView(BaseNodeData node);

        private BaseNodeView ResolveNodeView(BaseNodeData node)
            => node is GraphLinkNodeData link ? new GraphLinkNodeView(link) : CreateNodeView(node);

        /// <summary>
        /// Create and return a <see cref="BaseEdgeView"/> for the given edge data.
        /// </summary>
        protected abstract BaseEdgeView CreateEdgeView(BaseEdgeData edge);

        // ── Hooks ────────────────────────────────────────────────────────────

        /// <summary>Called after a new node has been added to the canvas and to the graph data.</summary>
        protected virtual void OnNodeCreated(BaseNodeData node) { }

        /// <summary>Called after a new edge has been accepted and added to the canvas and graph data.</summary>
        protected virtual void OnEdgeConnected(BaseEdgeData edge) { }

        /// <summary>Called after a node has been removed from the canvas and graph data.</summary>
        protected virtual void OnNodeDeleted(BaseNodeData node) { }

        // ── Style ────────────────────────────────────────────────────────────

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
