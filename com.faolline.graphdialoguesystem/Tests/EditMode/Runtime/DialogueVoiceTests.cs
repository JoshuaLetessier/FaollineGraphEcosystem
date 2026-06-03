using System.Collections.Generic;
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
        private sealed class FakeAssets : ILocalizedAssetProvider
        {
            public readonly Dictionary<string, Object> Map = new Dictionary<string, Object>();
            public T ResolveAsset<T>(string key) where T : Object
                => Map.TryGetValue(key, out var a) ? a as T : null;
        }

        // Start → l → End (no per-node clip)
        private static DialogueGraph BuildGraph(out DialogueLineNodeData line)
        {
            var g = ScriptableObject.CreateInstance<DialogueGraph>();
            var s = new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId };
            line = new DialogueLineNodeData { Id = "l", NodeType = DialogueLineNodeData.NodeTypeId };
            var e = new EndNodeData { Id = "e", NodeType = EndNodeData.NodeTypeId };
            g.AddNode(s); g.AddNode(line); g.AddNode(e);
            g.EntryNodeId = "s";
            g.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "l", PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "l", ToNodeId = "e", PortName = "out" });
            return g;
        }
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

        [Test]
        public void LineStep_ResolvesLocalizedVoiceByKey_WhenNoNodeClip()
        {
            var clip = AudioClip.Create("loc", 64, 1, 44100, false);
            var g = BuildGraph(out _);
            var assets = new FakeAssets();
            assets.Map["line_l"] = clip; // keyed like the text

            var player = new DialoguePlayer(g, new DialogueContext(),
                new CsvLocalizationProvider("Key,en\nline_l,Hi\n", "en"), null,
                LocalizationStrictMode.Permissive, assets);
            LineStep line = null;
            player.OnLine += s => line = s;
            try
            {
                player.Start();
                Assert.AreSame(clip, line.VoiceClip, "Voice should be resolved by the line key from the asset provider.");
            }
            finally { Object.DestroyImmediate(g); Object.DestroyImmediate(clip); }
        }

        [Test]
        public void NodeClip_OverridesLocalizedVoice()
        {
            var nodeClip = AudioClip.Create("node", 64, 1, 44100, false);
            var locClip = AudioClip.Create("loc", 64, 1, 44100, false);
            var g = BuildGraph(out var lineNode);
            lineNode.VoiceClip = nodeClip;
            var assets = new FakeAssets();
            assets.Map["line_l"] = locClip;

            var player = new DialoguePlayer(g, new DialogueContext(),
                new CsvLocalizationProvider("Key,en\nline_l,Hi\n", "en"), null,
                LocalizationStrictMode.Permissive, assets);
            LineStep line = null;
            player.OnLine += s => line = s;
            try
            {
                player.Start();
                Assert.AreSame(nodeClip, line.VoiceClip, "Per-node clip must win over the localized asset.");
            }
            finally { Object.DestroyImmediate(g); Object.DestroyImmediate(nodeClip); Object.DestroyImmediate(locClip); }
        }
    }
}
