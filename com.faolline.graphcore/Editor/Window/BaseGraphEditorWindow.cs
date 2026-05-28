using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Abstract base for Unity EditorWindow instances that host a <see cref="BaseGraphView"/>
    /// canvas. Implement <see cref="CreateGraphView"/> to supply the concrete view type.
    /// </summary>
    public abstract class BaseGraphEditorWindow : EditorWindow
    {
        private BaseGraphView _graphView;
        private BaseGraph _pendingGraph;

        /// <summary>The canvas hosted in this window. Available after OnEnable.</summary>
        protected BaseGraphView GraphView => _graphView;

        /// <summary>
        /// Called once in OnEnable. Return the concrete BaseGraphView subclass for this window.
        /// </summary>
        protected abstract BaseGraphView CreateGraphView();

        /// <summary>
        /// Loads <paramref name="graph"/> into <see cref="GraphView"/>.
        /// If called before OnEnable, the load is deferred until the view is ready.
        /// </summary>
        protected void LoadGraph(BaseGraph graph)
        {
            if (_graphView != null)
                _graphView.LoadGraph(graph);
            else
                _pendingGraph = graph;
        }

        private void OnEnable()
        {
            _graphView = CreateGraphView();
            _graphView.StretchToParentSize();
            rootVisualElement.Add(_graphView);

            BuildToolbar();

            rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown);

            if (_pendingGraph != null)
            {
                _graphView.LoadGraph(_pendingGraph);
                _pendingGraph = null;
            }
        }

        private void OnDisable()
        {
            if (_graphView != null)
            {
                rootVisualElement.Remove(_graphView);
                _graphView = null;
            }
        }

        private void BuildToolbar()
        {
            var toolbar = new UnityEditor.UIElements.Toolbar();

            var saveButton = new UnityEditor.UIElements.ToolbarButton(() => _graphView?.SaveGraph())
            {
                text = "Save"
            };
            toolbar.Add(saveButton);

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
