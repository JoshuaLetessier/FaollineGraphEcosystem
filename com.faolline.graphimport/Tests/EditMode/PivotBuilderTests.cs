using System.Collections.Generic;
using NUnit.Framework;

namespace Faolline.GraphImport.Tests
{
    public class PivotBuilderTests
    {
        [Test]
        public void Build_MapsFieldsAndIgnoresUnmappedColumns()
        {
            var table = new SourceTable("Quetes", new List<string> { "ID", "Nom", "Chapitres", "Statut", "Notes" });
            table.AddRow(new Dictionary<string, string>
            {
                ["ID"] = "Q_001",
                ["Nom"] = "Rencontrer Tsuki",
                ["Chapitres"] = "Everfrost",
                ["Statut"] = "À écrire",
                ["Notes"] = "some production note"
            });

            var tableMapping = new TableMapping("Quetes", "ID", TableRole.Quest,
                new List<FieldMapping>
                {
                    new FieldMapping("name", "Nom"),
                    new FieldMapping("chapter", "Chapitres")
                },
                new List<string> { "Statut", "Notes" },
                new List<ReferenceMapping>());

            var mapping = new MappingConfig(new List<TableMapping> { tableMapping });
            var sourceTables = new Dictionary<string, SourceTable> { ["Quetes"] = table };

            var quests = new PivotBuilder(mapping, new IdOrNameReferenceResolver()).Build(sourceTables);

            Assert.AreEqual(1, quests.Count);
            var quest = quests[0];
            Assert.AreEqual("Q_001", quest.Id);
            Assert.AreEqual("Rencontrer Tsuki", quest.Name);
            Assert.AreEqual("Everfrost", quest.Fields["chapter"]);
            Assert.IsFalse(quest.Fields.ContainsKey("Statut"));
            Assert.IsFalse(quest.Fields.ContainsKey("Notes"));
        }

        [Test]
        public void Build_ResolvesTriggerReference()
        {
            var quetes = new SourceTable("Quetes", new List<string> { "ID", "Nom", "Déclencheur" });
            quetes.AddRow(new Dictionary<string, string> { ["ID"] = "Q_001", ["Nom"] = "Rencontrer Tsuki", ["Déclencheur"] = "" });
            quetes.AddRow(new Dictionary<string, string> { ["ID"] = "Q_004", ["Nom"] = "Convaincre le cheval", ["Déclencheur"] = "Rencontrer Tsuki" });

            var tableMapping = new TableMapping("Quetes", "ID", TableRole.Quest,
                new List<FieldMapping> { new FieldMapping("name", "Nom") },
                new List<string>(),
                new List<ReferenceMapping>
                {
                    new ReferenceMapping("triggeredBy", "Déclencheur", new List<string> { "Quetes" },
                        new List<ReferenceMatchKey> { ReferenceMatchKey.Id, ReferenceMatchKey.Name("Nom") })
                });

            var mapping = new MappingConfig(new List<TableMapping> { tableMapping });
            var sourceTables = new Dictionary<string, SourceTable> { ["Quetes"] = quetes };

            var quests = new PivotBuilder(mapping, new IdOrNameReferenceResolver()).Build(sourceTables);

            var q004 = quests[1];
            Assert.AreEqual(1, q004.References["triggeredBy"].Count);
            Assert.AreEqual("Q_001", q004.References["triggeredBy"][0].TargetId);

            var q001 = quests[0];
            Assert.AreEqual(0, q001.References["triggeredBy"].Count);
        }

        [Test]
        public void BuildContentFields_MapsRowsByIdColumn_FromContentRoleTable()
        {
            var dialogues = new SourceTable("Dialogues", new List<string> { "ID", "Chapitres", "Notes" });
            dialogues.AddRow(new Dictionary<string, string> { ["ID"] = "DLG_006", ["Chapitres"] = "Everfrost", ["Notes"] = "ignored" });
            dialogues.AddRow(new Dictionary<string, string> { ["ID"] = "DLG_008", ["Chapitres"] = "Ashwake", ["Notes"] = "ignored" });

            var tableMapping = new TableMapping("Dialogues", "ID", TableRole.Content,
                new List<FieldMapping> { new FieldMapping("chapter", "Chapitres") },
                new List<string> { "Notes" },
                new List<ReferenceMapping>());

            var mapping = new MappingConfig(new List<TableMapping> { tableMapping });
            var sourceTables = new Dictionary<string, SourceTable> { ["Dialogues"] = dialogues };

            var fields = new PivotBuilder(mapping, new IdOrNameReferenceResolver()).BuildContentFields(sourceTables);

            Assert.AreEqual(2, fields.Count);
            Assert.AreEqual("Everfrost", fields["DLG_006"]["chapter"]);
            Assert.AreEqual("Ashwake", fields["DLG_008"]["chapter"]);
        }

        [Test]
        public void BuildContentFields_IgnoresNonContentRoleTables()
        {
            var quetes = new SourceTable("Quetes", new List<string> { "ID", "Nom" });
            quetes.AddRow(new Dictionary<string, string> { ["ID"] = "Q_001", ["Nom"] = "Rencontrer Tsuki" });

            var tableMapping = new TableMapping("Quetes", "ID", TableRole.Quest,
                new List<FieldMapping> { new FieldMapping("name", "Nom") },
                new List<string>(),
                new List<ReferenceMapping>());

            var mapping = new MappingConfig(new List<TableMapping> { tableMapping });
            var sourceTables = new Dictionary<string, SourceTable> { ["Quetes"] = quetes };

            var fields = new PivotBuilder(mapping, new IdOrNameReferenceResolver()).BuildContentFields(sourceTables);

            Assert.AreEqual(0, fields.Count);
        }

        [Test]
        public void BuildContentFields_OnDuplicateIdAcrossContentTables_FirstDeclarationWins()
        {
            var puzzles = new SourceTable("Puzzles", new List<string> { "ID", "Chapitres" });
            puzzles.AddRow(new Dictionary<string, string> { ["ID"] = "SHARED_001", ["Chapitres"] = "FromPuzzles" });

            var dialogues = new SourceTable("Dialogues", new List<string> { "ID", "Chapitres" });
            dialogues.AddRow(new Dictionary<string, string> { ["ID"] = "SHARED_001", ["Chapitres"] = "FromDialogues" });

            var puzzlesMapping = new TableMapping("Puzzles", "ID", TableRole.Content,
                new List<FieldMapping> { new FieldMapping("chapter", "Chapitres") }, new List<string>(), new List<ReferenceMapping>());
            var dialoguesMapping = new TableMapping("Dialogues", "ID", TableRole.Content,
                new List<FieldMapping> { new FieldMapping("chapter", "Chapitres") }, new List<string>(), new List<ReferenceMapping>());

            var mapping = new MappingConfig(new List<TableMapping> { puzzlesMapping, dialoguesMapping });
            var sourceTables = new Dictionary<string, SourceTable> { ["Puzzles"] = puzzles, ["Dialogues"] = dialogues };

            var fields = new PivotBuilder(mapping, new IdOrNameReferenceResolver()).BuildContentFields(sourceTables);

            Assert.AreEqual("FromPuzzles", fields["SHARED_001"]["chapter"]);
        }
    }
}
