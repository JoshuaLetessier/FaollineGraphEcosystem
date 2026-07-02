using Faolline.GraphLocalization;
using Faolline.GraphCore;
using Faolline.GraphDialogue;
using UnityEngine;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>Shared in-memory graph builders for DialoguePlayer EditMode tests.</summary>
    internal static class DialoguePlayerTestGraphs
    {
        // Keys are derived from node/choice/speaker identity (DialogueLocalizationKeys): line_<id>,
        // choice_<id>, speaker_<speakerId>. Test graphs use deterministic Ids so the CSV matches.
        public const string Csv =
            "Key,en,fr\n" +
            "line_l,Hello,Bonjour\n" +
            "line_cl,Hello,Bonjour\n" +
            "choice_a,Yes,Oui\n" +
            "choice_b,No,Non\n" +
            "speaker_npc,NPC,PNJ\n";

        /// <summary>Start â†’ Line (Id "l", speaker "npc") â†’ End. Caller destroys the graph.</summary>
        public static DialogueGraph Linear()
        {
            var g = ScriptableObject.CreateInstance<DialogueGraph>();
            var s = new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId };
            var l = new DialogueLineNodeData { Id = "l", NodeType = DialogueLineNodeData.NodeTypeId, SpeakerKey = "npc" };
            var e = new EndNodeData { Id = "e", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };
            g.AddNode(s); g.AddNode(l); g.AddNode(e);
            g.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "l", PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "l", ToNodeId = "e", PortName = "out" });
            g.EntryNodeId = "s";
            return g;
        }

        /// <summary>
        /// Start â†’ Choice with two options. Option "a" (key "choice_a") â†’ End(Completed). Option "b"
        /// (key "choice_b") â†’ End(Cancelled), gated by an optional <paramref name="gateB"/> condition.
        /// </summary>
        public static DialogueGraph WithChoice(BaseCondition gateB = null)
        {
            var g = ScriptableObject.CreateInstance<DialogueGraph>();
            var s = new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId };
            var c = new ChoiceNodeData { Id = "c", NodeType = ChoiceNodeData.NodeTypeId };
            c.Choices.Add(new DialogueChoice { Id = "a" });
            c.Choices.Add(new DialogueChoice { Id = "b", Condition = gateB });
            var e1 = new EndNodeData { Id = "e1", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };
            var e2 = new EndNodeData { Id = "e2", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Cancelled };
            g.AddNode(s); g.AddNode(c); g.AddNode(e1); g.AddNode(e2);
            g.AddEdge(new BaseEdgeData { Id = "es", FromNodeId = "s", ToNodeId = "c", PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = "ea", FromNodeId = "c", ToNodeId = "e1", PortName = "a" });
            g.AddEdge(new BaseEdgeData { Id = "eb", FromNodeId = "c", ToNodeId = "e2", PortName = "b" });
            g.EntryNodeId = "s";
            return g;
        }

        /// <summary>
        /// Start → Router (plain BaseChoice branches, NOT DialogueChoice) → "a"→End(Completed),
        /// "b"→End(Cancelled). Each branch is gated by the matching condition. A router is auto-resolved by
        /// condition, never shown as a player prompt.
        /// </summary>
        public static DialogueGraph WithRouter(BaseCondition condA, BaseCondition condB)
        {
            var g = ScriptableObject.CreateInstance<DialogueGraph>();
            var s = new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId };
            var c = new ChoiceNodeData { Id = "c", NodeType = ChoiceNodeData.NodeTypeId };
            c.Choices.Add(new BaseChoice { Id = "a", Condition = condA });
            c.Choices.Add(new BaseChoice { Id = "b", Condition = condB });
            var e1 = new EndNodeData { Id = "e1", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };
            var e2 = new EndNodeData { Id = "e2", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Cancelled };
            g.AddNode(s); g.AddNode(c); g.AddNode(e1); g.AddNode(e2);
            g.AddEdge(new BaseEdgeData { Id = "es", FromNodeId = "s", ToNodeId = "c", PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = "ea", FromNodeId = "c", ToNodeId = "e1", PortName = "a" });
            g.AddEdge(new BaseEdgeData { Id = "eb", FromNodeId = "c", ToNodeId = "e2", PortName = "b" });
            g.EntryNodeId = "s";
            return g;
        }

        /// <summary>
        /// Start → Choice "a"→End(Completed, "persuaded"), "b"→End(Completed, "rejected").
        /// Both share the same EndReason; the OutcomeLabel distinguishes them.
        /// </summary>
        public static DialogueGraph WithOutcomeLabels()
        {
            var g = ScriptableObject.CreateInstance<DialogueGraph>();
            var s = new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId };
            var c = new ChoiceNodeData { Id = "c", NodeType = ChoiceNodeData.NodeTypeId };
            c.Choices.Add(new DialogueChoice { Id = "a" });
            c.Choices.Add(new DialogueChoice { Id = "b" });
            var e1 = new EndNodeData { Id = "e1", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed, OutcomeLabel = "persuaded" };
            var e2 = new EndNodeData { Id = "e2", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed, OutcomeLabel = "rejected" };
            g.AddNode(s); g.AddNode(c); g.AddNode(e1); g.AddNode(e2);
            g.AddEdge(new BaseEdgeData { Id = "es", FromNodeId = "s", ToNodeId = "c", PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = "ea", FromNodeId = "c", ToNodeId = "e1", PortName = "a" });
            g.AddEdge(new BaseEdgeData { Id = "eb", FromNodeId = "c", ToNodeId = "e2", PortName = "b" });
            g.EntryNodeId = "s";
            return g;
        }
    }
}
