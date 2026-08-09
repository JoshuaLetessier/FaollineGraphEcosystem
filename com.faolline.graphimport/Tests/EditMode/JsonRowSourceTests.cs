using System.IO;
using NUnit.Framework;

namespace Faolline.GraphImport.Tests
{
    public class JsonRowSourceTests
    {
        static string WriteTemp(string content)
        {
            var path = Path.Combine(Path.GetTempPath(), "graphimport_json_" + System.Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, content);
            return path;
        }

        [Test]
        public void Read_ArrayOfObjects_MapsFieldsToValues()
        {
            var path = WriteTemp(@"[
                { ""ID"": ""Q_001"", ""Nom"": ""Rencontrer Tsuki"" },
                { ""ID"": ""Q_002"", ""Nom"": ""Aller a l'orphelinat"" }
            ]");

            var table = new JsonRowSource().Read(path, "Quetes");

            Assert.AreEqual("Quetes", table.Name);
            Assert.AreEqual(2, table.Rows.Count);
            Assert.AreEqual("Q_001", table.Rows[0].Values["ID"]);
            Assert.AreEqual("Rencontrer Tsuki", table.Rows[0].Values["Nom"]);
            Assert.AreEqual("Q_002", table.Rows[1].Values["ID"]);
        }

        [Test]
        public void Read_NonStringFieldValues_AreCoercedToString()
        {
            var path = WriteTemp(@"[ { ""ID"": ""S_000"", ""Ordre"": 2 } ]");

            var table = new JsonRowSource().Read(path, "Sequence");

            Assert.AreEqual("2", table.Rows[0].Values["Ordre"]);
        }

        [Test]
        public void Read_HeaderIsUnionOfAllObjectKeys()
        {
            var path = WriteTemp(@"[
                { ""ID"": ""Q_001"", ""Nom"": ""A"" },
                { ""ID"": ""Q_002"", ""Chapitres"": ""Everfrost"" }
            ]");

            var table = new JsonRowSource().Read(path, "Quetes");

            CollectionAssert.AreEquivalent(new[] { "ID", "Nom", "Chapitres" }, table.Header);
        }

        [Test]
        public void Read_EmptyArray_ProducesZeroRows()
        {
            var path = WriteTemp("[]");

            var table = new JsonRowSource().Read(path, "Quetes");

            Assert.AreEqual(0, table.Rows.Count);
        }
    }
}
