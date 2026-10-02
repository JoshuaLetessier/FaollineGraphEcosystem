using System.Collections.Generic;
using Faolline.GraphCore;
using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>
    /// 052 US2 scenario 2: the player resolves each line in the table of the graph that owns it — the parent's
    /// table for the parent's lines, the sub-dialogue's own table while the sub-dialogue runs — and asks no
    /// other table.
    /// </summary>
    public class DialoguePlayerScopedLookupTests
    {
        private static DialogueGraph Child()
        {
            var g = ScriptableObject.CreateInstance<DialogueGraph>();
            g.name = "Child";
            var s = new StartNodeData { Id = "cs", NodeType = StartNodeData.NodeTypeId };
            var l = new DialogueLineNodeData { Id = "cl", NodeType = DialogueLineNodeData.NodeTypeId };
            var e = new EndNodeData { Id = "ce", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };
            g.AddNode(s); g.AddNode(l); g.AddNode(e);
            g.AddEdge(new BaseEdgeData { Id = "ce1", FromNodeId = "cs", ToNodeId = "cl", PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = "ce2", FromNodeId = "cl", ToNodeId = "ce", PortName = "out" });
            g.EntryNodeId = "cs";
            return g;
        }

        [Test]
        public void ParentAndSubDialogueLines_ResolveInTheirOwnTables()
        {
            var child = Child();
            var parent = ScriptableObject.CreateInstance<DialogueGraph>();
            parent.name = "Parent";
            try
            {
                var s = new StartNodeData { Id = "ps", NodeType = StartNodeData.NodeTypeId };
                var pl = new DialogueLineNodeData { Id = "pl", NodeType = DialogueLineNodeData.NodeTypeId };
                var sub = new SubGraphNodeData { Id = "sub", NodeType = SubGraphNodeData.NodeTypeId, TargetGraph = child, InheritParentContext = true };
                var e = new EndNodeData { Id = "pe", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };
                parent.AddNode(s); parent.AddNode(pl); parent.AddNode(sub); parent.AddNode(e);
                parent.AddEdge(new BaseEdgeData { Id = "pe1", FromNodeId = "ps", ToNodeId = "pl", PortName = "out" });
                parent.AddEdge(new BaseEdgeData { Id = "pe2", FromNodeId = "pl", ToNodeId = "sub", PortName = "out" });
                parent.AddEdge(new BaseEdgeData { Id = "pe3", FromNodeId = "sub", ToNodeId = "pe", PortName = "out" });
                parent.EntryNodeId = "ps";

                var provider = new ScopedRecordingProvider()
                    .With("Parent", "line_pl", "Parent line")
                    .With("Child", "line_cl", "Child line");
                var player = new DialoguePlayer(parent, new DialogueContext(), provider);

                var lines = new List<string>();
                player.OnLine += step => lines.Add(step.ResolvedText);
                EndStep end = null;
                player.OnEnded += step => end = step;

                player.Start();     // parent line
                player.Advance();   // → sub-dialogue line
                player.Advance();   // → child end → parent end

                CollectionAssert.AreEqual(new[] { "Parent line", "Child line" }, lines);
                Assert.IsNotNull(end);
                CollectionAssert.AreEquivalent(new[] { ("Parent", "line_pl"), ("Child", "line_cl") }, provider.ScopedCalls);
                Assert.IsEmpty(provider.ClassicCalls, "no untargeted lookup during playback");
            }
            finally { Object.DestroyImmediate(parent); Object.DestroyImmediate(child); }
        }
    }
}
