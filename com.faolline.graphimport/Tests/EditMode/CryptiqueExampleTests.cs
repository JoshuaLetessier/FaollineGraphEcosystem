using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Faolline.GraphImport.Tests
{
    /// <summary>
    /// Runs the shipped sample (Samples/CryptiqueExample) end to end through mapping -> pivot -> plan,
    /// as a regression guard against the sample and mapping.json drifting apart — and as the
    /// executable form of quickstart.md's walkthrough (T034).
    /// </summary>
    public class CryptiqueExampleTests
    {
        static string SampleRoot => PackageRoot.Combine("Samples", "CryptiqueExample");

        static IReadOnlyDictionary<string, SourceTable> LoadSourceTables(MappingConfig mapping)
        {
            var tables = new Dictionary<string, SourceTable>();
            foreach (var table in mapping.Tables)
            {
                var path = Path.Combine(SampleRoot, table.SourceTableName + ".csv");
                tables[table.SourceTableName] = new CsvRowSource().Read(path, table.SourceTableName);
            }
            return tables;
        }

        [Test]
        public void Sample_MappingValidatesAgainstShippedCsvs()
        {
            var mapping = MappingConfig.LoadFromJson(File.ReadAllText(Path.Combine(SampleRoot, "mapping.json")));
            var tables = LoadSourceTables(mapping);

            Assert.DoesNotThrow(() => mapping.Validate(tables));
        }

        [Test]
        public void Sample_PivotBuilds_OnlyQuestWithStepsGetsAFlowEntry()
        {
            var mapping = MappingConfig.LoadFromJson(File.ReadAllText(Path.Combine(SampleRoot, "mapping.json")));
            var tables = LoadSourceTables(mapping);
            mapping.Validate(tables);

            var quests = new PivotBuilder(mapping, new IdOrNameReferenceResolver()).Build(tables);
            Assert.AreEqual(4, quests.Count);

            var q001 = quests.Single(q => q.Id == "Q_001");
            Assert.AreEqual(3, q001.Steps.Count);
            Assert.AreEqual("Puzzles", q001.Steps[0].ContentRef.TargetTable);

            var pathResolver = new TemplatePathResolver(new Dictionary<PlanEntryKind, string>
            {
                [PlanEntryKind.QuestAsset] = "Assets/Graphs/{chapter}/Quests/{name}.asset",
                [PlanEntryKind.FlowAsset] = "Assets/Graphs/{chapter}/GameFlow/{name}.asset"
            });
            var plan = new PlanBuilder(pathResolver).Build(quests);

            Assert.AreEqual(4, plan.Entries.Count(e => e.Kind == PlanEntryKind.QuestAsset));
            Assert.AreEqual(1, plan.Entries.Count(e => e.Kind == PlanEntryKind.FlowAsset));
            Assert.IsTrue(plan.Entries.Any(e => e.Kind == PlanEntryKind.FlowAsset && e.SourcePivotId == "Q_001"));
        }
    }
}
