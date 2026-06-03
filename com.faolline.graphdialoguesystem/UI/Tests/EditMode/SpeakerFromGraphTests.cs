using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphDialogue;
using Faolline.GraphDialogue.UI;
using Faolline.GraphLocalization;

namespace Faolline.GraphDialogue.UI.Tests
{
    /// <summary>
    /// The dialogue graph owns its speakers; the driver reads them (no scene-side speaker list).
    /// Covers DialogueGraph.FindSpeaker and DialogueDriver sourcing speakers from the played graph.
    /// </summary>
    public class SpeakerFromGraphTests
    {
        // speaker_npc resolves to "NPC"; without the graph-sourced speaker the name would be the raw id "npc".
        private const string Csv = "Key,en\nline_l,Hello\nspeaker_npc,NPC\n";

        private static Speaker NewSpeaker(string id, string fallback)
        {
            var s = ScriptableObject.CreateInstance<Speaker>();
            s.SpeakerId = id;
            s.DisplayNameFallback = fallback;
            return s;
        }

        // Start → Line "l" (speaker "npc") → End
        private static DialogueGraph BuildGraph(Speaker speaker)
        {
            var g = ScriptableObject.CreateInstance<DialogueGraph>();
            g.AddSpeaker(speaker);
            var s = new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId };
            var l = new DialogueLineNodeData { Id = "l", NodeType = DialogueLineNodeData.NodeTypeId, SpeakerKey = "npc" };
            var e = new EndNodeData { Id = "e", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };
            g.AddNode(s); g.AddNode(l); g.AddNode(e);
            g.EntryNodeId = "s";
            g.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "l", PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "l", ToNodeId = "e", PortName = "out" });
            return g;
        }

        [Test]
        public void Graph_FindSpeaker_ReturnsMatchById_ElseNull()
        {
            var speaker = NewSpeaker("npc", "NPC");
            var g = BuildGraph(speaker);
            try
            {
                Assert.AreSame(speaker, g.FindSpeaker("npc"));
                Assert.IsNull(g.FindSpeaker("unknown"));
                Assert.IsNull(g.FindSpeaker(null));
            }
            finally
            {
                Object.DestroyImmediate(g);
                Object.DestroyImmediate(speaker);
            }
        }

        [Test]
        public void Driver_BindsSpeakersFromGraph_WhenNoOverride()
        {
            var speaker = NewSpeaker("npc", "NPC");
            var g = BuildGraph(speaker);
            var view = new RecordingDialogueView();
            var go = new GameObject("driver");
            var driver = go.AddComponent<DialogueDriver>();
            driver.View = view;
            driver.Provider = new CsvLocalizationProvider(Csv, "en");
            try
            {
                driver.StartDialogue(g);

                Assert.IsNotNull(view.BoundSpeakers, "Driver must bind the graph's speakers.");
                Assert.AreEqual(1, view.BoundSpeakers.Count);
                Assert.AreSame(speaker, view.BoundSpeakers[0]);

                // The player resolved the speaker name via the graph-sourced lookup (fallback name here).
                Assert.IsNotNull(view.LastLine);
                Assert.AreEqual("NPC", view.LastLine.ResolvedSpeakerName);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(g);
                Object.DestroyImmediate(speaker);
            }
        }
    }
}
