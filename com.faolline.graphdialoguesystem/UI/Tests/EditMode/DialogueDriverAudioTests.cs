using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphDialogue;
using Faolline.GraphDialogue.UI;
using Faolline.GraphLocalization;

namespace Faolline.GraphDialogue.UI.Tests
{
    /// <summary>The driver plays the line's localized voice (resolved by key) on its AudioSource.</summary>
    public class DialogueDriverAudioTests
    {
        private sealed class FakeAssets : ILocalizedAssetProvider
        {
            public readonly Dictionary<string, Object> Map = new Dictionary<string, Object>();
            public T ResolveAsset<T>(string key) where T : Object
                => Map.TryGetValue(key, out var a) ? a as T : null;
        }

        [TearDown]
        public void TearDown() => LocalizationContext.Current = null;

        [Test]
        public void StartDialogue_PlaysLocalizedVoiceOnAudioSource()
        {
            var clip = AudioClip.Create("voice", 64, 1, 44100, false);
            var g = ScriptableObject.CreateInstance<DialogueGraph>();
            var s = new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId };
            var l = new DialogueLineNodeData { Id = "l", NodeType = DialogueLineNodeData.NodeTypeId };
            var e = new EndNodeData { Id = "e", NodeType = EndNodeData.NodeTypeId };
            g.AddNode(s); g.AddNode(l); g.AddNode(e);
            g.EntryNodeId = "s";
            g.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "l", PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "l", ToNodeId = "e", PortName = "out" });

            var assets = new FakeAssets();
            assets.Map["line_l"] = clip;
            LocalizationContext.Current = new LocalizationSettings(
                new CsvLocalizationProvider("Key,en\nline_l,Hi\n", "en"), "en") { AssetProvider = assets };

            var go = new GameObject("driver");
            var driver = go.AddComponent<DialogueDriver>();
            var source = go.AddComponent<AudioSource>();
            driver.View = new RecordingDialogueView();
            driver.ConfigureAudioForTest(source);
            try
            {
                driver.StartDialogue(g);
                Assert.AreSame(clip, source.clip, "Driver should assign the localized voice clip to the AudioSource.");
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
