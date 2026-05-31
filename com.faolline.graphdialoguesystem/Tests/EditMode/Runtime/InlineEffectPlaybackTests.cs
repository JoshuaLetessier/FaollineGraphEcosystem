using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>US3 — inline enter/exit effects mutate shared state during playback.</summary>
    public class InlineEffectPlaybackTests
    {
        [Test]
        public void EnterEffect_SetsState_BeforeStepEmitted_AndLaterConditionReadsIt()
        {
            var setFlag = ScriptableObject.CreateInstance<SetBoolAction>();
            setFlag.ParameterKey = DialogueContextKeys.Flag; setFlag.Value = true;

            var graph = ScriptableObject.CreateInstance<DialogueGraph>();
            try
            {
                var s = new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId };
                var l = new DialogueLineNodeData { Id = "l", NodeType = DialogueLineNodeData.NodeTypeId, TextKey = "dlg.hi" };
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
                player.OnLine += _ => flagAtLine = ctx.Flag;
                player.Start();

                Assert.IsTrue(flagAtLine, "Enter effect must run before the line step is emitted.");
                Assert.IsTrue(ctx.Flag, "Shared state carries the effect's value for later conditions.");
            }
            finally { Object.DestroyImmediate(setFlag); Object.DestroyImmediate(graph); }
        }

        [Test]
        public void ExitEffect_RunsBeforeAdvancing()
        {
            var setCounter = ScriptableObject.CreateInstance<SetIntAction>();
            setCounter.ParameterKey = DialogueContextKeys.Counter; setCounter.Value = 9;

            var graph = ScriptableObject.CreateInstance<DialogueGraph>();
            try
            {
                var s = new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId };
                var l1 = new DialogueLineNodeData { Id = "l1", NodeType = DialogueLineNodeData.NodeTypeId, TextKey = "dlg.hi" };
                l1.OnExitActions.Add(setCounter);
                var l2 = new DialogueLineNodeData { Id = "l2", NodeType = DialogueLineNodeData.NodeTypeId, TextKey = "dlg.yes" };
                var e = new EndNodeData { Id = "e", NodeType = EndNodeData.NodeTypeId };
                graph.AddNode(s); graph.AddNode(l1); graph.AddNode(l2); graph.AddNode(e);
                graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s",  ToNodeId = "l1", PortName = "out" });
                graph.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "l1", ToNodeId = "l2", PortName = "out" });
                graph.AddEdge(new BaseEdgeData { Id = "e3", FromNodeId = "l2", ToNodeId = "e",  PortName = "out" });
                graph.EntryNodeId = "s";

                var ctx = new DialogueContext();
                var player = new DialoguePlayer(graph, ctx,
                    new CsvLocalizationProvider(DialoguePlayerTestGraphs.Csv, "en"));

                int counterAtL2 = -1;
                player.OnLine += step => { if (step.NodeId == "l2") counterAtL2 = ctx.Counter; };

                player.Start();    // pauses at l1
                Assert.AreEqual(0, ctx.Counter, "Exit effect has not run yet at l1.");
                player.Advance();  // l1 exit (counter=9) → l2

                Assert.AreEqual(9, counterAtL2, "Exit effect must run before the next node is entered.");
            }
            finally { Object.DestroyImmediate(setCounter); Object.DestroyImmediate(graph); }
        }
    }
}
