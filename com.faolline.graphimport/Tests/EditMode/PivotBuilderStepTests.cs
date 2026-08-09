using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Faolline.GraphImport.Tests
{
    public class PivotBuilderStepTests
    {
        static (MappingConfig mapping, Dictionary<string, SourceTable> sourceTables) BuildFixture()
        {
            var quetes = new SourceTable("Quetes", new List<string> { "ID", "Nom" });
            quetes.AddRow(new Dictionary<string, string> { ["ID"] = "Q_001", ["Nom"] = "Rencontrer Tsuki" });

            var puzzles = new SourceTable("Puzzles", new List<string> { "ID", "Nom" });
            puzzles.AddRow(new Dictionary<string, string> { ["ID"] = "PZ_000", ["Nom"] = "jeu de dé" });

            var sequence = new SourceTable("Sequence", new List<string> { "ID", "Quête (ID)", "Ordre", "Référence_ID", "Signal" });
            sequence.AddRow(new Dictionary<string, string> { ["ID"] = "S_000", ["Quête (ID)"] = "Q_001", ["Ordre"] = "0", ["Référence_ID"] = "PZ_000", ["Signal"] = "" });

            var questMapping = new TableMapping("Quetes", "ID", TableRole.Quest,
                new List<FieldMapping> { new FieldMapping("name", "Nom") }, new List<string>(), new List<ReferenceMapping>());

            var puzzlesMapping = new TableMapping("Puzzles", "ID", TableRole.Content,
                new List<FieldMapping> { new FieldMapping("name", "Nom") }, new List<string>(), new List<ReferenceMapping>());

            var stepMapping = new TableMapping("Sequence", "ID", TableRole.Step,
                new List<FieldMapping>
                {
                    new FieldMapping("order", "Ordre"),
                    new FieldMapping("branchOutcome", "Signal")
                },
                new List<string>(),
                new List<ReferenceMapping>
                {
                    new ReferenceMapping("quest", "Quête (ID)", new List<string> { "Quetes" }, new List<ReferenceMatchKey> { ReferenceMatchKey.Id }),
                    new ReferenceMapping("content", "Référence_ID", new List<string> { "Puzzles" }, new List<ReferenceMatchKey> { ReferenceMatchKey.Id, ReferenceMatchKey.Name("Nom") })
                });

            var mapping = new MappingConfig(new List<TableMapping> { questMapping, puzzlesMapping, stepMapping });
            var sourceTables = new Dictionary<string, SourceTable> { ["Quetes"] = quetes, ["Puzzles"] = puzzles, ["Sequence"] = sequence };
            return (mapping, sourceTables);
        }

        [Test]
        public void Build_StepReferencesContentInsteadOfInliningIt()
        {
            var (mapping, sourceTables) = BuildFixture();

            var quests = new PivotBuilder(mapping, new IdOrNameReferenceResolver()).Build(sourceTables);

            var quest = quests.Single();
            Assert.AreEqual(1, quest.Steps.Count);
            var step = quest.Steps[0];
            Assert.AreEqual(0, step.Order);
            Assert.IsNotNull(step.ContentRef);
            Assert.AreEqual("Puzzles", step.ContentRef.TargetTable);
            Assert.AreEqual("PZ_000", step.ContentRef.TargetId);
        }

        [Test]
        public void Build_StepsOrderedByPosition()
        {
            var (mapping, sourceTables) = BuildFixture();
            sourceTables["Sequence"].AddRow(new Dictionary<string, string>
            {
                ["ID"] = "S_001", ["Quête (ID)"] = "Q_001", ["Ordre"] = "1", ["Référence_ID"] = "PZ_000", ["Signal"] = ""
            });

            var quests = new PivotBuilder(mapping, new IdOrNameReferenceResolver()).Build(sourceTables);

            var steps = quests.Single().Steps;
            Assert.AreEqual(2, steps.Count);
            Assert.AreEqual(0, steps[0].Order);
            Assert.AreEqual(1, steps[1].Order);
        }

        static (MappingConfig mapping, Dictionary<string, SourceTable> sourceTables) BuildFixtureWithOrderValue(string rawOrder)
        {
            var quetes = new SourceTable("Quetes", new List<string> { "ID", "Nom" });
            quetes.AddRow(new Dictionary<string, string> { ["ID"] = "Q_001", ["Nom"] = "Rencontrer Tsuki" });

            var puzzles = new SourceTable("Puzzles", new List<string> { "ID", "Nom" });
            puzzles.AddRow(new Dictionary<string, string> { ["ID"] = "PZ_000", ["Nom"] = "jeu de dé" });

            var sequence = new SourceTable("Sequence", new List<string> { "ID", "Quête (ID)", "Ordre", "Référence_ID", "Signal" });
            sequence.AddRow(new Dictionary<string, string> { ["ID"] = "S_000", ["Quête (ID)"] = "Q_001", ["Ordre"] = rawOrder, ["Référence_ID"] = "PZ_000", ["Signal"] = "" });

            var questMapping = new TableMapping("Quetes", "ID", TableRole.Quest,
                new List<FieldMapping> { new FieldMapping("name", "Nom") }, new List<string>(), new List<ReferenceMapping>());
            var puzzlesMapping = new TableMapping("Puzzles", "ID", TableRole.Content,
                new List<FieldMapping> { new FieldMapping("name", "Nom") }, new List<string>(), new List<ReferenceMapping>());
            var stepMapping = new TableMapping("Sequence", "ID", TableRole.Step,
                new List<FieldMapping> { new FieldMapping("order", "Ordre"), new FieldMapping("branchOutcome", "Signal") },
                new List<string>(),
                new List<ReferenceMapping>
                {
                    new ReferenceMapping("quest", "Quête (ID)", new List<string> { "Quetes" }, new List<ReferenceMatchKey> { ReferenceMatchKey.Id }),
                    new ReferenceMapping("content", "Référence_ID", new List<string> { "Puzzles" }, new List<ReferenceMatchKey> { ReferenceMatchKey.Id, ReferenceMatchKey.Name("Nom") })
                });

            var mapping = new MappingConfig(new List<TableMapping> { questMapping, puzzlesMapping, stepMapping });
            var sourceTables = new Dictionary<string, SourceTable> { ["Quetes"] = quetes, ["Puzzles"] = puzzles, ["Sequence"] = sequence };
            return (mapping, sourceTables);
        }

        [Test]
        public void Build_OrderExportedAsFloat_IsParsedAsInteger()
        {
            // Real-world case: a spreadsheet column typed as float exports whole numbers as "2.0".
            var (mapping, sourceTables) = BuildFixtureWithOrderValue("2.0");

            var quests = new PivotBuilder(mapping, new IdOrNameReferenceResolver()).Build(sourceTables);

            Assert.AreEqual(2, quests.Single().Steps[0].Order);
        }

        [Test]
        public void Build_OrderNotNumeric_ThrowsWithRowContext()
        {
            var (mapping, sourceTables) = BuildFixtureWithOrderValue("deux");

            var ex = Assert.Throws<PivotFieldParseException>(() => new PivotBuilder(mapping, new IdOrNameReferenceResolver()).Build(sourceTables));
            Assert.AreEqual("Sequence", ex.SourceTable.Name);
            Assert.AreEqual(1, ex.SourceRowIndex);
            Assert.AreEqual("Ordre", ex.SourceColumn);
        }
    }
}
