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
                _graphView.LoadGraph(_pendingGraph);
                OnGraphLoaded(_pendingGraph);
                _pendingGraph = null;
            }
        }

        private void OnDisable()
        {
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

            PopulateToolbar(toolbar);

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
