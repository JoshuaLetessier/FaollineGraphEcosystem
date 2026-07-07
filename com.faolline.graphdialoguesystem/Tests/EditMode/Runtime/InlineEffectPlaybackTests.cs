using Faolline.GraphLocalization;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>US3 â€” inline enter/exit effects mutate shared state during playback.</summary>
    public class InlineEffectPlaybackTests
    {
        [Test]
        public void EnterEffect_SetsState_BeforeStepEmitted_AndLaterConditionReadsIt()
        {
            var flag = VariableDef.Bool("flag");
            var setFlag = ScriptableObject.CreateInstance<SetBoolAction>();
            setFlag.Variable = flag; setFlag.Value = true;

            var graph = ScriptableObject.CreateInstance<DialogueGraph>();
            try
            {
                var s = new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId };
                var l = new DialogueLineNodeData { Id = "l", NodeType = DialogueLineNodeData.NodeTypeId };
                l.OnEnterActions.Add(setFlag);
                var e = new EndNodeData { Id = "e", NodeType = EndNodeData.NodeTypeId };
                graph.AddNode(s); graph.AddNode(l); graph.AddNode(e);
                graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "l", PortName = "out" });
                graph.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "l", ToNodeId = "e", PortName = "out" });
                graph.EntryNodeId = "s";

                var ctx = new DialogueContext();
                var player = new DialoguePlayer(graph, ctx,
                    new CsvLocalizationProvider(DialoguePlayerTestGraphs.Csv, "en"));

                bool flagAtLine = false;
                player.OnLine += _ => flagAtLine = ctx.TryGet<bool>(flag, out var v) && v;
                player.Start();

                Assert.IsTrue(flagAtLine, "Enter effect must run before the line step is emitted.");
                Assert.IsTrue(ctx.TryGet<bool>(flag, out var f) && f, "Shared state carries the effect's value for later conditions.");
            }
            finally { Object.DestroyImmediate(setFlag); Object.DestroyImmediate(flag); Object.DestroyImmediate(graph); }
        }

        [Test]
        public void ExitEffect_RunsBeforeAdvancing()
        {
            var counter = VariableDef.Int("counter");
            var setCounter = ScriptableObject.CreateInstance<SetIntAction>();
            setCounter.Variable = counter; setCounter.Value = 9;

            var graph = ScriptableObject.CreateInstance<DialogueGraph>();
            try
            {
                var s = new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId };
                var l1 = new DialogueLineNodeData { Id = "l1", NodeType = DialogueLineNodeData.NodeTypeId };
                l1.OnExitActions.Add(setCounter);
                var l2 = new DialogueLineNodeData { Id = "l2", NodeType = DialogueLineNodeData.NodeTypeId };
                var e = new EndNodeData { Id = "e", NodeType = EndNodeData.NodeTypeId };
                graph.AddNode(s); graph.AddNode(l1); graph.AddNode(l2); graph.AddNode(e);
                graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s",  ToNodeId = "l1", PortName = "out" });
                graph.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "l1", ToNodeId = "l2", PortName = "out" });
                graph.AddEdge(new BaseEdgeData { Id = "e3", FromNodeId = "l2", ToNodeId = "e",  PortName = "out" });
                graph.EntryNodeId = "s";

                var ctx = new DialogueContext();
                var player = new DialoguePlayer(graph, ctx,
                    new CsvLocalizationProvider(DialoguePlayerTestGraphs.Csv, "en"));

                int Read() => ctx.TryGet<int>(counter, out var v) ? v : 0;
                int counterAtL2 = -1;
                player.OnLine += step => { if (step.NodeId == "l2") counterAtL2 = Read(); };

                player.Start();    // pauses at l1
                Assert.AreEqual(0, Read(), "Exit effect has not run yet at l1.");
                player.Advance();  // l1 exit (counter=9) â†’ l2

                Assert.AreEqual(9, counterAtL2, "Exit effect must run before the next node is entered.");
            }
            finally { Object.DestroyImmediate(setCounter); Object.DestroyImmediate(counter); Object.DestroyImmediate(graph); }
        }
    }
}
