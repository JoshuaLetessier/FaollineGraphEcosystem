using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using UnityLocalizationSettings = UnityEngine.Localization.Settings.LocalizationSettings;

namespace Faolline.GraphLocalization.Unity.Editor
{
    /// <summary>
    /// Syncs a <see cref="LocalizationDatabase"/> to Unity Localization String Tables for one graph lib.
    /// Called by <see cref="Faolline.GraphLocalization.Editor.LocalizationBuilderCore"/> via reflection.
    /// Collections are created under Assets/Localization/Collections/{libName}/ to keep libs isolated.
    /// </summary>
    public static class UnityLocalizationSyncer
    {
        private const string CollectionsRoot = "Assets/Localization/Collections";
        private const string GraphCollectionPrefix = "DLG_";
        private const string GlobalCollectionSuffix = "_Global";

        /// <summary>
        /// Entry point called via reflection from the builder core.
        /// Signature: (string libName, LocalizationDatabase database, LocaleValidationMode validation).
        /// </summary>
        public static void SyncDatabase(string libName, LocalizationDatabase database, LocaleValidationMode validation)
        {
            if (database == null) return;

            var locales = LocalizationEditorSettings.GetLocales();
            if (locales == null || locales.Count == 0)
            {
                Debug.LogWarning($"[UnityLocalizationSyncer] [{libName}] No locales configured in Project Settings > Localization.");
                return;
            }

            var libFolder = EnsureLibFolder(libName);
            var sourceLocale = GetSourceLocale(locales);
            var report = new SyncReport(libName);
            var managed = new List<StringTableCollection>();
            var desiredNames = new HashSet<string>(StringComparer.Ordinal);

            // Per-graph collections
            foreach (var graphEntry in database.Graphs)
            {
                var name = $"{GraphCollectionPrefix}{Sanitize(graphEntry.GraphName)}";
                desiredNames.Add(name);
                var col = GetOrCreateCollection(name, libFolder, report);
                EnsureTablesForAllLocales(col, locales);
                SyncEntries(col, graphEntry.Keys, sourceLocale, report);
                managed.Add(col);
            }

            // Global collection (speakers, etc.)
            if (database.GlobalKeys.Count > 0)
            {
                var globalName = $"{Sanitize(libName)}{GlobalCollectionSuffix}";
                desiredNames.Add(globalName);
                var globalCol = GetOrCreateCollection(globalName, libFolder, report);
                EnsureTablesForAllLocales(globalCol, locales);
                SyncEntries(globalCol, database.GlobalKeys, sourceLocale, report);
                managed.Add(globalCol);
            }

            ReportOrphanCollections(libFolder, desiredNames, report);
            AssetDatabase.SaveAssets();
            ReportCoverage(managed, locales, sourceLocale, validation, report);
            Debug.Log(report.Build());
        }

        // ── Folder ───────────────────────────────────────────────────────────────

        private static string EnsureLibFolder(string libName)
        {
            var safeName = Sanitize(libName);
            if (!AssetDatabase.IsValidFolder(CollectionsRoot))
            {
                AssetDatabase.CreateFolder(
                    AssetDatabase.IsValidFolder("Assets/Localization") ? "Assets/Localization" : "Assets",
                    AssetDatabase.IsValidFolder("Assets/Localization") ? "Collections" : "Localization");
            }
            var libPath = $"{CollectionsRoot}/{safeName}";
            if (!AssetDatabase.IsValidFolder(libPath))
                AssetDatabase.CreateFolder(CollectionsRoot, safeName);
            return libPath;
        }

        // ── Collections ──────────────────────────────────────────────────────────

        private static StringTableCollection GetOrCreateCollection(string name, string folder, SyncReport report)
        {
            var existing = LocalizationEditorSettings.GetStringTableCollection(name);
            if (existing != null) return existing;

            var col = LocalizationEditorSettings.CreateStringTableCollection(name, $"{folder}/{name}");
            report.CollectionsCreated++;
            return col;
        }

        private static void EnsureTablesForAllLocales(StringTableCollection col, IEnumerable<Locale> locales)
        {
            foreach (var locale in locales)
                if (!(col.GetTable(locale.Identifier) is StringTable))
                    col.AddNewTable(locale.Identifier);
        }

        private static void ReportOrphanCollections(string libFolder, HashSet<string> desired, SyncReport report)
        {
            var all = LocalizationEditorSettings.GetStringTableCollections();
            if (all == null) return;
            foreach (var col in all)
            {
                if (col == null) continue;
                var path = AssetDatabase.GetAssetPath(col);
                if (!path.StartsWith(libFolder, StringComparison.Ordinal)) continue;
                if (!desired.Contains(col.TableCollectionName))
                    report.OrphanCollections.Add(col.TableCollectionName);
            }
        }

