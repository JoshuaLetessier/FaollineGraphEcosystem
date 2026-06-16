using NUnit.Framework;
using UnityEngine;
using UnityEditor.Experimental.GraphView;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// The opt-in <see cref="BaseEdgeView.ColorByEndpoints"/> mode draws each edge as a gradient from its SOURCE
    /// node's colour to its TARGET node's colour; off (default) it keeps the single resolved colour.
    /// </summary>
    [TestFixture]
    public class BaseEdgeViewEndpointColorTests
    {
        private class TestNodeData : BaseNodeData { }

        // A node view with one input + one output port, so the graph view can actually connect an edge to it
        // (the endpoint-gradient reads the connected ports' nodes).
        private class PortNodeView : BaseNodeView
        {
            protected override void OnBuildView() { }
            public PortNodeView(BaseNodeData data)
            {
                Initialize(data);
                var inPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
                inPort.portName = "in";
                inputContainer.Add(inPort);
                var outPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
                outPort.portName = "";   // FindPort matches the first port for an empty PortName
                outputContainer.Add(outPort);
            }
        }

        private class TestEdgeView : BaseEdgeView
        {
            public TestEdgeView(BaseEdgeData data) { Initialize(data); }
        }

        private class TestGraphView : BaseGraphView
        {
            protected override BaseNodeView CreateNodeView(BaseNodeData node) => new PortNodeView(node);
            protected override BaseEdgeView CreateEdgeView(BaseEdgeData edge) => new TestEdgeView(edge);
        }

        [TearDown]
        public void TearDown()
        {
            BaseEdgeView.ColorByEndpoints = false;   // static flag — never leak it to other tests
            NodeTypeColorRegistry.Clear();
        }

        private static BaseEdgeView LoadTwoNodeEdge(TestGraphView view, out Color srcColor, out Color dstColor)
        {
            srcColor = new Color(0.9f, 0.1f, 0.1f);
            dstColor = new Color(0.1f, 0.2f, 0.9f);
            NodeTypeColorRegistry.Register("src", srcColor);
            NodeTypeColorRegistry.Register("dst", dstColor);

            var graph = ScriptableObject.CreateInstance<BaseGraph>();
            var n1 = new TestNodeData { Id = "n1", NodeType = "src" };
            var n2 = new TestNodeData { Id = "n2", NodeType = "dst" };
            graph.AddNode(n1);
            graph.AddNode(n2);
            graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "n1", ToNodeId = "n2", PortName = "" });

            view.LoadGraph(graph);

            BaseEdgeView edge = null;
            view.edges.ForEach(e => edge = e as BaseEdgeView);
            return edge;
        }

        [Test]
        public void ColorByEndpoints_On_GradesFromSourceColorToTargetColor()
        {
            BaseEdgeView.ColorByEndpoints = true;
            var view = new TestGraphView();
            var edge = LoadTwoNodeEdge(view, out var srcColor, out var dstColor);

            Assert.IsNotNull(edge, "the edge view was created and connected.");
            Assert.AreEqual(srcColor, edge.edgeControl.outputColor, "the out-port end takes the source node's colour.");
            Assert.AreEqual(dstColor, edge.edgeControl.inputColor, "the in-port end takes the target node's colour.");
        }

        [Test]
        public void ColorByEndpoints_SurvivesEdgeControlRedraw()
        {
            // Regression: Unity's Edge.UpdateEdgeControl resets the control colours from the port colours on every
            // redraw (a node hover triggers it). The endpoint gradient must survive that, or it reverts to grey
            // the instant the mouse touches a node.
            BaseEdgeView.ColorByEndpoints = true;
            var view = new TestGraphView();
            var edge = LoadTwoNodeEdge(view, out var srcColor, out var dstColor);
            Assert.IsNotNull(edge);

            edge.UpdateEdgeControl();   // simulate the redraw a hover/move would cause

            Assert.AreEqual(srcColor, edge.edgeControl.outputColor, "endpoint colour survives a control redraw (source).");
            Assert.AreEqual(dstColor, edge.edgeControl.inputColor, "endpoint colour survives a control redraw (target).");
        }

        [Test]
        public void ColorByEndpoints_Off_KeepsSingleResolvedColor()
        {
            BaseEdgeView.ColorByEndpoints = false;
            var view = new TestGraphView();
            var edge = LoadTwoNodeEdge(view, out _, out _);

            Assert.IsNotNull(edge);
            // No edge-level override / registry entry for the edge Id → the single resolved colour is the grey default.
            Assert.AreEqual(GraphCoreDefaults.NodeGrey, edge.edgeControl.outputColor);
            Assert.AreEqual(GraphCoreDefaults.NodeGrey, edge.edgeControl.inputColor);
            Assert.AreEqual(edge.edgeControl.outputColor, edge.edgeControl.inputColor, "off → both ends share one colour.");
        }
    }
}
