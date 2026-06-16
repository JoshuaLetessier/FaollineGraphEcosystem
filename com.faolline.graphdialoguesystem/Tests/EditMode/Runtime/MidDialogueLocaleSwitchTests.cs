using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphDialogue;
using Faolline.GraphLocalization;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>
    /// Proves a runtime locale switch BETWEEN lines affects the NEXT line: the presenter resolves each line's text
    /// live at emit time against the provider's current locale (<c>ResolveLine</c> → <c>CurrentLocale</c>). A dogfood
    /// test tried to verify this but mis-drove the player (Choose on a line is a no-op), so it never emitted a second
    /// line and looked broken — this drives it correctly with Advance.
    /// </summary>
    public class MidDialogueLocaleSwitchTests
    {
        private const string Csv =
            "Key,en,fr\n" +
            "line_a,Hello,Bonjour\n" +
            "line_b,Goodbye,Au revoir\n";

        [Test]
        public void LocaleSwitchBetweenLines_NextLineResolvesInTheNewLocale()
        {
            var graph = ScriptableObject.CreateInstance<DialogueGraph>();
            try
            {
                var s = new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId };
                var a = new DialogueLineNodeData { Id = "a", NodeType = DialogueLineNodeData.NodeTypeId };
                var b = new DialogueLineNodeData { Id = "b", NodeType = DialogueLineNodeData.NodeTypeId };
                var e = new EndNodeData { Id = "e", NodeType = EndNodeData.NodeTypeId };
                graph.AddNode(s); graph.AddNode(a); graph.AddNode(b); graph.AddNode(e);
                graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "a", PortName = "out" });
                graph.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "a", ToNodeId = "b", PortName = "out" });
                graph.AddEdge(new BaseEdgeData { Id = "e3", FromNodeId = "b", ToNodeId = "e", PortName = "out" });
                graph.EntryNodeId = "s";

                var loc = new CsvLocalizationProvider(Csv, "en");
                var player = new DialoguePlayer(graph, new DialogueContext(), loc);
                string last = null;
                player.OnLine += step => last = step.ResolvedText;

                player.Start();
                Assert.AreEqual("Hello", last, "line a resolves in the starting locale (en)");

                loc.SetLocale("fr");   // switch mid-dialogue, between line a and line b
                player.Advance();
                Assert.AreEqual("Au revoir", last,
                    "the NEXT line resolves in the locale current at emit time (fr) — mid-dialogue switch works");
            }
            finally { Object.DestroyImmediate(graph); }
        }
    }
}
