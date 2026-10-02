using System.Collections.Generic;
using System.Linq;
using Faolline.GraphDialogue;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Faolline.GraphImport.Editor.Tests
{
    public class ProjectAssetResolverTests
    {
        const string ScratchFolder = "Assets/GraphImportTestScratch";
        const string SpeakerFolder = ScratchFolder + "/Speakers";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(ScratchFolder))
                AssetDatabase.CreateFolder("Assets", "GraphImportTestScratch");
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(ScratchFolder);

        static PlanEntry DialogueEntry(string pivotId, string path) =>
            new PlanEntry($"dialogue:{pivotId}", PlanEntryKind.DialogueAsset, path, pivotId, null);

        [Test]
        public void ResolveGraph_TargetInPlanAndOnDisk_ReturnsAsset()
        {
            var path = ScratchFolder + "/DLG_002.asset";
            var target = ScriptableObject.CreateInstance<DialogueGraph>();
            AssetDatabase.CreateAsset(target, path);

            var plan = new GenerationPlan(new List<PlanEntry> { DialogueEntry("DLG_002", path) });
            var resolver = new ProjectAssetResolver(plan, SpeakerFolder);

            var resolved = resolver.ResolveGraph("Dialogues", "DLG_002");

            Assert.AreEqual(target, resolved);
        }

        [Test]
        public void ResolveGraph_TargetNotInPlan_ReturnsNull()
        {
            var plan = new GenerationPlan(new List<PlanEntry>());
            var resolver = new ProjectAssetResolver(plan, SpeakerFolder);

            Assert.IsNull(resolver.ResolveGraph("Dialogues", "DLG_999"));
        }

        [Test]
        public void ResolveGraph_TargetInPlanButNotYetOnDisk_ReturnsNull()
        {
            var plan = new GenerationPlan(new List<PlanEntry> { DialogueEntry("DLG_002", ScratchFolder + "/NeverCreated.asset") });
            var resolver = new ProjectAssetResolver(plan, SpeakerFolder);

            Assert.IsNull(resolver.ResolveGraph("Dialogues", "DLG_002"));
        }

        [Test]
        public void ResolveSpeaker_NoExistingMatch_CreatesNewSpeakerAsset()
        {
            var plan = new GenerationPlan(new List<PlanEntry>());
            var resolver = new ProjectAssetResolver(plan, SpeakerFolder);

            var speaker = resolver.ResolveSpeaker("PNJ_000_Charlie");

            Assert.IsNotNull(speaker);
            Assert.AreEqual("PNJ_000_Charlie", speaker.SpeakerId);
            Assert.IsTrue(AssetDatabase.Contains(speaker));
        }

        [Test]
        public void ResolveSpeaker_ExistingMatch_ReturnsExistingRatherThanCreating()
        {
            var existing = ScriptableObject.CreateInstance<Speaker>();
            existing.SpeakerId = "PNJ_000_Charlie";
            AssetDatabase.CreateAsset(existing, ScratchFolder + "/HandAuthoredCharlie.asset");

            var plan = new GenerationPlan(new List<PlanEntry>());
            var resolver = new ProjectAssetResolver(plan, SpeakerFolder);

            var speaker = resolver.ResolveSpeaker("PNJ_000_Charlie");

            Assert.AreEqual(existing, speaker);
            var allSpeakers = AssetDatabase.FindAssets("t:Speaker", new[] { "Assets" });
            var matching = allSpeakers.Count(guid =>
                AssetDatabase.LoadAssetAtPath<Speaker>(AssetDatabase.GUIDToAssetPath(guid)).SpeakerId == "PNJ_000_Charlie");
            Assert.AreEqual(1, matching, "must not create a duplicate when one already exists");
        }

        [Test]
        public void ResolveSpeaker_CalledTwiceForSameKey_SecondCallReusesFirstCreation()
        {
            var plan = new GenerationPlan(new List<PlanEntry>());
            var resolver = new ProjectAssetResolver(plan, SpeakerFolder);

            var first = resolver.ResolveSpeaker("PNJ_002_Antagoniste");
            var second = resolver.ResolveSpeaker("PNJ_002_Antagoniste");

            Assert.AreEqual(first, second);
        }

        [Test]
        public void ResolveSpeaker_WithMapping_CreatedSpeakerTakesTheMappedGroup()
        {
            var mapping = SpeakerGroupMapping.Parse("SpeakerKey,Table\nPNJ_052_Mapped,Chapitre1\n");
            var resolver = new ProjectAssetResolver(new GenerationPlan(new List<PlanEntry>()), SpeakerFolder, mapping);

            Assert.AreEqual("Chapitre1", resolver.ResolveSpeaker("PNJ_052_Mapped").LocalizationGroup);
            Assert.AreEqual(string.Empty, resolver.ResolveSpeaker("PNJ_052_Unlisted").LocalizationGroup);
        }

        [Test]
        public void ResolveSpeaker_WithoutMapping_CreatedSpeakerHasNoGroup()
        {
            var resolver = new ProjectAssetResolver(new GenerationPlan(new List<PlanEntry>()), SpeakerFolder);
            Assert.AreEqual(string.Empty, resolver.ResolveSpeaker("PNJ_052_NoMap").LocalizationGroup);
        }

        [Test]
        public void ResolveSpeaker_ExistingSpeaker_IsNotModifiedByTheResolver()
        {
            var existing = ScriptableObject.CreateInstance<Speaker>();
            existing.SpeakerId = "PNJ_052_Existing";
            existing.LocalizationGroup = "Old";
            AssetDatabase.CreateAsset(existing, ScratchFolder + "/Existing052.asset");

            var mapping = SpeakerGroupMapping.Parse("SpeakerKey,Table\nPNJ_052_Existing,New\n");
            var resolver = new ProjectAssetResolver(new GenerationPlan(new List<PlanEntry>()), SpeakerFolder, mapping);

            Assert.AreEqual("Old", resolver.ResolveSpeaker("PNJ_052_Existing").LocalizationGroup,
                "realigning existing speakers is SpeakerGroupApplier's job, after the plan is applied");
        }

        [Test]
        public void ResolveSpeaker_CreatesUnderFlatSpeakerFolder()
        {
            var plan = new GenerationPlan(new List<PlanEntry>());
            var resolver = new ProjectAssetResolver(plan, SpeakerFolder);

            var speaker = resolver.ResolveSpeaker("tsuki");

            Assert.AreEqual(SpeakerFolder + "/tsuki.asset", AssetDatabase.GetAssetPath(speaker));
        }
    }
}
