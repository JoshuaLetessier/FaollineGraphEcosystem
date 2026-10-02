using System;
using System.Collections.Generic;
using Faolline.GraphLocalization.Unity.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace Faolline.GraphLocalization.Unity.Tests
{
    /// <summary>
    /// Syncer side of 052 (research R8): one lib-scoped collection per global-key group, and translations
    /// carried over whenever a key moves to another collection of the same lib (group change, graph rename,
    /// legacy shared table). Runs against a throwaway collections root with unique lib/collection names, so
    /// the dev project's real collections (including its legacy Global_Text) are never touched.
    /// </summary>
    public class UnityLocalizationSyncerGroupTests
    {
        private string _tempRoot;      // Assets/__GLSyncTest_xxxx
        private string _collections;   // {_tempRoot}/Collections
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
            AssetDatabase.CreateFolder("Assets", "__GLSyncTest_" + id);
            _tempRoot = "Assets/__GLSyncTest_" + id;
            AssetDatabase.CreateFolder(_tempRoot, "Collections");
            _collections = _tempRoot + "/Collections";
            _lib = "TestLib" + id;
        }

        [TearDown]
        public void TearDown() => SyncerTestCleanup.DeleteCollectionsUnder(_tempRoot);

        private void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        // ── helpers ─────────────────────────────────────────────────────────────

        private void Sync(LocalizationDatabase db)
            => UnityLocalizationSyncer.SyncDatabase(_lib, db, LocaleValidationMode.Permissive,
                generateStringTables: true, generateAssetTables: false, _source.Identifier.Code, _collections);

        private string GroupCollection(string group)
            => LocalizationTableNames.TextCollection(LocalizationTableNames.ForGroup(_lib, group));

        private static StringTableCollection Collection(string name) => LocalizationEditorSettings.GetStringTableCollection(name);

        private static string Value(StringTableCollection col, Locale locale, string key)
        {
            var table = col.GetTable(locale.Identifier) as StringTable;
            var entry = table != null ? table.GetEntry(key) : null;
            return entry != null ? entry.Value : null;
        }

        private static void SetValue(StringTableCollection col, Locale locale, string key, string value)
        {
            var table = (StringTable)col.GetTable(locale.Identifier);
            var entry = table.GetEntry(key) ?? table.AddEntry(key, value);
            entry.Value = value;
            EditorUtility.SetDirty(table);
            EditorUtility.SetDirty(col.SharedData);
        }

        private static LocalizationDatabase Globals(params (string key, string hint, string group)[] keys)
        {
            var db = new LocalizationDatabase();
            foreach (var (key, hint, group) in keys) db.AddGlobalKey(key, LocalizationKeyType.SpeakerName, hint, group);
            return db;
        }

        // ── (a) one collection per group ────────────────────────────────────────

        [Test]
        public void EachGroup_GetsItsOwnLibScopedCollection_WithOnlyItsKeys()
        {
            Sync(Globals(("speaker_a", "Aubergiste", "Speakers"), ("speaker_b", "Forgeron", "Speakers_A")));

            var def = Collection(GroupCollection("Speakers"));
            var groupA = Collection(GroupCollection("Speakers_A"));
            Assert.IsNotNull(def, "default group collection");
            Assert.IsNotNull(groupA, "group A collection");

            Assert.IsNotNull(def.SharedData.GetEntry("speaker_a"));
            Assert.IsNull(def.SharedData.GetEntry("speaker_b"));
            Assert.IsNotNull(groupA.SharedData.GetEntry("speaker_b"));
            Assert.IsNull(groupA.SharedData.GetEntry("speaker_a"));

            Assert.AreEqual("Aubergiste", Value(def, _source, "speaker_a"), "source column pre-filled");
            StringAssert.StartsWith($"{_collections}/{_lib}/_Global/", AssetDatabase.GetAssetPath(groupA));
        }

        // ── (b) group change carries translations ───────────────────────────────

        [Test]
        public void KeyChangingGroup_CarriesItsTranslations_AndLeavesTheOldTable()
        {
            Sync(Globals(("speaker_a", "A", "Speakers_A"), ("speaker_keep", "K", "Speakers_A")));
            SetValue(Collection(GroupCollection("Speakers_A")), _other, "speaker_a", "Traduit");

            Sync(Globals(("speaker_a", "A", "Speakers_B"), ("speaker_keep", "K", "Speakers_A")));

            Assert.AreEqual("Traduit", Value(Collection(GroupCollection("Speakers_B")), _other, "speaker_a"));
            Assert.IsNull(Collection(GroupCollection("Speakers_A")).SharedData.GetEntry("speaker_a"),
                "removed from the table it left");
        }

        // ── (c) legacy/orphan collection migrated, reported, kept ────────────────

        [Test]
        public void OrphanCollectionOfTheLib_IsMigratedFrom_AndKept()
        {
            var legacyName = "Legacy" + _lib + "_Text";
            var legacyFolder = $"{_collections}/{_lib}/_Global/{legacyName}";
            EnsureFolder(legacyFolder);
            var legacy = LocalizationEditorSettings.CreateStringTableCollection(legacyName, legacyFolder);
            legacy.SharedData.AddKey("speaker_a");
            SetValue(legacy, _source, "speaker_a", "Aubergiste (edited)");
            SetValue(legacy, _other, "speaker_a", "Innkeeper");
            AssetDatabase.SaveAssets();

            Sync(Globals(("speaker_a", "Aubergiste", "Speakers")));

            var target = Collection(GroupCollection("Speakers"));
            Assert.AreEqual("Innkeeper", Value(target, _other, "speaker_a"));
            Assert.AreEqual("Aubergiste (edited)", Value(target, _source, "speaker_a"),
                "a carried source value wins over the build hint");
            Assert.IsNotNull(Collection(legacyName), "the orphan is reported, never deleted");
            Assert.IsNotNull(Collection(legacyName).SharedData.GetEntry("speaker_a"));
        }

        // ── (d) target's own value is never overwritten ─────────────────────────

        [Test]
        public void TargetNonEmptyValue_IsNeverOverwrittenByCarry()
        {
            Sync(Globals(("speaker_a", "A", "Speakers_A"), ("speaker_keep", "K", "Speakers_A")));
            SetValue(Collection(GroupCollection("Speakers_A")), _other, "speaker_a", "Old");
            Sync(Globals(("speaker_a", "A", "Speakers_B"), ("speaker_keep", "K", "Speakers_A")));
            SetValue(Collection(GroupCollection("Speakers_B")), _other, "speaker_a", "Mine");

            // Re-introduce a stale copy in A, then rebuild: B keeps its own value.
            var a = Collection(GroupCollection("Speakers_A"));
            a.SharedData.AddKey("speaker_a");
            SetValue(a, _other, "speaker_a", "Stale");
            Sync(Globals(("speaker_a", "A", "Speakers_B"), ("speaker_keep", "K", "Speakers_A")));

            Assert.AreEqual("Mine", Value(Collection(GroupCollection("Speakers_B")), _other, "speaker_a"));
        }

        // ── (e) graph rename carries line translations ──────────────────────────

        [Test]
        public void RenamedGraph_CarriesItsLineTranslations()
        {
            var oldName = "G1" + _lib;
            var newName = "G2" + _lib;

            var db1 = new LocalizationDatabase();
            db1.GetOrCreateGraphEntry("guid-1", oldName).AddKey("line_x", LocalizationKeyType.Text, defaultHint: "Hello");
            Sync(db1);
            SetValue(Collection(LocalizationTableNames.TextCollection(oldName)), _other, "line_x", "Bonjour");

            var db2 = new LocalizationDatabase();
            db2.GetOrCreateGraphEntry("guid-1", newName).AddKey("line_x", LocalizationKeyType.Text, defaultHint: "Hello");
            Sync(db2);

            Assert.AreEqual("Bonjour", Value(Collection(LocalizationTableNames.TextCollection(newName)), _other, "line_x"));
        }
    }
}
