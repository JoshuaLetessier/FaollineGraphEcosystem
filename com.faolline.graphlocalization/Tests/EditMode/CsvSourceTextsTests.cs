using System;
using System.IO;
using Faolline.GraphLocalization.Editor;
using NUnit.Framework;

namespace Faolline.GraphLocalization.Tests
{
    /// <summary>
    /// 054, CSV backend: the source-language column of each key is read from the file the build writes that key
    /// to (<c>{folder}/{lib}/{table}.csv</c>), and from nowhere else. Uses a throwaway folder outside Assets/.
    /// </summary>
    public class CsvSourceTextsTests
    {
        private const string Lib = "CsvLib";
        private string _folder;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "GLCsvSource_" + Guid.NewGuid().ToString("N").Substring(0, 8))
                .Replace('\\', '/');
            Directory.CreateDirectory($"{_folder}/{Lib}");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        private void Write(string table, string csv) => File.WriteAllText($"{_folder}/{Lib}/{table}.csv", csv);

        [Test]
        public void ReadsTheSourceColumn_OfEachKeysOwnFile()
        {
            var db = new LocalizationDatabase();
            db.GetOrCreateGraphEntry("g1", "Intro").AddKey("line_a", LocalizationKeyType.Text, defaultHint: "x");
            db.AddGlobalKey("speaker_m", LocalizationKeyType.SpeakerName, "y", "Speakers");
            Write(LocalizationTableNames.ForGraph("Intro"), "Key,fr,en\nline_a,\"Bonjour, toi\",Hello\n");
            Write(LocalizationTableNames.ForGroup(Lib, "Speakers"), "Key,fr,en\nspeaker_m,Le Maire,The Mayor\n");

            var texts = CsvLocalizationExporter.ReadSourceTexts(Lib, db, "fr", _folder);

            Assert.AreEqual("Bonjour, toi", texts["line_a"]);
            Assert.AreEqual("Le Maire", texts["speaker_m"]);
        }

        [Test]
        public void IgnoresAKeyFoundOnlyInAnotherFile()
        {
            var db = new LocalizationDatabase();
            db.GetOrCreateGraphEntry("g1", "Intro").AddKey("line_a", LocalizationKeyType.Text, defaultHint: "x");
            Write("Other", "Key,fr\nline_a,Ailleurs\n");

            var texts = CsvLocalizationExporter.ReadSourceTexts(Lib, db, "fr", _folder);

            Assert.IsFalse(texts.ContainsKey("line_a"));
        }

        [Test]
        public void EmptyCellMissingFileOrMissingColumn_GiveNothing()
        {
            var db = new LocalizationDatabase();
            db.GetOrCreateGraphEntry("g1", "Intro").AddKey("line_a", LocalizationKeyType.Text, defaultHint: "x");
            db.GetOrCreateGraphEntry("g2", "Absent").AddKey("line_b", LocalizationKeyType.Text, defaultHint: "y");
            Write(LocalizationTableNames.ForGraph("Intro"), "Key,fr,en\nline_a,,Hello\n");

            Assert.IsEmpty(CsvLocalizationExporter.ReadSourceTexts(Lib, db, "fr", _folder));
            Assert.IsEmpty(CsvLocalizationExporter.ReadSourceTexts(Lib, db, "de", _folder));
        }

        [Test]
        public void MultilineCell_IsReadWhole()
        {
            var db = new LocalizationDatabase();
            db.GetOrCreateGraphEntry("g1", "Intro").AddKey("line_a", LocalizationKeyType.Text, defaultHint: "x");
            Write(LocalizationTableNames.ForGraph("Intro"), "Key,fr\nline_a,\"Un\ndeux\"\n");

            Assert.AreEqual("Un\ndeux", CsvLocalizationExporter.ReadSourceTexts(Lib, db, "fr", _folder)["line_a"]);
        }
    }
}