        // ── Entries ──────────────────────────────────────────────────────────────

        private static void SyncEntries(StringTableCollection col, IReadOnlyList<LocalizationKeyEntry> keys,
            Locale sourceLocale, SyncReport report)
        {
            var shared = col.SharedData;
            var sourceTable = sourceLocale != null ? col.GetTable(sourceLocale.Identifier) as StringTable : null;
            var desired = new HashSet<string>(StringComparer.Ordinal);

            foreach (var keyEntry in keys)
            {
                if (keyEntry == null || string.IsNullOrWhiteSpace(keyEntry.Key)) continue;
                desired.Add(keyEntry.Key);

                var sharedEntry = shared.GetEntry(keyEntry.Key);
                if (sharedEntry == null) { sharedEntry = shared.AddKey(keyEntry.Key); report.KeysAdded++; }
                if (sharedEntry == null) continue;

                if (sourceTable != null && !string.IsNullOrEmpty(keyEntry.DefaultHint))
                {
                    var entry = sourceTable.GetEntry(sharedEntry.Id) ?? sourceTable.AddEntry(sharedEntry.Id, string.Empty);
                    if (entry != null && string.IsNullOrEmpty(entry.Value)) entry.Value = keyEntry.DefaultHint;
                }
            }

            // Remove orphan entries
            foreach (var orphan in shared.Entries.Where(e => e != null && !desired.Contains(e.Key)).ToList())
            {
                foreach (var t in col.StringTables) if (t?.GetEntry(orphan.Id) != null) t.RemoveEntry(orphan.Id);
                shared.RemoveKey(orphan.Id);
                report.KeysRemoved++;
            }

            EditorUtility.SetDirty(shared);
            foreach (var t in col.StringTables) if (t != null) EditorUtility.SetDirty(t);
        }

        // ── Coverage ─────────────────────────────────────────────────────────────

        private static void ReportCoverage(List<StringTableCollection> managed, IList<Locale> locales,
            Locale sourceLocale, LocaleValidationMode validation, SyncReport report)
        {
            foreach (var locale in locales)
            {
                int total = 0, filled = 0;
                foreach (var col in managed)
                {
                    if (!(col.GetTable(locale.Identifier) is StringTable table)) continue;
                    foreach (var se in col.SharedData.Entries)
                    {
                        if (se == null) continue;
                        total++;
                        var e = table.GetEntry(se.Id);
                        if (e != null && !string.IsNullOrEmpty(e.Value)) filled++;
                    }
                }
                report.Coverage.Add((locale.Identifier.Code, filled, total, locale == sourceLocale));
            }

            report.Validation = validation;
            if (validation == LocaleValidationMode.Permissive) return;
            foreach (var (code, filled, total, _) in report.Coverage)
            {
                if (total == 0 || filled >= total) continue;
                var msg = $"[UnityLocalizationSyncer] [{report.LibName}] Locale '{code}': {filled}/{total} ({Pct(filled, total)}%), {total - filled} missing.";
                if (validation == LocaleValidationMode.Strict) Debug.LogError(msg);
                else Debug.LogWarning(msg);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static Locale GetSourceLocale(IList<Locale> locales)
        {
            try { var p = UnityLocalizationSettings.ProjectLocale; if (p != null) return p; } catch { }
            return locales?.Count > 0 ? locales[0] : null;
        }

        private static int Pct(int n, int d) => d <= 0 ? 100 : Mathf.RoundToInt(100f * n / d);

        private static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unnamed";
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        }

        private sealed class SyncReport
        {
            public readonly string LibName;
            public int CollectionsCreated, KeysAdded, KeysRemoved;
            public readonly List<string> OrphanCollections = new();
            public readonly List<(string code, int filled, int total, bool isSource)> Coverage = new();
            public LocaleValidationMode Validation;
            public SyncReport(string libName) => LibName = libName;

            public string Build()
            {
                var sb = new StringBuilder();
                sb.AppendLine($"[UnityLocalizationSyncer] [{LibName}] Sync complete. Collections +{CollectionsCreated} | Keys +{KeysAdded}/-{KeysRemoved}");
                foreach (var (code, filled, total, isSource) in Coverage)
                    sb.AppendLine($"  {code}{(isSource ? " (source)" : "")}: {filled}/{total} ({Pct(filled, total)}%)");
                if (OrphanCollections.Count > 0)
                    sb.AppendLine($"  Orphan collections (not deleted): {string.Join(", ", OrphanCollections)}");
                return sb.ToString().TrimEnd();
            }
        }
    }
}
