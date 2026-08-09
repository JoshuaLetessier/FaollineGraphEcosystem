using System.Collections.Generic;
using NUnit.Framework;

namespace Faolline.GraphImport.Editor.Tests
{
    public class PlanApplierTests
    {
        sealed class RecordingGenerator : IAssetGenerator
        {
            public readonly List<string> GeneratedLogicalIds = new List<string>();
            public void Generate(PlanEntry entry) => GeneratedLogicalIds.Add(entry.LogicalId);
        }

        static PlanEntry Entry(string logicalId, string path) =>
            new PlanEntry(logicalId, PlanEntryKind.QuestAsset, path, logicalId, null);

        [Test]
        public void Apply_NoConflicts_CreatesEveryEntry()
        {
            var plan = new GenerationPlan(new List<PlanEntry>
            {
                Entry("quest:Q_001", "Assets/A.asset"),
                Entry("quest:Q_004", "Assets/B.asset")
            });
            var report = new ConflictReport(new List<ConflictEntry>());
            var generator = new RecordingGenerator();

            var created = PlanApplier.Apply(plan, report, new Dictionary<PlanEntryKind, IAssetGenerator> { [PlanEntryKind.QuestAsset] = generator });

            Assert.AreEqual(2, created.Count);
            CollectionAssert.AreEquivalent(new[] { "quest:Q_001", "quest:Q_004" }, generator.GeneratedLogicalIds);
        }

        [Test]
        public void Apply_ConflictingEntry_IsNeverGeneratedAndNotReturned()
        {
            var conflicting = Entry("quest:Q_001", "Assets/Existing.asset");
            var clean = Entry("quest:Q_004", "Assets/B.asset");
            var plan = new GenerationPlan(new List<PlanEntry> { conflicting, clean });
            var report = new ConflictReport(new List<ConflictEntry>
            {
                new ConflictEntry(conflicting, conflicting.ProposedPath, ConflictReason.AlreadyExists)
            });
            var generator = new RecordingGenerator();

            var created = PlanApplier.Apply(plan, report, new Dictionary<PlanEntryKind, IAssetGenerator> { [PlanEntryKind.QuestAsset] = generator });

            Assert.AreEqual(1, created.Count);
            Assert.AreEqual("quest:Q_004", created[0].LogicalId);
            CollectionAssert.DoesNotContain(generator.GeneratedLogicalIds, "quest:Q_001");
        }
    }
}
