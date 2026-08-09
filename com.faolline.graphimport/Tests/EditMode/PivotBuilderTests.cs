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
    }
}
