using Faolline.GraphCore;
using Faolline.GraphDialogue;
using UnityEngine;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>Shared in-memory graph builders for DialoguePlayer EditMode tests.</summary>
    internal static class DialoguePlayerTestGraphs
    {
        public const string Csv =
            "Key,en,fr\n" +
            "dlg.hi,Hello,Bonjour\n" +
            "dlg.yes,Yes,Oui\n" +
            "dlg.no,No,Non\n" +
            "speaker.npc,NPC,PNJ\n";

        /// <summary>Start → Line("dlg.hi", speaker "npc") → End. Caller destroys the graph.</summary>
        public static DialogueGraph Linear()
        {
            var g = ScriptableObject.CreateInstance<DialogueGraph>();
            var s = new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId };
            var l = new DialogueLineNodeData { Id = "l", NodeType = DialogueLineNodeData.NodeTypeId, SpeakerKey = "npc", TextKey = "dlg.hi" };
            var e = new EndNodeData { Id = "e", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };
            g.AddNode(s); g.AddNode(l); g.AddNode(e);
            g.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "l", PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "l", ToNodeId = "e", PortName = "out" });
            g.EntryNodeId = "s";
            return g;
        }

        /// <summary>
        /// Start → Choice with two options. Option "a" ("dlg.yes") → End(Completed). Option "b"
        /// ("dlg.no") → End(Aborted), gated by an optional <paramref name="gateB"/> condition.
        /// </summary>
        public static DialogueGraph WithChoice(BaseCondition gateB = null)
        {
            var g = ScriptableObject.CreateInstance<DialogueGraph>();
            var s = new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId };
            var c = new ChoiceNodeData { Id = "c", NodeType = ChoiceNodeData.NodeTypeId };
            c.Choices.Add(new DialogueChoice { Id = "a", DisplayTextKey = "dlg.yes" });
            c.Choices.Add(new DialogueChoice { Id = "b", DisplayTextKey = "dlg.no", Condition = gateB });
            var e1 = new EndNodeData { Id = "e1", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };
            var e2 = new EndNodeData { Id = "e2", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Cancelled };
            g.AddNode(s); g.AddNode(c); g.AddNode(e1); g.AddNode(e2);
            g.AddEdge(new BaseEdgeData { Id = "es", FromNodeId = "s", ToNodeId = "c", PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = "ea", FromNodeId = "c", ToNodeId = "e1", PortName = "a" });
            g.AddEdge(new BaseEdgeData { Id = "eb", FromNodeId = "c", ToNodeId = "e2", PortName = "b" });
            g.EntryNodeId = "s";
            return g;
        }
    }
}
