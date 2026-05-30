using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Faolline.GraphCore;
using Faolline.GraphTest.Editor;

namespace Faolline.GraphTest.Tests
{
    [TestFixture]
    public class TestGraphViewAddNodeTests
    {
        private TestGraph _graph;
        private TestGraphView _view;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<TestGraph>();
            _view = new TestGraphView();
            _view.LoadGraph(_graph);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_graph);
        }

        [Test]
        public void AddStatementNode_AddsNodeToGraph()
        {
            var node = new TestStatementNodeData { NodeType = TestStatementNodeData.NodeTypeId };
            _view.CallAddNodeToCanvas(node, Vector2.zero);

            Assert.AreEqual(1, _graph.Nodes.Count,
                "Adding a TestStatementNodeData must register it in the graph");
            Assert.AreSame(node, _graph.Nodes[0]);
        }

        [Test]
        public void AddStartNode_CreatesStartNodeView()
        {
            var node = new StartNodeData { NodeType = StartNodeData.NodeTypeId };
            _view.CallAddNodeToCanvas(node, new Vector2(50f, 50f));

            Assert.AreEqual(1, _graph.Nodes.Count);
            Assert.IsInstanceOf<StartNodeData>(_graph.Nodes[0]);
        }

        [Test]
        public void AddEndNode_CreatesEndNodeView()
        {
            var node = new EndNodeData { NodeType = EndNodeData.NodeTypeId };
            _view.CallAddNodeToCanvas(node, Vector2.zero);

            Assert.AreEqual(1, _graph.Nodes.Count);
            Assert.IsInstanceOf<EndNodeData>(_graph.Nodes[0]);
        }

        [Test]
        public void AddChoiceNode_AddsNodeToGraph()
        {
            var node = new ChoiceNodeData { NodeType = ChoiceNodeData.NodeTypeId };
            _view.CallAddNodeToCanvas(node, Vector2.zero);

            Assert.AreEqual(1, _graph.Nodes.Count,
                "Adding a ChoiceNodeData must register it in the graph");
            Assert.IsInstanceOf<ChoiceNodeData>(_graph.Nodes[0]);
        }

        [Test]
        public void AddChoiceNode_CreatesChoiceNodeView()
        {
            var node = new ChoiceNodeData { NodeType = ChoiceNodeData.NodeTypeId };
            _view.CallAddNodeToCanvas(node, Vector2.zero);

            var choiceView = _view.GetChoiceView(node.Id);
            Assert.IsNotNull(choiceView,
                "CreateNodeView must dispatch a ChoiceNodeData to a ChoiceNodeView");
        }

        [Test]
        public void AddSubGraphNode_AddsNodeAndDispatchesView()
        {
            var node = new SubGraphNodeData { NodeType = SubGraphNodeData.NodeTypeId };
            _view.CallAddNodeToCanvas(node, Vector2.zero);

            Assert.AreEqual(1, _graph.Nodes.Count);
            Assert.IsInstanceOf<SubGraphNodeData>(_graph.Nodes[0]);

            var dispatched = _view.nodes.ToList().OfType<SubGraphNodeView>().Any(v => v.NodeData?.Id == node.Id);
            Assert.IsTrue(dispatched, "CreateNodeView must dispatch a SubGraphNodeData to a SubGraphNodeView");
        }

        [Test]
        public void AddNode_AssignsGuid()
        {
            var node = new TestStatementNodeData { NodeType = TestStatementNodeData.NodeTypeId };
            _view.CallAddNodeToCanvas(node, Vector2.zero);

            Assert.IsFalse(string.IsNullOrEmpty(node.Id),
                "AddNodeToCanvas must assign a GUID when Id is empty");
        }

        [Test]
        public void AddNode_MarksDirty()
        {
            var node = new TestStatementNodeData { NodeType = TestStatementNodeData.NodeTypeId };
            _view.CallAddNodeToCanvas(node, Vector2.zero);

            Assert.IsTrue(_view.IsDirty);
        }

        [Test]
        public void LoadGraph_ReconnectsEdgesToPorts()
        {
            var start = new StartNodeData         { Id = "s", NodeType = StartNodeData.NodeTypeId };
            var stmt  = new TestStatementNodeData { Id = "n", NodeType = TestStatementNodeData.NodeTypeId };
            _graph.AddNode(start);
            _graph.AddNode(stmt);
            _graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "n", PortName = "out" });

            _view.LoadGraph(_graph);

            var edges = _view.edges.ToList();
            Assert.AreEqual(1, edges.Count, "Reloaded graph must show its edge on the canvas");
            Assert.IsNotNull(edges[0].output, "Reloaded edge must be wired to a source output port");
            Assert.IsNotNull(edges[0].input,  "Reloaded edge must be wired to a target input port");
            Assert.AreEqual("out", edges[0].output.portName,
                "Source port must be matched by the edge's PortName");
        }

        [Test]
        public void LoadGraph_ReconnectsChoiceEdgeByChoiceId()
        {
            var choice = new ChoiceNodeData       { Id = "c", NodeType = ChoiceNodeData.NodeTypeId };
            choice.Choices.Add(new TestChoice { Id = "pick", Label = "Pick" });
            var target = new TestStatementNodeData { Id = "t", NodeType = TestStatementNodeData.NodeTypeId };
            _graph.AddNode(choice);
            _graph.AddNode(target);
            _graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "c", ToNodeId = "t", PortName = "pick" });

            _view.LoadGraph(_graph);

            var edges = _view.edges.ToList();
            Assert.AreEqual(1, edges.Count);
            Assert.AreEqual("pick", edges[0].output?.portName,
                "Choice edge must reconnect to the output port whose portName is the choice Id");
        }

        [Test]
        public void LoadGraph_ReloadingSameGraph_PreservesData()
        {
            _graph.AddNode(new StartNodeData         { Id = "s", NodeType = StartNodeData.NodeTypeId });
            _graph.AddNode(new TestStatementNodeData { Id = "n", NodeType = TestStatementNodeData.NodeTypeId });
            _graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "n", PortName = "out" });

            _view.LoadGraph(_graph);
            _view.LoadGraph(_graph); // reload must NOT delete the graph's own data via the change callback

            Assert.AreEqual(2, _graph.Nodes.Count, "Reloading a graph must not delete its nodes");
            Assert.AreEqual(1, _graph.Edges.Count, "Reloading a graph must not delete its edges");
        }

        [Test]
        public void LoadGraph_SwitchingBetweenGraphs_PreservesBothDatasets()
        {
            _graph.AddNode(new StartNodeData         { Id = "as", NodeType = StartNodeData.NodeTypeId });
            _graph.AddNode(new TestStatementNodeData { Id = "an", NodeType = TestStatementNodeData.NodeTypeId });
            _graph.AddEdge(new BaseEdgeData { Id = "ae", FromNodeId = "as", ToNodeId = "an", PortName = "out" });

            var other = ScriptableObject.CreateInstance<TestGraph>();
            other.AddNode(new StartNodeData { Id = "bs", NodeType = StartNodeData.NodeTypeId });
            try
            {
                _view.LoadGraph(_graph);
                _view.LoadGraph(other);
                _view.LoadGraph(_graph); // switch back

                Assert.AreEqual(2, _graph.Nodes.Count, "Switching away and back must not delete the first graph's nodes");
                Assert.AreEqual(1, _graph.Edges.Count, "Switching away and back must not delete the first graph's edges");
                Assert.AreEqual(1, other.Nodes.Count, "The other graph's data must remain intact too");
            }
            finally { Object.DestroyImmediate(other); }
        }

        [Test]
        public void RemoveChoice_KeepsSurvivingChoiceEdgeConnected()
        {
            var choice = new ChoiceNodeData { Id = "c", NodeType = ChoiceNodeData.NodeTypeId };
            var choiceA = new TestChoice { Id = "a", Label = "A" };
            var choiceB = new TestChoice { Id = "b", Label = "B" };
            choice.Choices.Add(choiceA);
            choice.Choices.Add(choiceB);
            var targetA = new TestStatementNodeData { Id = "ta", NodeType = TestStatementNodeData.NodeTypeId };
            var targetB = new TestStatementNodeData { Id = "tb", NodeType = TestStatementNodeData.NodeTypeId };
            _graph.AddNode(choice);
            _graph.AddNode(targetA);
            _graph.AddNode(targetB);
            _graph.AddEdge(new BaseEdgeData { Id = "ea", FromNodeId = "c", ToNodeId = "ta", PortName = "a" });
            _graph.AddEdge(new BaseEdgeData { Id = "eb", FromNodeId = "c", ToNodeId = "tb", PortName = "b" });
            _view.LoadGraph(_graph);

            var inspector = new TestNodeInspectorView();
            inspector.SetGraph(_graph);
            inspector.SetGraphView(_view);

            inspector.RemoveChoice(choice, choiceA);

            var edges = _view.edges.ToList();
            Assert.AreEqual(1, edges.Count, "Removed choice's edge is gone; the survivor remains");
            Assert.AreEqual("b", edges[0].output?.portName,
                "Surviving choice's edge must be reconnected to its (rebuilt) output port, not orphaned");
            Assert.IsNotNull(edges[0].input, "Surviving edge must still have a target input port");
        }
    }

    // Extension method to expose AddNodeToCanvas for tests
    internal static class TestGraphViewTestExtensions
    {
        public static void CallAddNodeToCanvas(this TestGraphView view, BaseNodeData node, Vector2 position)
        {
            // Use reflection to call the protected AddNodeToCanvas method
            var method = typeof(TestGraphView).GetMethod(
                "AddNodeToCanvas",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method?.Invoke(view, new object[] { node, position });
        }
    }
}
