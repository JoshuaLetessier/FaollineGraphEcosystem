using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>EditMode tests for the DialogueGraph asset.</summary>
    public class DialogueGraphTests
    {
        [Test]
        public void DialogueGraph_IsBaseGraph_WithStableId()
        {
            var graph = ScriptableObject.CreateInstance<DialogueGraph>();
            try
            {
                Assert.IsInstanceOf<BaseGraph>(graph);
                Assert.IsFalse(string.IsNullOrEmpty(graph.GraphId), "GraphId should be assigned on enable.");
                var id = graph.GraphId;
                Assert.AreEqual(id, graph.GraphId, "GraphId must be stable.");
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void Graph_RoundTrips_NodesEdges()
        {
            var graph = ScriptableObject.CreateInstance<DialogueGraph>();
            try
            {
                var start = new StartNodeData { Id = "start", NodeType = StartNodeData.NodeTypeId };
                var end   = new EndNodeData   { Id = "end",   NodeType = EndNodeData.NodeTypeId };
                graph.AddNode(start);
                graph.AddNode(end);
                graph.AddEdge(new BaseEdgeData { Id = "e0", FromNodeId = "start", ToNodeId = "end" });
                graph.EntryNodeId = "start";

                Assert.AreEqual(2, graph.Nodes.Count);
                Assert.AreEqual(1, graph.Edges.Count);
                Assert.AreEqual("start", graph.EntryNodeId);
            }
            finally { Object.DestroyImmediate(graph); }
        }
    }
}
