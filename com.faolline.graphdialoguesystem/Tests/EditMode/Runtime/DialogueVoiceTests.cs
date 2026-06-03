using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphDialogue;
using Faolline.GraphLocalization;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>Line audio is resolved by the line key from the localized asset provider (no per-node clip).</summary>
    public class DialogueVoiceTests
    {
        private sealed class FakeAssets : ILocalizedAssetProvider
        {
            public readonly Dictionary<string, Object> Map = new Dictionary<string, Object>();
            public T ResolveAsset<T>(string key) where T : Object
                => Map.TryGetValue(key, out var a) ? a as T : null;
        }

        // Start → l → End
        private static DialogueGraph BuildGraph()
        {
            var g = ScriptableObject.CreateInstance<DialogueGraph>();
            var s = new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId };
            var l = new DialogueLineNodeData { Id = "l", NodeType = DialogueLineNodeData.NodeTypeId };
            var e = new EndNodeData { Id = "e", NodeType = EndNodeData.NodeTypeId };
            g.AddNode(s); g.AddNode(l); g.AddNode(e);
            g.EntryNodeId = "s";
            g.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "l", PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "l", ToNodeId = "e", PortName = "out" });
            return g;
        }

        [Test]
        public void LineStep_ResolvesLocalizedVoiceByKey()
        {
            var clip = AudioClip.Create("loc", 64, 1, 44100, false);
            var g = BuildGraph();
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
        public void LineStep_HasNoVoice_WhenNoAssetProvider()
        {
            var g = BuildGraph();
            var player = new DialoguePlayer(g, new DialogueContext(),
                new CsvLocalizationProvider("Key,en\nline_l,Hi\n", "en"));
            LineStep line = null;
            player.OnLine += s => line = s;
            try
            {
                player.Start();
                Assert.IsNull(line.VoiceClip);
            }
            finally { Object.DestroyImmediate(g); }
        }
    }
}
