using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// Tests for BaseGraphView.AddNodeToCanvas protected helper.
    /// </summary>
    [TestFixture]
    public class BaseGraphViewAddNodeTests
    {
        private class StubNodeData : BaseNodeData { }

        private class StubEdgeView : BaseEdgeView
        {
            public StubEdgeView(BaseEdgeData data) { Initialize(data); }
        }

        private class TestableGraphView : BaseGraphView
        {
            public BaseNodeView LastCreatedView;

            protected override BaseNodeView CreateNodeView(BaseNodeData node)
            {
                var view = new StubNodeView(node);
                LastCreatedView = view;
                return view;
            }

            protected override BaseEdgeView CreateEdgeView(BaseEdgeData edge) => new StubEdgeView(edge);

            public void CallAddNodeToCanvas(BaseNodeData node, Vector2 position)
                => AddNodeToCanvas(node, position);
        }

        private class StubNodeView : BaseNodeView
        {
            public StubNodeView(BaseNodeData data) { Initialize(data); }
            protected override void OnBuildView() { }
        }

        private BaseGraph _graph;
        private TestableGraphView _view;

        [SetUp]
        public void SetUp()
        {
            _graph = UnityEngine.ScriptableObject.CreateInstance<BaseGraph>();
            _view = new TestableGraphView();
            _view.LoadGraph(_graph);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_graph);
        }

        [Test]
        public void AddNodeToCanvas_AddsNodeToLoadedGraph()
        {
            var node = new StubNodeData();

            _view.CallAddNodeToCanvas(node, Vector2.zero);

            Assert.AreEqual(1, _graph.Nodes.Count,
                "AddNodeToCanvas must add the node to the loaded graph's Nodes list");
            Assert.AreSame(node, _graph.Nodes[0]);
        }

        [Test]
        public void AddNodeToCanvas_AssignsGuidWhenIdEmpty()
        {
            var node = new StubNodeData();
            Assert.IsTrue(string.IsNullOrEmpty(node.Id), "Precondition: Id must be empty");

            _view.CallAddNodeToCanvas(node, Vector2.zero);

            Assert.IsFalse(string.IsNullOrEmpty(node.Id),
                "AddNodeToCanvas must assign a non-empty Id (GUID) to the node");
        }

        [Test]
        public void AddNodeToCanvas_PreservesExistingId()
        {
            var node = new StubNodeData { Id = "existing-id" };

            _view.CallAddNodeToCanvas(node, Vector2.zero);

            Assert.AreEqual("existing-id", node.Id,
                "AddNodeToCanvas must not overwrite an existing Id");
        }

        [Test]
        public void AddNodeToCanvas_SetsPosition()
        {
            var node = new StubNodeData();
            var pos = new Vector2(100f, 200f);

            _view.CallAddNodeToCanvas(node, pos);

            Assert.AreEqual(pos, node.Position,
                "AddNodeToCanvas must set node.Position to the provided position");
        }

        [Test]
        public void AddNodeToCanvas_CreatesAndRegistersNodeView()
        {
            var node = new StubNodeData();

            _view.CallAddNodeToCanvas(node, Vector2.zero);

            Assert.IsNotNull(_view.LastCreatedView,
                "AddNodeToCanvas must call CreateNodeView to produce a view for the new node");
        }

        [Test]
        public void AddNodeToCanvas_MarksDirty()
        {
            var node = new StubNodeData();

            _view.CallAddNodeToCanvas(node, Vector2.zero);

            Assert.IsTrue(_view.IsDirty,
                "AddNodeToCanvas must set IsDirty to true");
        }

        [Test]
        public void AddNodeToCanvas_RefusesSecondStartNode()
        {
            _view.CallAddNodeToCanvas(new StartNodeData { NodeType = StartNodeData.NodeTypeId }, Vector2.zero);
            Assert.AreEqual(1, _graph.Nodes.Count, "Precondition: the first Start node is added.");

            LogAssert.Expect(LogType.Warning, new Regex("already has a Start node"));
            _view.CallAddNodeToCanvas(new StartNodeData { NodeType = StartNodeData.NodeTypeId }, Vector2.zero);

            Assert.AreEqual(1, _graph.Nodes.Count, "A second Start node must be refused (one Start per graph max).");
        }

        [Test]
        public void AddNodeToCanvas_AllowsNonStartNode_WhenAStartExists()
        {
            _view.CallAddNodeToCanvas(new StartNodeData { NodeType = StartNodeData.NodeTypeId }, Vector2.zero);
            _view.CallAddNodeToCanvas(new StubNodeData(), Vector2.zero);

            Assert.AreEqual(2, _graph.Nodes.Count, "The one-Start rule must only block Start nodes, not others.");
        }

        [Test]
        public void ReloadView_RebuildsCanvasFromData_PreservingNodes()
        {
            var node = new StubNodeData { Id = "n1" };
            _view.CallAddNodeToCanvas(node, new Vector2(10f, 20f));
            var firstView = _view.LastCreatedView;

            _view.ReloadView();

            Assert.AreEqual(1, _graph.Nodes.Count, "ReloadView must not change the data's node set.");
            Assert.AreSame(node, _graph.Nodes[0]);
            Assert.IsNotNull(_view.LastCreatedView, "ReloadView rebuilds the canvas (CreateNodeView runs again).");
            Assert.AreNotSame(firstView, _view.LastCreatedView,
                "ReloadView creates fresh node views from the data, so every edge re-renders cleanly.");
        }
    }
}
