using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Faolline.GraphImport.Tests
{
    public class MappingConfigTests
    {
        const string ValidJson = @"{
            ""tables"": [
                {
                    ""sourceTableName"": ""Quetes"",
                    ""idColumn"": ""ID"",
                    ""role"": ""quest"",
                    ""fields"": [ { ""pivotField"": ""name"", ""sourceColumn"": ""Nom"" } ],
                    ""references"": []
                }
            ]
        }";

        static SourceTable QuetesTable(params string[] extraColumns)
        {
            var header = new List<string> { "ID", "Nom" };
            header.AddRange(extraColumns);
            var table = new SourceTable("Quetes", header);
            var values = new Dictionary<string, string> { ["ID"] = "Q_001", ["Nom"] = "Rencontrer Tsuki" };
            foreach (var c in extraColumns) values[c] = "ignored-value";
            table.AddRow(values);
            return table;
        }

        [Test]
        public void LoadFromJson_ParsesTablesFieldsAndRole()
        {
            var mapping = MappingConfig.LoadFromJson(ValidJson);

            Assert.AreEqual(1, mapping.Tables.Count);
            Assert.AreEqual("Quetes", mapping.Tables[0].SourceTableName);
            Assert.AreEqual("ID", mapping.Tables[0].IdColumn);
            Assert.AreEqual(TableRole.Quest, mapping.Tables[0].Role);
            Assert.AreEqual("name", mapping.Tables[0].Fields[0].PivotField);
            Assert.AreEqual("Nom", mapping.Tables[0].Fields[0].SourceColumn);
        }

        [Test]
        public void Validate_UnmappedColumnsPresent_DoesNotThrow()
        {
            var mapping = MappingConfig.LoadFromJson(ValidJson);
            var table = QuetesTable("Statut", "Notes", "Discord_MsgID");
            var sourceTables = new Dictionary<string, SourceTable> { ["Quetes"] = table };

            Assert.DoesNotThrow(() => mapping.Validate(sourceTables));
        }

        [Test]
        public void Validate_MappedColumnMissingFromSource_ThrowsWithSpecificMessage()
        {
            const string json = @"{
                ""tables"": [
                    {
                        ""sourceTableName"": ""Quetes"",
                        ""idColumn"": ""ID"",
                        ""role"": ""quest"",
                        ""fields"": [ { ""pivotField"": ""name"", ""sourceColumn"": ""NomDeLaQuete"" } ],
                        ""references"": []
                    }
                ]
            }";
            var mapping = MappingConfig.LoadFromJson(json);
            var sourceTables = new Dictionary<string, SourceTable> { ["Quetes"] = QuetesTable() };

            var ex = Assert.Throws<MappingValidationException>(() => mapping.Validate(sourceTables));
            Assert.IsTrue(ex.Errors.Any(e => e.Contains("NomDeLaQuete")));
        }

        [Test]
        public void Validate_DeclaredTableNotProvided_Throws()
        {
            var mapping = MappingConfig.LoadFromJson(ValidJson);
            var sourceTables = new Dictionary<string, SourceTable>();

            var ex = Assert.Throws<MappingValidationException>(() => mapping.Validate(sourceTables));
            Assert.IsTrue(ex.Errors.Any(e => e.Contains("Quetes")));
        }
    }
}
