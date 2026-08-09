using System.IO;
using NUnit.Framework;

namespace Faolline.GraphImport.Tests
{
    public class CsvRowSourceTests
    {
        static string WriteTemp(string content)
        {
            var path = Path.Combine(Path.GetTempPath(), "graphimport_csv_" + System.Guid.NewGuid().ToString("N") + ".csv");
            File.WriteAllText(path, content);
            return path;
        }

        [Test]
        public void Read_PlainRows_MapsHeaderToValues()
        {
            var path = WriteTemp("ID,Nom\nQ_001,Rencontrer Tsuki\nQ_002,Aller a l'orphelinat\n");

            var table = new CsvRowSource().Read(path, "Quetes");

            Assert.AreEqual("Quetes", table.Name);
            Assert.AreEqual(new[] { "ID", "Nom" }, table.Header);
            Assert.AreEqual(2, table.Rows.Count);
            Assert.AreEqual("Q_001", table.Rows[0].Values["ID"]);
            Assert.AreEqual("Rencontrer Tsuki", table.Rows[0].Values["Nom"]);
            Assert.AreEqual("Q_002", table.Rows[1].Values["ID"]);
        }

        [Test]
        public void Read_QuotedFieldWithEmbeddedComma_IsKeptAsOneValue()
        {
            var path = WriteTemp("ID,Notes\nQ_001,\"Trouver et rapporter l'objet, puis revenir\"\n");

            var table = new CsvRowSource().Read(path, "Quetes");

            Assert.AreEqual("Trouver et rapporter l'objet, puis revenir", table.Rows[0].Values["Notes"]);
        }

        [Test]
        public void Read_QuotedFieldWithEmbeddedNewline_IsKeptAsOneValue()
        {
            var path = WriteTemp("ID,Notes\nQ_001,\"Ligne 1\nLigne 2\"\nQ_002,Simple\n");

            var table = new CsvRowSource().Read(path, "Quetes");

            Assert.AreEqual(2, table.Rows.Count);
            Assert.AreEqual("Ligne 1\nLigne 2", table.Rows[0].Values["Notes"]);
            Assert.AreEqual("Simple", table.Rows[1].Values["Notes"]);
        }

        [Test]
        public void Read_EscapedQuoteInsideQuotedField_IsUnescaped()
        {
            var path = WriteTemp("ID,Notes\nQ_001,\"il dit \"\"bonjour\"\"\"\n");

            var table = new CsvRowSource().Read(path, "Quetes");

            Assert.AreEqual("il dit \"bonjour\"", table.Rows[0].Values["Notes"]);
        }

        [Test]
        public void Read_EmptyDataSection_ProducesZeroRows()
        {
            var path = WriteTemp("ID,Nom\n");

            var table = new CsvRowSource().Read(path, "Quetes");

            Assert.AreEqual(0, table.Rows.Count);
        }
    }
}
