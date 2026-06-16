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
        /// Override to add a lib's action buttons to the toolbar. Called from <c>BuildToolbar</c> after the shared
        /// document tools (Save / Arrange / ↻ Refresh) and the divider that follows them, so a lib's buttons form
        /// the next group. Use <see cref="ToolbarSeparator"/> to split that group by usage. Default is a no-op.
        /// </summary>
        protected virtual void PopulateToolbar(UnityEditor.UIElements.Toolbar toolbar) { }

        /// <summary>
        /// Override to add right-aligned controls to the toolbar (settings rather than actions — e.g. a language
        /// picker). Called from <c>BuildToolbar</c> after the flexible spacer, so these sit on the right. Default
        /// is a no-op.
        /// </summary>
        protected virtual void PopulateToolbarRight(UnityEditor.UIElements.Toolbar toolbar) { }

        /// <summary>
        /// A thin vertical divider for grouping toolbar items by usage. Add between two groups of buttons.
        /// </summary>
        protected static VisualElement ToolbarSeparator()
        {
            var sep = new VisualElement();
            sep.style.width = 1f;
            sep.style.marginLeft = 12f;
            sep.style.marginRight = 12f;
            sep.style.marginTop = 3f;
            sep.style.marginBottom = 3f;
            sep.style.backgroundColor = new Color(1f, 1f, 1f, 0.2f);
            return sep;
        }

        /// <summary>
        /// Refreshes the window without closing it: rebuilds the canvas from the graph data (recreating node and
        /// edge views and re-running edge routing, preserving layout + viewport) and reloads the window's dynamic
        /// data — the "little things" read once when the toolbar/panels are built and which can go stale (a
        /// language list from the localization settings, an asset dropdown, etc.). Runs on every Save (button +
        /// Ctrl+S) and from the toolbar's ↻ Refresh button. Override <see cref="OnRefresh"/> to add a lib's own
        /// reloads. Safe to call any time after OnEnable.
        /// </summary>
        public void Refresh()
        {
            _graphView?.ReloadView();
            OnRefresh();
        }

        /// <summary>
        /// Override to reload a window's dynamic data on top of the canvas rebuild (see <see cref="Refresh"/>).
        /// Default is a no-op.
        /// </summary>
        protected virtual void OnRefresh() { }

        // Persist the canvas to disk, then Refresh — the reload (canvas rebuild + dynamic data) lives in Refresh.
        private void SaveAndRefresh()
        {
            _graphView?.AutoSave(writeToDisk: true);
            Refresh();
        }

        // EditorPrefs key for the opt-in "colour edges by endpoints" toggle (shared across all graph editors).
        private const string ColorEdgesPrefKey = "Faolline.GraphCore.Editor.ColorEdgesByEndpoints";

        private void BuildToolbar()
        {
            var toolbar = new UnityEditor.UIElements.Toolbar();

            var saveButton = new UnityEditor.UIElements.ToolbarButton(SaveAndRefresh)
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

            var refreshButton = new UnityEditor.UIElements.ToolbarButton(Refresh)
            {
                text = "↻ Refresh",
                tooltip = "Rebuild the canvas (re-render nodes + re-route edges) and reload window data that is "
                        + "read once when the window opens (e.g. the language list from your localization "
                        + "settings), without closing the window. Also runs on Save."
            };
            toolbar.Add(refreshButton);

            // Divider: the shared document tools (above) form their own group; a lib's buttons follow as the next.
            toolbar.Add(ToolbarSeparator());
            PopulateToolbar(toolbar);

            // Flexible spacer pushes the settings + edges hint to the right.
            var spacer = new UnityEngine.UIElements.VisualElement();
            spacer.style.flexGrow = 1f;
            toolbar.Add(spacer);

            PopulateToolbarRight(toolbar);

            // Opt-in, persisted: colour each edge as a source→target gradient (each end takes its node's colour)
            // so dense graphs stay readable. Off by default; flipping it re-colours every edge live.
            BaseEdgeView.ColorByEndpoints = EditorPrefs.GetBool(ColorEdgesPrefKey, false);
            var colorEdgesToggle = new UnityEditor.UIElements.ToolbarToggle
            {
                text = "🎨 edge colors",
                value = BaseEdgeView.ColorByEndpoints,
                tooltip = "Colour each edge from its source node's colour to its target node's colour, so you can "
                        + "tell at a glance which nodes an edge links in a dense graph. Off by default."
            };
            colorEdgesToggle.RegisterValueChangedCallback(evt =>
            {
                BaseEdgeView.ColorByEndpoints = evt.newValue;
                EditorPrefs.SetBool(ColorEdgesPrefKey, evt.newValue);
                _graphView?.RefreshAllEdgeColors();
            });
            toolbar.Add(colorEdgesToggle);

            var edgeHint = new UnityEngine.UIElements.Label("ⓘ edges")
            {
                tooltip = "Malleable edges — double-click an edge to add a bend point, drag the dots to shape it, "
                        + "right-click a dot to remove it. Live preview can lag; the routing fully refreshes on "
                        + "Save (Ctrl+S) or ↻ Refresh."
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
                SaveAndRefresh();
                evt.StopPropagation();
            }
        }
    }
}
