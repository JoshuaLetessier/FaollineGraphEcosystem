using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphDialogue;
using Faolline.GraphLocalization;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>
    /// The standalone <see cref="DialoguePlayer"/> can now opt into <c>titleFallback</c> (previously only
    /// <see cref="DialoguePresenter"/> could). With it, a code-built line whose localization key is absent renders
    /// its authored Title instead of the <c>#key</c> marker. (Cryptique rebuild finding.)
    /// </summary>
    public class DialoguePlayerTitleFallbackTests
    {
        private static DialogueGraph OneTitledLine()
        {
            var graph = ScriptableObject.CreateInstance<DialogueGraph>();
            var s = new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId };
            var l = new DialogueLineNodeData { Id = "greet", NodeType = DialogueLineNodeData.NodeTypeId, Title = "Hello there" };
            var e = new EndNodeData { Id = "e", NodeType = EndNodeData.NodeTypeId };
            graph.AddNode(s); graph.AddNode(l); graph.AddNode(e);
            graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "greet", PortName = "out" });
            graph.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "greet", ToNodeId = "e", PortName = "out" });
            graph.EntryNodeId = "s";
            return graph;
        }

        [Test]
        public void TitleFallbackOn_MissingKey_RendersAuthoredTitle()
        {
            var graph = OneTitledLine();
            try
            {
                string text = null;
                var player = new DialoguePlayer(graph, new DialogueContext(),
                    new CsvLocalizationProvider("Key,en\n", "en"), titleFallback: true);   // no CSV entry for the line
                player.OnLine += s => text = s.ResolvedText;
                player.Start();
                Assert.AreEqual("Hello there", text, "the absent key falls back to the authored node Title");
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void TitleFallbackOff_MissingKey_RendersTheKeyMarker()
        {
            var graph = OneTitledLine();
            try
            {
                string text = null;
                var player = new DialoguePlayer(graph, new DialogueContext(),
                    new CsvLocalizationProvider("Key,en\n", "en"));   // titleFallback defaults to false
                player.OnLine += s => text = s.ResolvedText;
                player.Start();
                Assert.IsTrue(text != null && text.StartsWith("#"),
                    "without the fallback, an absent key renders the #key marker, not the Title");
                Assert.AreNotEqual("Hello there", text);
            }
            finally { Object.DestroyImmediate(graph); }
        }
    }
}
