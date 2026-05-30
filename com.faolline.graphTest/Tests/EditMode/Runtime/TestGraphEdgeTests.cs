using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphTest.Tests
{
    [TestFixture]
    public class TestGraphEdgeTests
    {
        [Test]
        public void TestGraph_CanAddAndRetrieveEdge()
        {
            var graph = ScriptableObject.CreateInstance<TestGraph>();
            try
            {
                var start = new StartNodeData { Id = "start", NodeType = StartNodeData.NodeTypeId };
                var end   = new EndNodeData   { Id = "end",   NodeType = EndNodeData.NodeTypeId };
                var edge  = new BaseEdgeData  { Id = "e1",    FromNodeId = "start", ToNodeId = "end", PortName = "out" };

                graph.AddNode(start);
                graph.AddNode(end);
                graph.AddEdge(edge);

                Assert.AreEqual(1, graph.Edges.Count);
                Assert.AreEqual("start", graph.Edges[0].FromNodeId);
                Assert.AreEqual("end",   graph.Edges[0].ToNodeId);
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void TestGraph_RemoveEdge_DecreasesEdgeCount()
        {
            var graph = ScriptableObject.CreateInstance<TestGraph>();
            try
            {
                var edge = new BaseEdgeData { Id = "e1", FromNodeId = "a", ToNodeId = "b" };
                graph.AddEdge(edge);
                Assert.AreEqual(1, graph.Edges.Count);

                graph.RemoveEdge(edge);
                Assert.AreEqual(0, graph.Edges.Count);
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void TestEdgeView_ImplementsBaseEdgeView()
        {
            Assert.IsTrue(
                typeof(Faolline.GraphCore.Editor.BaseEdgeView)
                    .IsAssignableFrom(typeof(Faolline.GraphTest.Editor.TestEdgeView)),
                "TestEdgeView must extend BaseEdgeView");
        }
    }
}
