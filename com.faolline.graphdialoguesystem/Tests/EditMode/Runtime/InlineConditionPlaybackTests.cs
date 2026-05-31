using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>US3 — inline conditions gate choices and node entry during playback.</summary>
    public class InlineConditionPlaybackTests
    {
        [Test]
        public void GatedChoice_TogglesAvailability_WithContextValue()
        {
            // Option "b" gated by BoolCondition(flag == true).
            var gate = ScriptableObject.CreateInstance<BoolCondition>();
            gate.ParameterKey = DialogueContextKeys.Flag; gate.ExpectedValue = true;
            var graph = DialoguePlayerTestGraphs.WithChoice(gate);
            try
            {
                var ctx = new DialogueContext { Flag = false };
                var player = new DialoguePlayer(graph, ctx,
                    new CsvLocalizationProvider(DialoguePlayerTestGraphs.Csv, "en"));

                ChoiceStep step = null;
                player.OnChoices += s => step = s;
                player.Start();

                Assert.IsNotNull(step);
                Assert.IsTrue(step.Options[0].Available, "Open option always available.");
                Assert.IsFalse(step.Options[1].Available, "Gated option unavailable when flag=false.");
            }
            finally { Object.DestroyImmediate(gate); Object.DestroyImmediate(graph); }
        }

        [Test]
        public void FailingEntryCondition_ReportsStuck_WithoutPresentingNode()
        {
            var gate = ScriptableObject.CreateInstance<AlwaysFalseCondition>();
            var graph = ScriptableObject.CreateInstance<DialogueGraph>();
            try
            {
                var s = new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId };
                var l = new DialogueLineNodeData { Id = "l", NodeType = DialogueLineNodeData.NodeTypeId, TextKey = "dlg.hi" };
                l.EntryConditions.Add(gate); // node cannot be entered
                var e = new EndNodeData { Id = "e", NodeType = EndNodeData.NodeTypeId };
                graph.AddNode(s); graph.AddNode(l); graph.AddNode(e);
                graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "l", PortName = "out" });
                graph.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "l", ToNodeId = "e", PortName = "out" });
                graph.EntryNodeId = "s";

                var player = new DialoguePlayer(graph, new DialogueContext(),
                    new CsvLocalizationProvider(DialoguePlayerTestGraphs.Csv, "en"));

                bool line = false, stuck = false;
                player.OnLine += _ => line = true;
                player.OnStuck += () => stuck = true;
                player.Start();

                Assert.IsTrue(stuck, "A failing entry condition must report stuck.");
                Assert.IsFalse(line, "The gated node's content must not be presented.");
            }
            finally { Object.DestroyImmediate(gate); Object.DestroyImmediate(graph); }
        }
    }
}
