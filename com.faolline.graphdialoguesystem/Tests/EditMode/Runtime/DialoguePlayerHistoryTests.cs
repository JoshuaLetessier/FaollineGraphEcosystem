using Faolline.GraphLocalization;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>US3 â€” step-back restores prior shared state and checkpoint navigation.</summary>
    public class DialoguePlayerHistoryTests
    {
        // Start â†’ L1(enter Counter=1, checkpoint) â†’ L2(enter Counter=2) â†’ End
        private static DialogueGraph Build(out SetIntAction a1, out SetIntAction a2)
        {
            a1 = ScriptableObject.CreateInstance<SetIntAction>(); a1.ParameterKey = DialogueContextKeys.Counter; a1.Value = 1;
            a2 = ScriptableObject.CreateInstance<SetIntAction>(); a2.ParameterKey = DialogueContextKeys.Counter; a2.Value = 2;

            var g = ScriptableObject.CreateInstance<DialogueGraph>();
            var s = new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId };
            var l1 = new DialogueLineNodeData { Id = "l1", NodeType = DialogueLineNodeData.NodeTypeId };
            l1.OnEnterActions.Add(a1); l1.IsCheckpoint = true;
            var l2 = new DialogueLineNodeData { Id = "l2", NodeType = DialogueLineNodeData.NodeTypeId };
            l2.OnEnterActions.Add(a2);
            var e = new EndNodeData { Id = "e", NodeType = EndNodeData.NodeTypeId };
            g.AddNode(s); g.AddNode(l1); g.AddNode(l2); g.AddNode(e);
            g.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s",  ToNodeId = "l1", PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "l1", ToNodeId = "l2", PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = "e3", FromNodeId = "l2", ToNodeId = "e",  PortName = "out" });
            g.EntryNodeId = "s";
            return g;
        }

        [Test]
        public void Back_RestoresEarlierState_AndReEmitsNode()
        {
            var g = Build(out var a1, out var a2);
            try
            {
                var ctx = new DialogueContext();
                var player = new DialoguePlayer(g, ctx, new CsvLocalizationProvider(DialoguePlayerTestGraphs.Csv, "en"));

                player.Start();    // at L1, counter=1
                Assert.AreEqual(1, ctx.Counter);
                player.Advance();  // at L2, counter=2
                Assert.AreEqual(2, ctx.Counter);

                player.Back();     // back to L1
                Assert.AreEqual(1, ctx.Counter, "Step-back restores the earlier counter value.");
                Assert.IsInstanceOf<LineStep>(player.CurrentStep);
                Assert.AreEqual("l1", player.CurrentStep.NodeId, "Back re-emits the restored node.");
            }
            finally { Object.DestroyImmediate(a1); Object.DestroyImmediate(a2); Object.DestroyImmediate(g); }
        }

        [Test]
        public void BackToCheckpoint_ReturnsToCheckpointNode()
        {
            var g = Build(out var a1, out var a2);
            try
            {
                var ctx = new DialogueContext();
                var player = new DialoguePlayer(g, ctx, new CsvLocalizationProvider(DialoguePlayerTestGraphs.Csv, "en"));

                player.Start();    // L1 (checkpoint)
                player.Advance();  // L2
                player.BackToCheckpoint();

                Assert.AreEqual("l1", player.CurrentStep.NodeId, "Returns to the nearest checkpoint node.");
            }
            finally { Object.DestroyImmediate(a1); Object.DestroyImmediate(a2); Object.DestroyImmediate(g); }
        }
    }
}
