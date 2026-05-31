using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>EditMode tests: sub-dialogue nesting and runtime cycle detection.</summary>
    public class DialogueSubGraphTests
    {
        private static DialogueGraph Child()
        {
            var g = ScriptableObject.CreateInstance<DialogueGraph>();
            var s = new StartNodeData { Id = "cs", NodeType = StartNodeData.NodeTypeId };
            var l = new DialogueLineNodeData { Id = "cl", NodeType = DialogueLineNodeData.NodeTypeId, TextKey = "dlg.hi" };
            var e = new EndNodeData { Id = "ce", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };
            g.AddNode(s); g.AddNode(l); g.AddNode(e);
            g.AddEdge(new BaseEdgeData { Id = "ce1", FromNodeId = "cs", ToNodeId = "cl", PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = "ce2", FromNodeId = "cl", ToNodeId = "ce", PortName = "out" });
            g.EntryNodeId = "cs";
            return g;
        }

        [Test]
        public void SubDialogue_Plays_AndResumesParent()
        {
            var child = Child();
            var parent = ScriptableObject.CreateInstance<DialogueGraph>();
            try
            {
                var s = new StartNodeData { Id = "ps", NodeType = StartNodeData.NodeTypeId };
                var sub = new SubGraphNodeData { Id = "sub", NodeType = SubGraphNodeData.NodeTypeId, TargetGraph = child, InheritParentContext = true };
                var e = new EndNodeData { Id = "pe", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };
                parent.AddNode(s); parent.AddNode(sub); parent.AddNode(e);
                parent.AddEdge(new BaseEdgeData { Id = "pe1", FromNodeId = "ps", ToNodeId = "sub", PortName = "out" });
                parent.AddEdge(new BaseEdgeData { Id = "pe2", FromNodeId = "sub", ToNodeId = "pe", PortName = "out" });
                parent.EntryNodeId = "ps";

                var provider = new CsvLocalizationProvider(DialoguePlayerTestGraphs.Csv, "en");
                var player = new DialoguePlayer(parent, new DialogueContext(), provider);

                LineStep line = null; EndStep end = null;
                player.OnLine += s2 => line = s2;
                player.OnEnded += s2 => end = s2;

                player.Start();    // drains into child, pauses at child line
                Assert.IsNotNull(line, "Child sub-dialogue line should be emitted.");
                Assert.AreEqual("Hello", line.ResolvedText);

                player.Advance();  // child line → child end → resume parent → parent end
                Assert.IsNotNull(end, "Parent should resume and end after the sub-dialogue.");
                Assert.AreEqual(EndReason.Completed, end.EndReason);
            }
            finally { Object.DestroyImmediate(parent); Object.DestroyImmediate(child); }
        }

        [Test]
        public void CyclicSubDialogue_Throws_BeforeRecursion()
        {
            var parent = ScriptableObject.CreateInstance<DialogueGraph>();
            try
            {
                var s = new StartNodeData { Id = "ps", NodeType = StartNodeData.NodeTypeId };
                var sub = new SubGraphNodeData { Id = "sub", NodeType = SubGraphNodeData.NodeTypeId, TargetGraph = parent };
                parent.AddNode(s); parent.AddNode(sub);
                parent.AddEdge(new BaseEdgeData { Id = "pe1", FromNodeId = "ps", ToNodeId = "sub", PortName = "out" });
                parent.EntryNodeId = "ps";

                var runner = new BaseRunner();
                runner.Start(parent, new DialogueContext(), DialogueExecutorRegistryFactory.Create());

                Assert.Throws<GraphCycleException>(() =>
                {
                    int guard = 0;
                    while (runner.State == RunnerState.NodeReady && guard++ < 10)
                        runner.Proceed();
                });
            }
            finally { Object.DestroyImmediate(parent); }
        }
    }
}
