using System;
using System.Collections.Generic;
using System.Linq;
using Faolline.GraphCore;
using Faolline.GraphDialogue.Editor;
using Faolline.GraphLocalization;
using Faolline.GraphLocalization.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>
    /// 054 write-back for dialogues: the table text goes back into the line's Title, the choice option's Title or
    /// the speaker's Display Name Fallback — the texts the adapter seeds the tables with. The table is the
    /// reference; this only brings the graph view in step with it.
    /// </summary>
    public class DialogueGraphTextWriterTests
    {
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();
        private string _folder;
        private string _suffix;

        [SetUp]
        public void SetUp()
        {
            _suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            // "Localization" in the folder name keeps LocalizationAutoBuilder from rebuilding the real tables for
            // these throwaway assets (it skips any path containing it).
            var name = "__GDTextSync_Localization_" + _suffix;
            AssetDatabase.CreateFolder("Assets", name);
            _folder = "Assets/" + name;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) if (o != null && !AssetDatabase.Contains(o)) UnityEngine.Object.DestroyImmediate(o);
            _created.Clear();
            AssetDatabase.DeleteAsset(_folder);
        }

        private DialogueGraph Build(out DialogueLineNodeData line, out BaseChoice option)
        {
            var b = new DialogueGraphBuilder();
            var hi = b.AddLine("guard", "Bonjour").AsEntry();
            var hub = b.AddChoice();
            var end = b.AddEnd();
            hi.To(hub);
            hub.Option("Partir").To(end);
            var graph = b.Build();
            _created.Add(graph);
            line = graph.Nodes.OfType<DialogueLineNodeData>().Single();
            option = graph.Nodes.OfType<ChoiceNodeData>().Single().Choices.Single();
            return graph;
        }

        private static GraphTextDrift Drift(string key, string tableText, string table = "T", string graphGuid = "g")
            => new GraphTextDrift(DialogueLocalizationKeys.LibName, graphGuid, "G", table, key,
                LocalizationKeyType.Text, "", "old", tableText);

        [Test]
        public void LineDrift_WritesTheLineTitle()
        {
            var graph = Build(out var line, out _);

            Assert.IsTrue(new DialogueGraphLocalizationAdapter()
                .WriteGraphText(graph, Drift(DialogueLocalizationKeys.ForLine(line), "Bonjour, voyageur")));

            Assert.AreEqual("Bonjour, voyageur", line.Title);
        }

        [Test]
        public void ChoiceDrift_WritesTheOptionTitle()
        {
            var graph = Build(out var line, out var option);

            Assert.IsTrue(new DialogueGraphLocalizationAdapter()
                .WriteGraphText(graph, Drift(DialogueLocalizationKeys.ForChoice(option), "S'en aller")));

            Assert.AreEqual("S'en aller", option.Title);
            Assert.AreEqual("Bonjour", line.Title, "only the matching text changes");
        }

        [Test]
        public void UnknownKey_WritesNothing()
        {
            var graph = Build(out var line, out var option);

            Assert.IsFalse(new DialogueGraphLocalizationAdapter().WriteGraphText(graph, Drift("line_nope", "x")));

            Assert.AreEqual("Bonjour", line.Title);
            Assert.AreEqual("Partir", option.Title);
        }

        private Speaker CreateSpeaker(string file, string id, string group, string name)
        {
            var s = ScriptableObject.CreateInstance<Speaker>();
            s.SpeakerId = id;
            s.DisplayNameFallback = name;
            s.LocalizationGroup = group;
            AssetDatabase.CreateAsset(s, $"{_folder}/{file}.asset");
            return s;
        }

        [Test]
        public void SpeakerDrift_WritesTheDisplayNameFallback_OfTheSpeakerInThatTable()
        {
            var id = "t" + _suffix + "_mayor";
            var inTable = CreateSpeaker("A", id, "Chapitre1", "Maire");
            var other = CreateSpeaker("B", "t" + _suffix + "_smith", "Chapitre1", "Forgeron");
            var drift = new GraphTextDrift(DialogueLocalizationKeys.LibName, null, null,
                DialogueLocalizationKeys.ForSpeakerTable(inTable), DialogueLocalizationKeys.ForSpeakerId(id),
                LocalizationKeyType.SpeakerName, "", "Maire", "Le Maire");

            Assert.IsTrue(new DialogueGraphLocalizationAdapter().WriteGraphText(drift));

            Assert.AreEqual("Le Maire", inTable.DisplayNameFallback);
            Assert.AreEqual("Forgeron", other.DisplayNameFallback);
        }

        [Test]
        public void SpeakerDrift_ForAnotherGroupTable_WritesNothing()
        {
            var id = "t" + _suffix + "_mayor";
            var speaker = CreateSpeaker("A", id, "Chapitre1", "Maire");
            var drift = new GraphTextDrift(DialogueLocalizationKeys.LibName, null, null,
                DialogueLocalizationKeys.ForSpeakerGroupTable("Chapitre2"), DialogueLocalizationKeys.ForSpeakerId(id),
                LocalizationKeyType.SpeakerName, "", "Maire", "Le Maire");

            Assert.IsFalse(new DialogueGraphLocalizationAdapter().WriteGraphText(drift));
            Assert.AreEqual("Maire", speaker.DisplayNameFallback);
        }

        [Test]
        public void ScanCompareApply_RoundTrip_OnAGraphAsset()
        {
            var graph = Build(out var line, out _);
            graph.name = "TextSync_" + _suffix;
            AssetDatabase.CreateAsset(graph, $"{_folder}/{graph.name}.asset");
            var key = DialogueLocalizationKeys.ForLine(line);
            var adapter = new DialogueGraphLocalizationAdapter();

            var db = new LocalizationDatabase();
            adapter.ScanAndIndex(db);
            var drifts = GraphTextSync.Compare(adapter.LibName, db,
                new Dictionary<string, string> { [key] = "Bonjour, voyageur" });

            var drift = drifts.Single(d => d.Key == key);
            Assert.AreEqual(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(graph)), drift.GraphGuid);
            Assert.AreEqual(1, GraphTextSync.ApplyAll(new[] { drift }, new IGraphLocalizationAdapter[] { adapter }, save: false));
            Assert.AreEqual("Bonjour, voyageur", line.Title);
            Assert.IsTrue(EditorUtility.IsDirty(graph), "the asset is marked for saving");
        }
    }
}
