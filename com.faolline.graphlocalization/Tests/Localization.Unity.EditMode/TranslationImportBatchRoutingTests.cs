using System;
using System.IO;
using Faolline.GraphLocalization.Unity.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace Faolline.GraphLocalization.Unity.Tests
{
    /// <summary>
    /// 052 US3 end to end on real collections: a single speakers CSV is imported key by key into the group
    /// collection holding each key; an unknown key is reported, the valid rows are still imported, and other
    /// entries of the target collections are left untouched.
    /// </summary>
    public class TranslationImportBatchRoutingTests
    {
        private string _tempRoot;
        private string _collections;
        private string _lib;
        private Locale _source;
        private Locale _other;
        private string _csvPath;

        [SetUp]
        public void SetUp()
        {
            var locales = LocalizationEditorSettings.GetLocales();
            Assume.That(locales != null && locales.Count >= 2, "needs at least two project locales");
            _source = locales[0];
            _other = locales[1];

            var id = Guid.NewGuid().ToString("N").Substring(0, 8);
            AssetDatabase.CreateFolder("Assets", "__GLImportTest_" + id);
            _tempRoot = "Assets/__GLImportTest_" + id;
            AssetDatabase.CreateFolder(_tempRoot, "Collections");
            _collections = _tempRoot + "/Collections";
            _lib = "TestLib" + id;
            _csvPath = Path.Combine(Path.GetTempPath(), $"speakers_{id}.csv");
        }

        [TearDown]
        public void TearDown()
        {
            SyncerTestCleanup.DeleteCollectionsUnder(_tempRoot);
            if (_csvPath != null && File.Exists(_csvPath)) File.Delete(_csvPath);
        }

        private string GroupCollection(string group)
            => LocalizationTableNames.TextCollection(LocalizationTableNames.ForGroup(_lib, group));

        private static string Value(string collection, Locale locale, string key)
        {
            var col = LocalizationEditorSettings.GetStringTableCollection(collection);
            var table = col.GetTable(locale.Identifier) as StringTable;
            var entry = table != null ? table.GetEntry(key) : null;
            return entry != null ? entry.Value : null;
        }

        [Test]
        public void SpeakersCsv_IsRoutedByKey_UnknownKeyReported_RestImported()
        {
            var db = new LocalizationDatabase();
            db.AddGlobalKey("speaker_a", LocalizationKeyType.SpeakerName, "Innkeeper", "Speakers_A");
            db.AddGlobalKey("speaker_b", LocalizationKeyType.SpeakerName, "Smith", "Speakers_B");
            db.AddGlobalKey("speaker_keep", LocalizationKeyType.SpeakerName, "Keeper", "Speakers_B");
            UnityLocalizationSyncer.SyncDatabase(_lib, db, LocaleValidationMode.Permissive, true, false,
                _source.Identifier.Code, _collections);

            var src = _source.Identifier.Code;
            var oth = _other.Identifier.Code;
            File.WriteAllText(_csvPath,
                $"Key,{src},{oth}\n" +
                "speaker_a,Innkeeper,Aubergiste\n" +
                "speaker_b,Smith,Forgeron\n" +
                "speaker_ghost,Ghost,Fantome\n");

            var (imported, failures) = TranslationImportBatch.ImportGlobalKeysCsv(_csvPath, _collections);

            Assert.AreEqual(2, imported, "one import per target collection");
            Assert.AreEqual(1, failures.Count);
            StringAssert.Contains("speaker_ghost", failures[0]);
            Assert.AreEqual("Aubergiste", Value(GroupCollection("Speakers_A"), _other, "speaker_a"));
            Assert.AreEqual("Forgeron", Value(GroupCollection("Speakers_B"), _other, "speaker_b"));
            Assert.AreEqual("Keeper", Value(GroupCollection("Speakers_B"), _source, "speaker_keep"),
                "an entry absent from the CSV is left untouched");
        }

        [Test]
        public void MissingCsvFile_IsAFailure()
        {
            var (imported, failures) = TranslationImportBatch.ImportGlobalKeysCsv(_csvPath, _collections);
            Assert.AreEqual(0, imported);
            Assert.AreEqual(1, failures.Count);
        }
    }
}
