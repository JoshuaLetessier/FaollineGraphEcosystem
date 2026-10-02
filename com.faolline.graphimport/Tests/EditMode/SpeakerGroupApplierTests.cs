using System;
using Faolline.GraphDialogue;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Faolline.GraphImport.Editor.Tests
{
    /// <summary>
    /// 052 US4: after an import, every EXISTING speaker referenced by the imported dialogues is aligned with the
    /// mapping (the mapping wins); unlisted speakers are untouched; each change is reported.
    /// </summary>
    public class SpeakerGroupApplierTests
    {
        const string ScratchFolder = "Assets/GraphImportTestScratch";
        private string _suffix;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(ScratchFolder))
                AssetDatabase.CreateFolder("Assets", "GraphImportTestScratch");
            _suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(ScratchFolder);

        private string Id(string local) => $"{local}_{_suffix}";

        private string CreateSpeaker(string local, string group)
        {
            var s = ScriptableObject.CreateInstance<Speaker>();
            s.SpeakerId = Id(local);
            s.LocalizationGroup = group;
            var path = $"{ScratchFolder}/{Id(local)}.asset";
            AssetDatabase.CreateAsset(s, path);
            return path;
        }

        private SpeakerGroupMapping Mapping(params (string local, string group)[] rows)
        {
            var csv = "SpeakerKey,Table\n";
            foreach (var (local, group) in rows) csv += $"{Id(local)},{group}\n";
            return SpeakerGroupMapping.Parse(csv);
        }

        private static string GroupAt(string path)
            => AssetDatabase.LoadAssetAtPath<Speaker>(path).LocalizationGroup;

        [Test]
        public void ListedSpeakerWithAnotherGroup_IsUpdated_AndReported()
        {
            var path = CreateSpeaker("a", "Chapitre1");

            var changes = SpeakerGroupApplier.Apply(new[] { Id("a") }, Mapping(("a", "Chapitre2")));

            Assert.AreEqual("Chapitre2", GroupAt(path));
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(Id("a"), changes[0].SpeakerKey);
            Assert.AreEqual(path, changes[0].AssetPath);
            Assert.AreEqual("Chapitre1", changes[0].OldGroup);
            Assert.AreEqual("Chapitre2", changes[0].NewGroup);
        }

        [Test]
        public void SameGroup_IsNoChange()
        {
            CreateSpeaker("a", "Chapitre1");
            Assert.AreEqual(0, SpeakerGroupApplier.Apply(new[] { Id("a") }, Mapping(("a", " Chapitre1 "))).Count);
        }

        [Test]
        public void EmptyMappedGroup_ClearsTheGroup()
        {
            var path = CreateSpeaker("a", "Chapitre1");
            SpeakerGroupApplier.Apply(new[] { Id("a") }, Mapping(("a", "")));
            Assert.AreEqual(string.Empty, GroupAt(path));
        }

        [Test]
        public void UnlistedSpeaker_IsUntouched()
        {
            var path = CreateSpeaker("a", "Chapitre1");
            Assert.AreEqual(0, SpeakerGroupApplier.Apply(new[] { Id("a") }, Mapping(("other", "Chapitre9"))).Count);
            Assert.AreEqual("Chapitre1", GroupAt(path));
        }

        [Test]
        public void KeyWithoutAsset_IsIgnored()
            => Assert.AreEqual(0, SpeakerGroupApplier.Apply(new[] { Id("ghost") }, Mapping(("ghost", "C1"))).Count);

        [Test]
        public void DuplicateKeys_AreProcessedOnce()
        {
            CreateSpeaker("a", "Chapitre1");
            Assert.AreEqual(1, SpeakerGroupApplier.Apply(new[] { Id("a"), Id("a") }, Mapping(("a", "Chapitre2"))).Count);
        }

        [Test]
        public void ListedButUnreferencedSpeaker_IsUntouched()
        {
            var path = CreateSpeaker("a", "Chapitre1");
            SpeakerGroupApplier.Apply(new string[0], Mapping(("a", "Chapitre2")));
            Assert.AreEqual("Chapitre1", GroupAt(path));
        }
    }
}
