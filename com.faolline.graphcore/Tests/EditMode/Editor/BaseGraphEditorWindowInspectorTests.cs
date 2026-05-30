using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// Tests for BaseGraphEditorWindow inspector integration:
    /// default null return, event wiring, and startup clear.
    /// </summary>
    [TestFixture]
    public class BaseGraphEditorWindowInspectorTests
    {
        // ── Test doubles ──────────────────────────────────────────────────────

        private class StubNodeData : BaseNodeData { }

        private class StubNodeView : BaseNodeView
        {
            public StubNodeView(BaseNodeData data) { Initialize(data); }
            protected override void OnBuildView() { }
        }

        private class StubGraphView : BaseGraphView
        {
            protected override BaseNodeView CreateNodeView(BaseNodeData node) => new StubNodeView(node);
            protected override BaseEdgeView CreateEdgeView(BaseEdgeData edge) => null;
        }

        private class SpyInspector : BaseNodeInspectorView
        {
            public BaseNodeData LastBoundNode;
            public bool WasCleared;
            public int BindCallCount;
            public int ClearCallCount;

            public override void BindNode(BaseNodeData node)
            {
                LastBoundNode = node;
                BindCallCount++;
            }

            public override void ClearInspector()
            {
                WasCleared = true;
                ClearCallCount++;
            }
        }

        private class MinimalWindow : BaseGraphEditorWindow
        {
            protected override BaseGraphView CreateGraphView() => new StubGraphView();
        }

        private class WindowWithInspector : BaseGraphEditorWindow
        {
            // Initialized lazily in CreateNodeInspectorView (called from OnEnable).
            // VisualElement construction is forbidden in ScriptableObject field initializers.
            public SpyInspector Spy;
            protected override BaseGraphView CreateGraphView() => new StubGraphView();
            protected override BaseNodeInspectorView CreateNodeInspectorView()
            {
                Spy = new SpyInspector();
                return Spy;
            }
        }

        // ── T010 ──────────────────────────────────────────────────────────────

        [Test]
        public void CreateNodeInspectorView_returns_null_by_default()
        {
            var window = ScriptableObject.CreateInstance<MinimalWindow>();
            try
            {
                var method = typeof(BaseGraphEditorWindow).GetMethod(
                    "CreateNodeInspectorView",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(method, "CreateNodeInspectorView must exist on BaseGraphEditorWindow");
                var result = method.Invoke(window, null);
                Assert.IsNull(result, "Default CreateNodeInspectorView must return null");
            }
            finally { Object.DestroyImmediate(window); }
        }

        // ── T011 ──────────────────────────────────────────────────────────────

        [Test]
        public void BindNode_called_when_NodeSelected_fires()
        {
            var graphView = new StubGraphView();
            var spy = new SpyInspector();
            var data = new StubNodeData { Id = "n1", NodeType = "test" };
            var nodeView = new StubNodeView(data);

            graphView.NodeSelected += spy.BindNode;

            graphView.AddToSelection(nodeView);

            Assert.AreEqual(data, spy.LastBoundNode,
                "BindNode must be called with the selected node's data when NodeSelected fires");
        }

        // ── T012 ──────────────────────────────────────────────────────────────

        [Test]
        public void ClearInspector_called_when_SelectionCleared_fires()
        {
            var graphView = new StubGraphView();
            var spy = new SpyInspector();
            var data = new StubNodeData { Id = "n1", NodeType = "test" };
            var nodeView = new StubNodeView(data);

            graphView.SelectionCleared += spy.ClearInspector;

            graphView.AddToSelection(nodeView);
            graphView.ClearSelection();

            Assert.IsTrue(spy.WasCleared,
                "ClearInspector must be called when SelectionCleared fires");
        }

        // ── T013 ──────────────────────────────────────────────────────────────

        [Test]
        public void ClearInspector_called_on_startup()
        {
            // CreateInstance calls OnEnable automatically; by the time it returns,
            // CreateNodeInspectorView and the initial ClearInspector call have already run.
            var window = ScriptableObject.CreateInstance<WindowWithInspector>();
            try
            {
                Assert.IsNotNull(window.Spy, "CreateNodeInspectorView must have been called during OnEnable");
                Assert.IsTrue(window.Spy.WasCleared,
                    "ClearInspector must be called during OnEnable when inspector is non-null");
            }
            finally { Object.DestroyImmediate(window); }
        }
    }
}
