using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Faolline.GraphCore;
using Faolline.GraphDialogue;
using Faolline.GraphLocalization;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>
    /// The player pauses on BOTH line and choice nodes; calling <c>Choose</c> while still on a line (or
    /// <c>Advance</c> off a line) used to be a SILENT no-op — undebuggable. It now logs a diagnostic.
    /// (Dogfood finding: a consumer drove Start→Choose with no Advance and saw nothing happen.)
    /// </summary>
    public class ChooseAdvanceDiagnosticTests
    {
        private static DialogueGraph LineThenEnd()
        {
            var graph = ScriptableObject.CreateInstance<DialogueGraph>();
            var s = new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId };
            var l = new DialogueLineNodeData { Id = "l", NodeType = DialogueLineNodeData.NodeTypeId };
            var e = new EndNodeData { Id = "e", NodeType = EndNodeData.NodeTypeId };
            graph.AddNode(s); graph.AddNode(l); graph.AddNode(e);
            graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "l", PortName = "out" });
            graph.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "l", ToNodeId = "e", PortName = "out" });
            graph.EntryNodeId = "s";
            return graph;
        }

        [Test]
        public void Choose_WhileOnALine_LogsDiagnostic_AndIsNoOp()
        {
            var graph = LineThenEnd();
            try
            {
                var player = new DialoguePlayer(graph, new DialogueContext(),
                    new CsvLocalizationProvider(DialoguePlayerTestGraphs.Csv, "en"));
                player.Start();   // pauses on the line
                var onTheLine = player.CurrentStep;
                Assert.IsTrue(onTheLine is LineStep, "the player is paused on the line after Start");

                LogAssert.Expect(LogType.Warning, new Regex("Choose.*not paused at a choice"));
                player.Choose("anything");

                Assert.AreSame(onTheLine, player.CurrentStep,
                    "Choose() on a line is a no-op — the player did not advance");
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void Advance_AfterEnd_LogsDiagnostic()
        {
            var graph = LineThenEnd();
            try
            {
                var player = new DialoguePlayer(graph, new DialogueContext(),
                    new CsvLocalizationProvider(DialoguePlayerTestGraphs.Csv, "en"));
                player.Start();     // on the line
                player.Advance();   // line → end (dialogue ends)

                LogAssert.Expect(LogType.Warning, new Regex("Advance.*not paused on a line"));
                player.Advance();   // nothing left to advance → diagnostic, not a silent no-op
            }
            finally { Object.DestroyImmediate(graph); }
        }
    }
}
