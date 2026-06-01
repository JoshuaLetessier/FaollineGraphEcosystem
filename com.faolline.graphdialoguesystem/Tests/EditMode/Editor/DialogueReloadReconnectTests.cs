using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphDialogue.Editor;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>EditMode tests: load preserves data; removing one choice keeps the other's edge.</summary>
    public class DialogueReloadReconnectTests
    {
        [Test]
        public void RemoveChoiceEdges_RemovesOnlyThatChoicesEdge()
        {
            var graph = ScriptableObject.CreateInstance<DialogueGraph>();
            var gv = new DialogueGraphView();
            try
            {
                var choice = new ChoiceNodeData { Id = "c", NodeType = ChoiceNodeData.NodeTypeId };
                choice.Choices.Add(new DialogueChoice { Id = "a" });
                choice.Choices.Add(new DialogueChoice { Id = "b" });
                var e1 = new EndNodeData { Id = "e1", NodeType = EndNodeData.NodeTypeId };
                var e2 = new EndNodeData { Id = "e2", NodeType = EndNodeData.NodeTypeId };
                graph.AddNode(choice); graph.AddNode(e1); graph.AddNode(e2);
                graph.AddEdge(new BaseEdgeData { Id = "ea", FromNodeId = "c", ToNodeId = "e1", PortName = "a" });
                graph.AddEdge(new BaseEdgeData { Id = "eb", FromNodeId = "c", ToNodeId = "e2", PortName = "b" });

                gv.LoadGraphForTest(graph);
                gv.RemoveChoiceEdges("c", "a"); // remove choice 'a' edge only

                Assert.AreEqual(1, graph.Edges.Count, "Only choice 'a' edge should be removed.");
                Assert.AreEqual("b", graph.Edges[0].PortName, "Choice 'b' edge survives.");
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void LoadGraph_PreservesNodesAndEdges()
        {
            var graph = ScriptableObject.CreateInstance<DialogueGraph>();
            var gv = new DialogueGraphView();
            try
            {
                graph.AddNode(new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId });
                graph.AddNode(new EndNodeData { Id = "e", NodeType = EndNodeData.NodeTypeId });
                graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "e", PortName = "out" });

                gv.LoadGraphForTest(graph);

                Assert.AreEqual(2, graph.Nodes.Count, "LoadGraph must not delete node data.");
                Assert.AreEqual(1, graph.Edges.Count, "LoadGraph must not delete edge data.");
            }
            finally { Object.DestroyImmediate(graph); }
        }
    }
}
