using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

namespace Faolline.GraphImport.Editor.Tests
{
    public class PlanApplierTests
    {
        const string ScratchFolder = "Assets/GraphImportTestScratch";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(ScratchFolder))
                AssetDatabase.CreateFolder("Assets", "GraphImportTestScratch");
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(ScratchFolder);

        sealed class RecordingGenerator : IAssetGenerator
        {
            public readonly List<string> GeneratedLogicalIds = new List<string>();
            public void Generate(PlanEntry entry) => GeneratedLogicalIds.Add(entry.LogicalId);
        }

        sealed class ThrowingGenerator : IAssetGenerator
        {
            public readonly HashSet<string> FailOn;
            public ThrowingGenerator(params string[] failOn) => FailOn = new HashSet<string>(failOn);
            public void Generate(PlanEntry entry)
            {
                if (FailOn.Contains(entry.LogicalId))
                    throw new InvalidOperationException($"boom: {entry.LogicalId}");
            }
        }

        static PlanEntry Entry(string logicalId, string path) =>
            new PlanEntry(logicalId, PlanEntryKind.DialogueAsset, path, logicalId, null);

        [Test]
        public void Apply_NoConflicts_CreatesEveryEntry()
        {
            var plan = new GenerationPlan(new List<PlanEntry>
            {
                Entry("dialogue:DLG_001", "Assets/A.asset"),
                Entry("dialogue:DLG_004", "Assets/B.asset")
            });
            var report = new ConflictReport(new List<ConflictEntry>());
            var generator = new RecordingGenerator();

            var result = PlanApplier.Apply(plan, report, new Dictionary<PlanEntryKind, IAssetGenerator> { [PlanEntryKind.DialogueAsset] = generator });

            Assert.AreEqual(2, result.Created.Count);
            Assert.IsTrue(result.IsClean);
            CollectionAssert.AreEquivalent(new[] { "dialogue:DLG_001", "dialogue:DLG_004" }, generator.GeneratedLogicalIds);
        }

        [Test]
        public void Apply_ConflictingEntry_IsNeverGeneratedAndNotReturned()
        {
            var conflicting = Entry("dialogue:DLG_001", "Assets/Existing.asset");
            var clean = Entry("dialogue:DLG_004", "Assets/B.asset");
            var plan = new GenerationPlan(new List<PlanEntry> { conflicting, clean });
            var report = new ConflictReport(new List<ConflictEntry>
            {
                new ConflictEntry(conflicting, conflicting.ProposedPath, ConflictReason.AlreadyExists)
            });
            var generator = new RecordingGenerator();

            var result = PlanApplier.Apply(plan, report, new Dictionary<PlanEntryKind, IAssetGenerator> { [PlanEntryKind.DialogueAsset] = generator });

            Assert.AreEqual(1, result.Created.Count);
            Assert.AreEqual("dialogue:DLG_004", result.Created[0].LogicalId);
            CollectionAssert.DoesNotContain(generator.GeneratedLogicalIds, "dialogue:DLG_001");
        }

        [Test]
        public void Apply_DestinationFolderDoesNotExist_IsCreatedBeforeGenerating()
        {
            var deepPath = ScratchFolder + "/Nested/Deeper/DLG_001.asset";
            var plan = new GenerationPlan(new List<PlanEntry> { Entry("dialogue:DLG_001", deepPath) });
            var report = new ConflictReport(new List<ConflictEntry>());
            var generator = new RecordingGenerator();

            Assert.IsFalse(AssetDatabase.IsValidFolder(ScratchFolder + "/Nested/Deeper"));

            var result = PlanApplier.Apply(plan, report, new Dictionary<PlanEntryKind, IAssetGenerator> { [PlanEntryKind.DialogueAsset] = generator });

            Assert.IsTrue(AssetDatabase.IsValidFolder(ScratchFolder + "/Nested/Deeper"));
            Assert.AreEqual(1, result.Created.Count);
        }

        [Test]
        public void Apply_OneEntryGeneratorThrows_OthersStillSucceedAndFailureIsReported()
        {
            var failing = Entry("dialogue:DLG_001", ScratchFolder + "/A.asset");
            var clean = Entry("dialogue:DLG_004", ScratchFolder + "/B.asset");
            var plan = new GenerationPlan(new List<PlanEntry> { failing, clean });
            var report = new ConflictReport(new List<ConflictEntry>());
            var generator = new ThrowingGenerator("dialogue:DLG_001");

            var result = PlanApplier.Apply(plan, report, new Dictionary<PlanEntryKind, IAssetGenerator> { [PlanEntryKind.DialogueAsset] = generator });

            Assert.IsFalse(result.IsClean);
            Assert.AreEqual(1, result.Created.Count);
            Assert.AreEqual("dialogue:DLG_004", result.Created[0].LogicalId);
            Assert.AreEqual(1, result.Failures.Count);
            Assert.AreEqual("dialogue:DLG_001", result.Failures[0].Entry.LogicalId);
            StringAssert.Contains("boom", result.Failures[0].Exception.Message);
        }
    }
}
