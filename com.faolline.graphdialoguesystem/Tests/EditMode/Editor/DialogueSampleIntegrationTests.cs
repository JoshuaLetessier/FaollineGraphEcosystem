using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>
    /// Polish — an end-to-end integration check that mirrors the sample dialogue shape and plays it
    /// start→end in two locales headlessly (covers SC-001/003/004 without touching AssetDatabase).
    /// </summary>
    public class DialogueSampleIntegrationTests
    {
        private const string Csv =
            "Key,en,fr\n" +
            "dlg.intro,Welcome,Bienvenue\n" +
            "dlg.opt.ask,Ask,Demander\n" +
            "dlg.opt.leave,Leave,Partir\n" +
            "dlg.town,Quiet place,Endroit paisible\n" +
            "speaker.mayor.name,Mayor,Maire\n";

        // Start → intro(line) → choice[ask→sub→end, leave→end]
        private static DialogueGraph Build(out DialogueGraph child)
        {
            child = ScriptableObject.CreateInstance<DialogueGraph>();
            var cs = new StartNodeData { Id = "cs", NodeType = StartNodeData.NodeTypeId };
            var cl = new DialogueLineNodeData { Id = "cl", NodeType = DialogueLineNodeData.NodeTypeId, SpeakerKey = "npc_mayor", TextKey = "dlg.town" };
            var ce = new EndNodeData { Id = "ce", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };
            child.AddNode(cs); child.AddNode(cl); child.AddNode(ce); child.EntryNodeId = "cs";
            child.AddEdge(new BaseEdgeData { Id = "c1", FromNodeId = "cs", ToNodeId = "cl", PortName = "out" });
            child.AddEdge(new BaseEdgeData { Id = "c2", FromNodeId = "cl", ToNodeId = "ce", PortName = "out" });

            var g = ScriptableObject.CreateInstance<DialogueGraph>();
            var s = new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId };
            var intro = new DialogueLineNodeData { Id = "i", NodeType = DialogueLineNodeData.NodeTypeId, SpeakerKey = "npc_mayor", TextKey = "dlg.intro" };
            var choice = new ChoiceNodeData { Id = "c", NodeType = ChoiceNodeData.NodeTypeId };
            choice.Choices.Add(new DialogueChoice { Id = "ask", DisplayTextKey = "dlg.opt.ask" });
            choice.Choices.Add(new DialogueChoice { Id = "leave", DisplayTextKey = "dlg.opt.leave" });
            var sub = new SubGraphNodeData { Id = "sub", NodeType = SubGraphNodeData.NodeTypeId, TargetGraph = child, InheritParentContext = true };
            var end = new EndNodeData { Id = "e", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };
            g.AddNode(s); g.AddNode(intro); g.AddNode(choice); g.AddNode(sub); g.AddNode(end);
            g.EntryNodeId = "s";
            g.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "i", PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "i", ToNodeId = "c", PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = "e3", FromNodeId = "c", ToNodeId = "sub", PortName = "ask" });
            g.AddEdge(new BaseEdgeData { Id = "e4", FromNodeId = "c", ToNodeId = "e", PortName = "leave" });
            g.AddEdge(new BaseEdgeData { Id = "e5", FromNodeId = "sub", ToNodeId = "e", PortName = "out" });
            return g;
        }

        [Test]
        public void Sample_PlaysStartToEnd_InTwoLocales()
        {
            foreach (var (locale, intro, town) in new[] { ("en", "Welcome", "Quiet place"), ("fr", "Bienvenue", "Endroit paisible") })
            {
                var graph = Build(out var child);
                var speaker = ScriptableObject.CreateInstance<Speaker>();
                speaker.SpeakerId = "npc_mayor"; speaker.DisplayNameKey = "speaker.mayor.name"; speaker.DisplayNameFallback = "Mayor";
                try
                {
                    var player = new DialoguePlayer(graph, new DialogueContext(),
                        new CsvLocalizationProvider(Csv, locale), _ => speaker);

                    string introText = null, townText = null;
                    EndStep end = null;
                    player.OnLine += s => { if (s.NodeId == "i") introText = s.ResolvedText; if (s.NodeId == "cl") townText = s.ResolvedText; };
                    player.OnEnded += s => end = s;

                    player.Start();        // intro line
                    Assert.AreEqual(intro, introText, $"intro @ {locale}");
                    player.Advance();      // → choice
                    player.Choose("ask");  // → sub-dialogue line
                    Assert.AreEqual(town, townText, $"sub line @ {locale}");
                    player.Advance();      // sub line → sub end → parent end

                    Assert.IsNotNull(end, "Dialogue reaches the end.");
                    Assert.AreEqual(EndReason.Completed, end.EndReason);
                }
                finally
                {
                    Object.DestroyImmediate(speaker);
                    Object.DestroyImmediate(graph);
                    Object.DestroyImmediate(child);
                }
            }
        }
    }
}
