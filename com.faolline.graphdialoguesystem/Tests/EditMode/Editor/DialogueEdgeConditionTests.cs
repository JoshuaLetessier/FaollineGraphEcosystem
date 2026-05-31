using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>
    /// EditMode test for FR-021 "condition on a connection": an edge carrying a false condition
    /// blocks traversal at runtime, so the player gets stuck instead of advancing.
    /// </summary>
    public class DialogueEdgeConditionTests
    {
        [Test]
        public void FalseEdgeCondition_BlocksTraversal()
        {
            var gate = ScriptableObject.CreateInstance<AlwaysFalseCondition>();
            var graph = ScriptableObject.CreateInstance<DialogueGraph>();
            try
            {
                var s = new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId };
                var l = new DialogueLineNodeData { Id = "l", NodeType = DialogueLineNodeData.NodeTypeId, TextKey = "k" };
                var e = new EndNodeData { Id = "e", NodeType = EndNodeData.NodeTypeId };
                graph.AddNode(s); graph.AddNode(l); graph.AddNode(e);
                graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "l", PortName = "out" });
                graph.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "l", ToNodeId = "e", PortName = "out", Condition = gate });
                graph.EntryNodeId = "s";

                var player = new DialoguePlayer(graph, new DialogueContext(),
                    new CsvLocalizationProvider(string.Empty, "en"));

                bool stuck = false; bool ended = false;
                player.OnStuck += () => stuck = true;
                player.OnEnded += _ => ended = true;

                player.Start();    // pauses at line
                player.Advance();  // blocked edge → stuck

                Assert.IsTrue(stuck, "A false edge condition must block traversal (stuck).");
                Assert.IsFalse(ended, "Playback must not reach the end through a blocked edge.");
            }
            finally { Object.DestroyImmediate(gate); Object.DestroyImmediate(graph); }
        }
    }
}
