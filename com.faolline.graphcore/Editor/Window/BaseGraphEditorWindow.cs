using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Abstract base for Unity EditorWindow instances that host a <see cref="BaseGraphView"/>
    /// canvas. Implement <see cref="CreateGraphView"/> to supply the concrete view type.
    /// Override <see cref="CreateNodeInspectorView"/> to add an embedded inspector panel;
    /// return null (the default) to keep the single-pane layout.
    /// </summary>
    public abstract class BaseGraphEditorWindow : EditorWindow
    {
        private BaseGraphView _graphView;
        private BaseNodeInspectorView _inspector;
        private VisualElement _rootContent; // the element added directly to rootVisualElement
        private BaseGraph _pendingGraph;
        private BaseGraph _loadedGraph;

        // Serialized so the open graph survives a domain reload (entering Play, a script recompile, or reopening
        // Unity with the window docked): Unity tears the window down (OnDisable) and rebuilds it (OnEnable) with
        // the plain fields reset to null, which is why the canvas would otherwise come back blank. Asset
        // references serialize across reloads, so OnEnable can reload the graph into the freshly built view.
        [SerializeField] private BaseGraph _persistedGraph;

        /// <summary>The canvas hosted in this window. Available after OnEnable.</summary>
        protected BaseGraphView GraphView => _graphView;

        /// <summary>
        /// The embedded inspector panel. Non-null only when <see cref="CreateNodeInspectorView"/>
        /// returns a non-null instance. Available after OnEnable.
        /// </summary>
        protected BaseNodeInspectorView Inspector => _inspector;

        /// <summary>The graph currently loaded into the canvas. Null when no graph is open.</summary>
        protected BaseGraph LoadedGraph => _loadedGraph;

        /// <summary>
        /// Called once in OnEnable. Return the concrete BaseGraphView subclass for this window.
        /// </summary>
        protected abstract BaseGraphView CreateGraphView();

        /// <summary>
        /// Override to supply a <see cref="BaseNodeInspectorView"/> that is embedded in a
        /// split-pane layout alongside the graph canvas. The default returns null, preserving
        /// the existing single-pane layout. The returned instance is wired to canvas selection
        /// events automatically.
        /// </summary>
        protected virtual BaseNodeInspectorView CreateNodeInspectorView() => null;

        /// <summary>
        /// Loads <paramref name="graph"/> into <see cref="GraphView"/>.
        /// If called before OnEnable, the load is deferred until the view is ready.
        /// </summary>
        protected void LoadGraph(BaseGraph graph)
        {
            _loadedGraph = graph;
            _persistedGraph = graph;   // remember it across domain reloads (see field doc)
            if (_graphView != null)
            {
                _graphView.LoadGraph(graph);
                OnGraphLoaded(graph);
            }
            else
            {
                _pendingGraph = graph;
            }
        }

        /// <summary>
        /// Called after a graph is successfully loaded into the canvas view.
        /// Override to perform additional setup (e.g., pass the graph to an inspector).
        /// Default implementation is a no-op.
        /// </summary>
        protected virtual void OnGraphLoaded(BaseGraph graph) { }

        private void OnEnable()
        {
            _graphView = CreateGraphView();
            _inspector = CreateNodeInspectorView();

            if (_inspector != null)
            {
                // Split layout: toolbar (in-flow, top) + split view (flex-grow, below).
                BuildToolbar();

                var splitView = new TwoPaneSplitView(1, 300f, TwoPaneSplitViewOrientation.Horizontal);
                splitView.style.flexGrow = 1;
                splitView.Add(_graphView);
                splitView.Add(_inspector);
                rootVisualElement.Add(splitView);
                _rootContent = splitView;

                _graphView.NodeSelected += _inspector.BindNode;
                _graphView.SelectionCleared += _inspector.ClearInspector;
                _inspector.ClearInspector();
            }
            else
            {
                // Existing single-pane layout: graphView absolute (back), toolbar last (front).
                _graphView.StretchToParentSize();
                rootVisualElement.Add(_graphView);
                _rootContent = _graphView;
                BuildToolbar();
            }

            rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown);

            if (_pendingGraph != null)
            {
                LoadGraph(_pendingGraph);
                _pendingGraph = null;
            }
            else if (_persistedGraph != null)
            {
                // Coming back from a domain reload (Play / recompile / reopened Unity): the view was rebuilt
                // empty; reload the graph that was open so the canvas isn't blank.
                LoadGraph(_persistedGraph);
            }

            EditorApplication.quitting += OnEditorQuitting;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.quitting -= OnEditorQuitting;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

            // Persist before teardown so a domain reload (Play / recompile) or a window close never drops the
            // canvas layout (node/group positions are only synced into the data on save). Mark dirty here; the
            // disk flush happens on the genuine close paths (OnDestroy / editor quit).
            _graphView?.AutoSave(writeToDisk: false);

            if (_inspector != null && _graphView != null)
            {
                _graphView.NodeSelected -= _inspector.BindNode;
                _graphView.SelectionCleared -= _inspector.ClearInspector;
            }

            if (_rootContent != null)
            {
                rootVisualElement.Remove(_rootContent);
                _rootContent = null;
            }

            _graphView = null;
        }

        private void OnDestroy()
        {
            // Genuine window close: flush the (already-synced, dirty) graph to disk so manual closes don't lose
            // work. OnDisable ran first and synced the canvas into the data.
            if (_persistedGraph != null)
                AssetDatabase.SaveAssets();
        }

        private void OnEditorQuitting()
        {
            // Editor shutting down: sync the live canvas and flush to disk while the view is still alive
            // (teardown runs after this).
            _graphView?.AutoSave(writeToDisk: true);
        }

        private void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            // Safety net for when "Enter Play Mode Options" disables the domain reload (no OnDisable fires):
            // persist the canvas into the data as we leave edit mode.
            if (change == PlayModeStateChange.ExitingEditMode)
                _graphView?.AutoSave(writeToDisk: false);
        }

        /// <summary>
        /// Override to add custom buttons or controls to the toolbar.
        /// Called from <c>BuildToolbar</c> after the Save button is added.
        /// Default implementation is a no-op.
        /// </summary>
        protected virtual void PopulateToolbar(UnityEditor.UIElements.Toolbar toolbar) { }

        private void BuildToolbar()
        {
            var toolbar = new UnityEditor.UIElements.Toolbar();

            var saveButton = new UnityEditor.UIElements.ToolbarButton(() => _graphView?.SaveGraph())
            {
                text = "Save"
            };
            toolbar.Add(saveButton);

            var arrangeButton = new UnityEditor.UIElements.ToolbarButton(() => _graphView?.ArrangeGraph())
            {
                text = "Arrange",
                tooltip = "Auto-arrange the graph into a tidy left-to-right layered layout (clears manual edge bends)."
            };
            toolbar.Add(arrangeButton);

            PopulateToolbar(toolbar);

            // Right-aligned info on the malleable edges (their routing fully refreshes on Save).
            var spacer = new UnityEngine.UIElements.VisualElement();
            spacer.style.flexGrow = 1f;
            toolbar.Add(spacer);

            var edgeHint = new UnityEngine.UIElements.Label("ⓘ edges")
            {
                tooltip = "Malleable edges — double-click an edge to add a bend point, drag the dots to shape it, "
                        + "right-click a dot to remove it. Live preview can lag; the routing fully refreshes on "
                        + "Save (Ctrl+S)."
            };
            edgeHint.style.unityTextAlign = UnityEngine.TextAnchor.MiddleRight;
            edgeHint.style.opacity = 0.6f;
            edgeHint.style.marginRight = 6f;
            toolbar.Add(edgeHint);

            rootVisualElement.Add(toolbar);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.S && evt.ctrlKey)
            {
                _graphView?.SaveGraph();
                evt.StopPropagation();
            }
        }
    }
}
