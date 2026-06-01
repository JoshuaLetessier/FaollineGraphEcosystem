using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using Faolline.GraphDialogue;
using UnityLocalizationSettings = UnityEngine.Localization.Settings.LocalizationSettings;

namespace Faolline.GraphDialogue.Localization.Unity.Editor
{
    /// <summary>
    /// Syncs a <see cref="LocalizationDatabase"/> to Unity Localization String Tables. Lives in the
    /// adapter's Editor assembly; called by DialogueLocalizationBuilder via reflection.
    /// Behaviour: per-graph collection (DLG_&lt;Graph&gt;) + a global Dialogue_Speakers collection; adds
    /// missing keys, pre-fills the source-locale value with the default text (Title/fallback) when empty,
    /// removes orphan entries (keys no longer in the graph), and reports per-locale coverage according to
    /// the configured <see cref="LocaleValidationMode"/>. Orphan collections (deleted/renamed graphs) are
    /// reported, never auto-deleted.
    /// </summary>
    public static class UnityLocalizationDatabaseSyncer
    {
        private const string DialogueCollectionPrefix = "DLG_";
        private const string SpeakersCollectionName = "Dialogue_Speakers";

        public static void SyncDatabase(LocalizationDatabase database, LocaleValidationMode validation)
        {
            if (database == null) return;

            var locales = LocalizationEditorSettings.GetLocales();
            if (locales == null || locales.Count == 0)
            {
                Debug.LogWarning("[UnityLocalizationDatabaseSyncer] No locales configured in Project Settings > Localization");
                return;
            }

            var sourceLocale = GetSourceLocale(locales);
            var report = new SyncReport();
            var managed = new List<StringTableCollection>();
            var desiredCollectionNames = new HashSet<string>(StringComparer.Ordinal);

            // Per-graph collections
            foreach (var graphEntry in database.Graphs)
            {
                var collectionName = $"{DialogueCollectionPrefix}{Sanitize(graphEntry.GraphName)}";
                desiredCollectionNames.Add(collectionName);
                var collection = GetOrCreateCollection(collectionName, report);
                EnsureTablesForAllLocales(collection, locales);
                SyncEntries(collection, graphEntry.Keys, sourceLocale, report);
                managed.Add(collection);
            }

            // Global speakers collection
            var speakers = GetOrCreateCollection(SpeakersCollectionName, report);
            EnsureTablesForAllLocales(speakers, locales);
            SyncEntries(speakers, database.SpeakerKeys, sourceLocale, report);
            managed.Add(speakers);

            ReportOrphanCollections(desiredCollectionNames, report);

            AssetDatabase.SaveAssets();

            ReportCoverage(managed, locales, sourceLocale, validation, report);
            Debug.Log(report.Build());
        }

        // ── Collections ─────────────────────────────────────────────────────────

        private static StringTableCollection GetOrCreateCollection(string collectionName, SyncReport report)
        {
            var existing = LocalizationEditorSettings.GetStringTableCollection(collectionName);
            if (existing != null) return existing;

            var folder = "Assets/Localization/Collections";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                var parent = AssetDatabase.IsValidFolder("Assets/Localization") ? "Assets/Localization" : "Assets";
                var name = AssetDatabase.IsValidFolder("Assets/Localization") ? "Collections" : "Localization";
                AssetDatabase.CreateFolder(parent, name);
            }

            var collection = LocalizationEditorSettings.CreateStringTableCollection(collectionName, $"{folder}/{collectionName}");
            report.CollectionsCreated++;
            return collection;
        }

        private static void EnsureTablesForAllLocales(StringTableCollection collection, IEnumerable<Locale> locales)
        {
            foreach (var locale in locales)
            {
                if (!(collection.GetTable(locale.Identifier) is StringTable))
                    collection.AddNewTable(locale.Identifier);
            }
        }

        /// <summary>Warns about DLG_ collections that no longer match any graph (deleted/renamed).</summary>
        private static void ReportOrphanCollections(HashSet<string> desiredCollectionNames, SyncReport report)
        {
            var all = LocalizationEditorSettings.GetStringTableCollections();
            if (all == null) return;

            foreach (var col in all)
            {
                if (col == null) continue;
                var name = col.TableCollectionName;
                if (string.IsNullOrEmpty(name) || !name.StartsWith(DialogueCollectionPrefix, StringComparison.Ordinal))
                    continue;
                if (!desiredCollectionNames.Contains(name))
                    report.OrphanCollections.Add(name);
            }
        }

