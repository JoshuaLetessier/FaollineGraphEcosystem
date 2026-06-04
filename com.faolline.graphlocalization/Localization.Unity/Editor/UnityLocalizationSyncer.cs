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
        /// <summary>Suffix for the mirror Asset Table collection of a String Table collection (shared with the builder).</summary>
        public const string AssetCollectionSuffix = "_Assets";

        /// <summary>
        /// Entry point called via reflection from the builder core.
        /// Signature: (string libName, LocalizationDatabase database, LocaleValidationMode validation,
        /// bool generateStringTables, bool generateAssetTables).
        /// Returns the String Table collection names for this lib (always — even when not regenerated — so
        /// the builder can record them in the runtime manifest). String Tables are created/synced only when
        /// <paramref name="generateStringTables"/> is true; a mirror Asset Table collection (name +
        /// <see cref="AssetCollectionSuffix"/>, same keys) is created beside each when
        /// <paramref name="generateAssetTables"/> is true. Existing collections are never deleted.
        /// </summary>
        public static string[] SyncDatabase(string libName, LocalizationDatabase database,
            LocaleValidationMode validation, bool generateStringTables, bool generateAssetTables)
        {
            if (database == null) return System.Array.Empty<string>();

            var locales = LocalizationEditorSettings.GetLocales();
            if (locales == null || locales.Count == 0)
            {
                Debug.LogWarning($"[UnityLocalizationSyncer] [{libName}] No locales configured in Project Settings > Localization.");
                return System.Array.Empty<string>();
            }

            var libFolder = EnsureLibFolder(libName);
            var sourceLocale = GetSourceLocale(locales);
            var report = new SyncReport(libName);
            var managed = new List<StringTableCollection>();
            var desiredNames = new HashSet<string>(StringComparer.Ordinal);

            // Per-graph collections, each in its own subfolder: Collections/{lib}/{graph}/
            foreach (var graphEntry in database.Graphs)
            {
                var name = $"{GraphCollectionPrefix}{Sanitize(graphEntry.GraphName)}";
                desiredNames.Add(name);
                var folder = EnsureSubFolder(libFolder, Sanitize(graphEntry.GraphName));
                if (generateStringTables)
                {
                    var col = GetOrCreateCollection(name, folder, report);
                    MoveCollectionIfNeeded(col, $"{folder}/{name}", report);
                    EnsureTablesForAllLocales(col, locales);
                    SyncEntries(col, graphEntry.Keys, sourceLocale, report);
                    managed.Add(col);
                }
                if (generateAssetTables) EnsureAssetCollection(name + AssetCollectionSuffix, folder, graphEntry.Keys, locales);
            }

            // Global collection (speakers, etc.) in Collections/{lib}/_Global/
            if (database.GlobalKeys.Count > 0)
            {
                var globalName = $"{Sanitize(libName)}{GlobalCollectionSuffix}";
                desiredNames.Add(globalName);
                var folder = EnsureSubFolder(libFolder, "_Global");
                if (generateStringTables)
                {
                    var globalCol = GetOrCreateCollection(globalName, folder, report);
                    MoveCollectionIfNeeded(globalCol, $"{folder}/{globalName}", report);
                    EnsureTablesForAllLocales(globalCol, locales);
                    SyncEntries(globalCol, database.GlobalKeys, sourceLocale, report);
                    managed.Add(globalCol);
                }
                if (generateAssetTables) EnsureAssetCollection(globalName + AssetCollectionSuffix, folder, database.GlobalKeys, locales);
            }

            ReportOrphanCollections(libFolder, desiredNames, report);
            AssetDatabase.SaveAssets();
            ReportCoverage(managed, locales, sourceLocale, validation, report);
            Debug.Log(report.Build());

            return new List<string>(desiredNames).ToArray();
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

        private static string EnsureSubFolder(string parent, string child)
        {
            var path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
            return path;
        }

        /// <summary>
        /// Relocates an existing collection into <paramref name="desiredFolder"/> (its own per-collection
        /// subfolder). GUIDs are preserved, so cross-references stay intact. No-op when already there.
        /// Creates the target folder if missing.
        /// </summary>
        private static void MoveCollectionIfNeeded(StringTableCollection col, string desiredFolder, SyncReport report)
        {
            if (col == null) return;
            var colPath = AssetDatabase.GetAssetPath(col);
            if (string.IsNullOrEmpty(colPath)) return;
            var currentFolder = System.IO.Path.GetDirectoryName(colPath)?.Replace('\\', '/');
            if (currentFolder == desiredFolder) return;

            if (!AssetDatabase.IsValidFolder(desiredFolder))
            {
                var parent = System.IO.Path.GetDirectoryName(desiredFolder)?.Replace('\\', '/');
                var leaf = System.IO.Path.GetFileName(desiredFolder);
                if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(leaf)) AssetDatabase.CreateFolder(parent, leaf);
            }

            var assets = new List<UnityEngine.Object>();
            if (col.SharedData != null) assets.Add(col.SharedData);
            foreach (var t in col.StringTables) if (t != null) assets.Add(t);
            assets.Add(col); // move the collection asset last

            foreach (var asset in assets)
            {
                var path = AssetDatabase.GetAssetPath(asset);
                if (string.IsNullOrEmpty(path)) continue;
                var newPath = $"{desiredFolder}/{System.IO.Path.GetFileName(path)}";
                if (path == newPath) continue;
                var error = AssetDatabase.MoveAsset(path, newPath);
                if (!string.IsNullOrEmpty(error))
                    Debug.LogWarning($"[UnityLocalizationSyncer] Could not move '{path}' → '{newPath}': {error}");
            }
            report.CollectionsMoved++;
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

        // ── Asset tables (mirror of the string collection, same keys) ──────────────

        private static void EnsureAssetCollection(string name, string folder,
            IReadOnlyList<LocalizationKeyEntry> keys, IList<Locale> locales)
        {
            var col = LocalizationEditorSettings.GetAssetTableCollection(name)
                      ?? LocalizationEditorSettings.CreateAssetTableCollection(name, $"{folder}/{name}");
            if (col == null) return;

            foreach (var locale in locales)
                if (!(col.GetTable(locale.Identifier) is AssetTable))
                    col.AddNewTable(locale.Identifier);

            var shared = col.SharedData;
            if (shared == null) return;

            var desired = new HashSet<string>(StringComparer.Ordinal);
            foreach (var k in keys)
            {
                if (k == null || string.IsNullOrWhiteSpace(k.Key)) continue;
                desired.Add(k.Key);
                if (shared.GetEntry(k.Key) == null) shared.AddKey(k.Key);
            }

            foreach (var orphan in shared.Entries.Where(e => e != null && !desired.Contains(e.Key)).ToList())
            {
                foreach (var t in col.AssetTables) if (t != null && t.GetEntry(orphan.Id) != null) t.RemoveEntry(orphan.Id);
                shared.RemoveKey(orphan.Id);
            }

            EditorUtility.SetDirty(shared);
            foreach (var t in col.AssetTables) if (t != null) EditorUtility.SetDirty(t);
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
            public int CollectionsCreated, CollectionsMoved, KeysAdded, KeysRemoved;
            public readonly List<string> OrphanCollections = new();
            public readonly List<(string code, int filled, int total, bool isSource)> Coverage = new();
            public LocaleValidationMode Validation;
            public SyncReport(string libName) => LibName = libName;

            public string Build()
            {
                var sb = new StringBuilder();
                sb.AppendLine($"[UnityLocalizationSyncer] [{LibName}] Sync complete. Collections +{CollectionsCreated}~{CollectionsMoved} | Keys +{KeysAdded}/-{KeysRemoved}");
                foreach (var (code, filled, total, isSource) in Coverage)
                    sb.AppendLine($"  {code}{(isSource ? " (source)" : "")}: {filled}/{total} ({Pct(filled, total)}%)");
                if (OrphanCollections.Count > 0)
                    sb.AppendLine($"  Orphan collections (not deleted): {string.Join(", ", OrphanCollections)}");
                return sb.ToString().TrimEnd();
            }
        }
    }
}
