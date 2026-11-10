using System;
using Faolline.GraphLocalization.Unity.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace Faolline.GraphLocalization.Unity.Tests
{
    /// <summary>
    /// 054, Unity Localization backend: <see cref="UnityLocalizationSyncer.ReadSourceTexts"/> returns, per key, the
    /// source-locale value of the collection the build files that key in — the value the game shows, whether the
    /// build pre-filled it or someone edited it since. Throwaway collections root and unique names, as in
    /// <see cref="UnityLocalizationSyncerGroupTests"/>.
    /// </summary>
    public class UnityLocalizationSourceTextsTests
    {
        private string _tempRoot;
        private string _collections;
        private string _lib;
        private Locale _source;
        private Locale _other;

        [SetUp]
        public void SetUp()
        {
            var locales = LocalizationEditorSettings.GetLocales();
            Assume.That(locales != null && locales.Count >= 2, "needs at least two project locales");
            _source = locales[0];
            _other = locales[1];

            var id = Guid.NewGuid().ToString("N").Substring(0, 8);
            AssetDatabase.CreateFolder("Assets", "__GLSourceTest_" + id);
            _tempRoot = "Assets/__GLSourceTest_" + id;
            AssetDatabase.CreateFolder(_tempRoot, "Collections");
            _collections = _tempRoot + "/Collections";
            _lib = "SrcLib" + id;
        }

        [TearDown]
        public void TearDown() => SyncerTestCleanup.DeleteCollectionsUnder(_tempRoot);

        private void Sync(LocalizationDatabase db)
            => UnityLocalizationSyncer.SyncDatabase(_lib, db, LocaleValidationMode.Permissive,
                generateStringTables: true, generateAssetTables: false, _source.Identifier.Code, _collections);

        private static void SetValue(string collection, Locale locale, string key, string value)
        {
            var col = LocalizationEditorSettings.GetStringTableCollection(collection);
            var table = (StringTable)col.GetTable(locale.Identifier);
            var entry = table.GetEntry(key) ?? table.AddEntry(key, value);
            entry.Value = value;
            EditorUtility.SetDirty(table);
        }

        private LocalizationDatabase Db()
        {
            var db = new LocalizationDatabase();
            db.GetOrCreateGraphEntry("guid-src", "Graph" + _lib).AddKey("line_a", LocalizationKeyType.Text, defaultHint: "Bonjour");
            db.AddGlobalKey("speaker_m", LocalizationKeyType.SpeakerName, "Maire", "Speakers");
            return db;
        }

        [Test]
        public void ReadsThePrefilledSourceValues_OfGraphAndGroupTables()
        {
            var db = Db();
            Sync(db);

            var texts = UnityLocalizationSyncer.ReadSourceTexts(_lib, db, _source.Identifier.Code);

            Assert.AreEqual("Bonjour", texts["line_a"]);
            Assert.AreEqual("Maire", texts["speaker_m"]);
        }

        [Test]
        public void ReadsAnEditedSourceValue_NotTheOtherLocale()
        {
            var db = Db();
            Sync(db);
            SetValue(LocalizationTableNames.TextCollection("Graph" + _lib), _source, "line_a", "Bonjour, voyageur");
            SetValue(LocalizationTableNames.TextCollection("Graph" + _lib), _other, "line_a", "Hello, traveller");

            var texts = UnityLocalizationSyncer.ReadSourceTexts(_lib, db, _source.Identifier.Code);

            Assert.AreEqual("Bonjour, voyageur", texts["line_a"]);
        }

        [Test]
        public void EmptySourceValue_OrNoCollection_GivesNothing()
        {
            var db = Db();
            Sync(db);
            SetValue(LocalizationTableNames.TextCollection("Graph" + _lib), _source, "line_a", "");

            var absent = new LocalizationDatabase();
            absent.GetOrCreateGraphEntry("guid-none", "NeverBuilt" + _lib)
                .AddKey("line_z", LocalizationKeyType.Text, defaultHint: "z");

            Assert.IsFalse(UnityLocalizationSyncer.ReadSourceTexts(_lib, db, _source.Identifier.Code).ContainsKey("line_a"));
            Assert.IsEmpty(UnityLocalizationSyncer.ReadSourceTexts(_lib, absent, _source.Identifier.Code));
        }
    }
}
