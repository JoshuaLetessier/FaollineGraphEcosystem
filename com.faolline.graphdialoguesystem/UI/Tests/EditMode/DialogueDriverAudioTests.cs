using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphDialogue;
using Faolline.GraphDialogue.UI;
using Faolline.GraphLocalization;

namespace Faolline.GraphDialogue.UI.Tests
{
    /// <summary>The driver routes a line's VoiceClip to its configured AudioSource.</summary>
    public class DialogueDriverAudioTests
    {
        [Test]
        public void StartDialogue_AssignsVoiceClipToAudioSource()
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

            var go = new GameObject("driver");
            var driver = go.AddComponent<DialogueDriver>();
            var source = go.AddComponent<AudioSource>();
            driver.View = new RecordingDialogueView();
            driver.Provider = new CsvLocalizationProvider("Key,en\nline_l,Hi\n", "en");
            driver.ConfigureAudioForTest(source);
            try
            {
                driver.StartDialogue(g);
                Assert.AreSame(clip, source.clip, "Driver should assign the line's VoiceClip to the AudioSource.");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(g);
                Object.DestroyImmediate(clip);
            }
        }
    }
}