        // ── Entries ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Adds missing keys, pre-fills the source-locale value with each key's default text when empty,
        /// and removes orphan entries (present in the table but no longer desired).
        /// </summary>
        private static void SyncEntries(StringTableCollection collection, IReadOnlyList<LocalizationKeyEntry> keys,
            Locale sourceLocale, SyncReport report)
        {
            var shared = collection.SharedData;
            var sourceTable = sourceLocale != null ? collection.GetTable(sourceLocale.Identifier) as StringTable : null;

            var desired = new HashSet<string>(StringComparer.Ordinal);

            foreach (var keyEntry in keys)
            {
                if (keyEntry == null || string.IsNullOrWhiteSpace(keyEntry.Key)) continue;
                desired.Add(keyEntry.Key);

                var sharedEntry = shared.GetEntry(keyEntry.Key);
                if (sharedEntry == null)
                {
                    sharedEntry = shared.AddKey(keyEntry.Key);
                    report.KeysAdded++;
                }
                if (sharedEntry == null) continue;

                if (sourceTable != null && !string.IsNullOrEmpty(keyEntry.DefaultHint))
                {
                    var entry = sourceTable.GetEntry(sharedEntry.Id) ?? sourceTable.AddEntry(sharedEntry.Id, string.Empty);
                    if (entry != null && string.IsNullOrEmpty(entry.Value))
                        entry.Value = keyEntry.DefaultHint;
                }
            }

            // Remove orphan entries (no longer in the graph/speaker set).
            var orphans = shared.Entries.Where(e => e != null && !desired.Contains(e.Key)).ToList();
            foreach (var orphan in orphans)
            {
                foreach (var table in collection.StringTables)
                    if (table != null && table.GetEntry(orphan.Id) != null) table.RemoveEntry(orphan.Id);
                shared.RemoveKey(orphan.Id);
                report.KeysRemoved++;
            }

            EditorUtility.SetDirty(shared);
            foreach (var table in collection.StringTables)
                if (table != null) EditorUtility.SetDirty(table);
        }

        // ── Coverage + validation ─────────────────────────────────────────────────

        private static void ReportCoverage(List<StringTableCollection> managed, IList<Locale> locales,
            Locale sourceLocale, LocaleValidationMode validation, SyncReport report)
        {
            foreach (var locale in locales)
            {
                int total = 0, filled = 0;
                foreach (var collection in managed)
                {
                    if (collection.GetTable(locale.Identifier) is StringTable table)
                    {
                        foreach (var sharedEntry in collection.SharedData.Entries)
                        {
                            if (sharedEntry == null) continue;
                            total++;
                            var e = table.GetEntry(sharedEntry.Id);
                            if (e != null && !string.IsNullOrEmpty(e.Value)) filled++;
                        }
                    }
                }
                report.Coverage.Add((locale.Identifier.Code, filled, total, locale == sourceLocale));
            }

            report.Validation = validation;
            if (validation == LocaleValidationMode.Permissive) return;

            foreach (var (code, filled, total, isSource) in report.Coverage)
            {
                if (total == 0 || filled >= total) continue;
                var missing = total - filled;
                var msg = $"[UnityLocalizationDatabaseSyncer] Locale '{code}' incomplete: {filled}/{total} ({Pct(filled, total)}%), {missing} missing.";
                if (validation == LocaleValidationMode.Strict) Debug.LogError(msg);
                else Debug.LogWarning(msg);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static Locale GetSourceLocale(IList<Locale> locales)
        {
            try
            {
                var project = UnityLocalizationSettings.ProjectLocale;
                if (project != null) return project;
            }
            catch { /* settings unavailable at edit time — fall through */ }
            return locales != null && locales.Count > 0 ? locales[0] : null;
        }

        private static int Pct(int n, int d) => d <= 0 ? 100 : Mathf.RoundToInt(100f * n / d);

        private static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unnamed";
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
            return new string(chars);
        }

        private sealed class SyncReport
        {
            public int CollectionsCreated;
            public int KeysAdded;
            public int KeysRemoved;
            public readonly List<string> OrphanCollections = new List<string>();
            public readonly List<(string code, int filled, int total, bool isSource)> Coverage =
                new List<(string, int, int, bool)>();
            public LocaleValidationMode Validation;

            public string Build()
            {
                var sb = new StringBuilder();
                sb.AppendLine("[UnityLocalizationDatabaseSyncer] Sync complete.");
                sb.AppendLine($"  Collections created: {CollectionsCreated} | keys +{KeysAdded} / -{KeysRemoved} (orphans)");
                if (Coverage.Count > 0)
                {
                    sb.AppendLine("  Per-locale coverage:");
                    foreach (var (code, filled, total, isSource) in Coverage)
                        sb.AppendLine($"    {code}{(isSource ? " (source)" : "")}: {filled}/{total} ({Pct(filled, total)}%)");
                }
                if (OrphanCollections.Count > 0)
                    sb.AppendLine($"  Orphan collections (no matching graph, not deleted): {string.Join(", ", OrphanCollections)}");
                return sb.ToString().TrimEnd();
            }
        }
    }
}
