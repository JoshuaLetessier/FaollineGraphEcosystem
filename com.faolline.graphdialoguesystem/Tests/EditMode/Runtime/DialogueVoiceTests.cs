using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphDialogue;
using Faolline.GraphLocalization;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>A line node's authored VoiceClip flows through the player onto the emitted LineStep.</summary>
    public class DialogueVoiceTests
    {
        [Test]
        public void LineStep_CarriesNodeVoiceClip()
        {
            var clip = AudioClip.Create("voice", 64, 1, 44100, false);
            var g = ScriptableObject.CreateInstance<DialogueGraph>();
            var s = new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId };
            var l = new DialogueLineNodeData { Id = "l", NodeType = DialogueLineNodeData.NodeTypeId, VoiceClip = clip };
            var e = new EndNodeData { Id = "e", NodeType = EndNodeData.NodeTypeId };
            g.AddNode(s); g.AddNode(l); g.AddNode(e);
            g.EntryNodeId = "s";
            g.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "l", PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "l", ToNodeId = "e", PortName = "out" });

            var player = new DialoguePlayer(g, new DialogueContext(), new CsvLocalizationProvider("Key,en\nline_l,Hi\n", "en"));
            LineStep line = null;
            player.OnLine += s2 => line = s2;
            try
            {
                player.Start();
                Assert.IsNotNull(line);
                Assert.AreSame(clip, line.VoiceClip);
            }
            finally
            {
                Object.DestroyImmediate(g);
                Object.DestroyImmediate(clip);
            }
        }
    }
}
