using System;
using System.Collections.Generic;
using System.Linq;
using Faolline.GraphLogging;
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
    /// Collections are created under Assets/Localization/Collections/{libName}/ to keep libs isolated:
    /// one per graph (<c>{graph}_Text</c>) and one per global-key group (<c>{lib}_{group}_Text</c> under
    /// <c>_Global/</c>), every name coming from <see cref="LocalizationTableNames"/>. When a key moves to
    /// another collection of the same lib (group change, graph rename, the pre-0.10 shared <c>Global_Text</c>),
    /// its existing translations are carried into the new collection before the old entry goes away.
    /// </summary>
    public static class UnityLocalizationSyncer
    {
        /// <summary>Root folder under which every lib's managed collections live (one subfolder per lib).</summary>
        internal const string CollectionsRoot = "Assets/Localization/Collections";

        private const string GlobalFolderName = "_Global";

        /// <summary>
        /// Entry point called via reflection from the builder core.
        /// Returns text collection names, a "|" separator, then asset collection names — the builder
        /// splits them into <c>UnityCollections</c> and <c>UnityAssetCollections</c> in the manifest.
        /// Asset Tables are created per flag type (Audio, Sprite, etc.) only for keys with matching flags.
        /// </summary>
        public static string[] SyncDatabase(string libName, LocalizationDatabase database,
            LocaleValidationMode validation, bool generateStringTables, bool generateAssetTables,
            string sourceLocaleCode = null)
            => SyncDatabase(libName, database, validation, generateStringTables, generateAssetTables,
                sourceLocaleCode, CollectionsRoot);

        /// <summary>Same as the public entry point, under an explicit collections root (tests use a throwaway one).</summary>
        internal static string[] SyncDatabase(string libName, LocalizationDatabase database,
            LocaleValidationMode validation, bool generateStringTables, bool generateAssetTables,
            string sourceLocaleCode, string collectionsRoot)
        {
            if (database == null) return System.Array.Empty<string>();

            var locales = LocalizationEditorSettings.GetLocales();
            if (locales == null || locales.Count == 0)
            {
                Logging.Warning("GraphLocalization.Validation", $"[UnityLocalizationSyncer] [{libName}] No locales configured in Project Settings > Localization.");
                return System.Array.Empty<string>();
            }

            var libFolder = EnsureLibFolder(collectionsRoot, libName);
            var sourceLocale = GetSourceLocale(libName, locales, sourceLocaleCode);
            var report = new SyncReport(libName);
            var managed = new List<StringTableCollection>();
            var desiredNames = new HashSet<string>(StringComparer.Ordinal);
            var assetCollectionNames = new List<string>();

            // Where every key of this lib must end up — computed first, so translations of keys that moved
            // can be read out of their old collection before any orphan entry is removed.
            var plan = new List<(string table, string folder, IReadOnlyList<LocalizationKeyEntry> keys)>();
            foreach (var graphEntry in database.Graphs)
            {
                var table = LocalizationTableNames.ForGraph(graphEntry.GraphName);
                plan.Add((table, $"{libFolder}/{table}", graphEntry.Keys));
            }
            foreach (var (group, keys) in database.GlobalKeysByGroup())
            {
                var table = LocalizationTableNames.ForGroup(libName, group);
                plan.Add((table, $"{libFolder}/{GlobalFolderName}/{table}", keys));
            }

            var carry = generateStringTables ? CollectCarry(libFolder, plan) : null;

            // Per-graph collections in Collections/{lib}/{graph}/, global groups in Collections/{lib}/_Global/{table}/.
            foreach (var (table, folder, keys) in plan)
            {
                var textName = LocalizationTableNames.TextCollection(table);
                desiredNames.Add(textName);
                EnsureFolderPath(folder);
                if (generateStringTables)
                {
                    var col = GetOrCreateCollection(textName, folder, report);
                    MoveCollectionIfNeeded(col, $"{folder}/{textName}", report);
                    EnsureTablesForAllLocales(col, locales);
                    SyncEntries(col, keys, sourceLocale, carry, report);
                    managed.Add(col);
                }
                if (generateAssetTables)
                    CreatePerTypeAssetCollections(table, folder, keys, locales, assetCollectionNames);
            }

            if (report.ValuesCarried > 0)
                Logging.Info("GraphLocalization.AutoBuild", $"[UnityLocalizationSyncer] [{libName}] Carried {report.ValuesCarried} " +
                    "existing translation(s) into the table their key moved to.");

            ReportOrphanCollections(libFolder, desiredNames, report);
            AssetDatabase.SaveAssets();
            ReportCoverage(managed, locales, sourceLocale, validation, report);

            // Return text names, then a separator, then asset names — the builder splits on it.
            var result = new List<string>(desiredNames);
            result.Add("|");
            result.AddRange(assetCollectionNames);
            return result.ToArray();
        }

        /// <summary>
        /// Non-empty values (key → locale code → value) of every key that currently sits in a collection of this
        /// lib OTHER than the one it is now desired in — including orphan collections (the pre-0.10 shared
        /// <c>Global_Text</c>, a renamed graph's old collection, an emptied group). Collections are visited in
        /// ordinal name order; the first non-empty value per (key, locale) wins.
        /// </summary>
        private static Dictionary<string, Dictionary<string, string>> CollectCarry(string libFolder,
            List<(string table, string folder, IReadOnlyList<LocalizationKeyEntry> keys)> plan)
        {
            var targetByKey = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (table, _, keys) in plan)
            {
                var textName = LocalizationTableNames.TextCollection(table);
                foreach (var k in keys)
                    if (k != null && !string.IsNullOrWhiteSpace(k.Key) && !targetByKey.ContainsKey(k.Key))
                        targetByKey[k.Key] = textName;
            }

            var carry = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            var all = LocalizationEditorSettings.GetStringTableCollections();
            if (all == null) return carry;

            var prefix = libFolder + "/";
            foreach (var col in all.Where(c => c != null && AssetDatabase.GetAssetPath(c).StartsWith(prefix, StringComparison.Ordinal))
                                   .OrderBy(c => c.TableCollectionName, StringComparer.Ordinal))
            {
                var shared = col.SharedData;
                if (shared == null) continue;
                foreach (var se in shared.Entries)
                {
                    if (se == null || string.IsNullOrEmpty(se.Key)) continue;
                    if (!targetByKey.TryGetValue(se.Key, out var target) || target == col.TableCollectionName) continue;

                    foreach (var table in col.StringTables)
                    {
                        var entry = table != null ? table.GetEntry(se.Id) : null;
                        if (entry == null || string.IsNullOrEmpty(entry.Value)) continue;
                        if (!carry.TryGetValue(se.Key, out var byLocale))
                            carry[se.Key] = byLocale = new Dictionary<string, string>(StringComparer.Ordinal);
                        var code = table.LocaleIdentifier.Code;
                        if (!byLocale.ContainsKey(code)) byLocale[code] = entry.Value;
                    }
                }
            }
            return carry;
        }

        /// <summary>
        /// The locale codes configured in <c>Project Settings &gt; Localization</c>, in their configured order
        /// (empty when none). Called via reflection from the core editor's locale catalog so a language picker
        /// can list the project's real locales without the core taking a hard dependency on com.unity.localization.
        /// </summary>
        public static string[] GetAvailableLocaleCodes()
        {
            var locales = LocalizationEditorSettings.GetLocales();
            if (locales == null) return System.Array.Empty<string>();
            return locales.Where(l => l != null).Select(l => l.Identifier.Code).ToArray();
        }

        // ── Folder ───────────────────────────────────────────────────────────────

        private static string EnsureLibFolder(string collectionsRoot, string libName)
        {
            var libPath = $"{collectionsRoot}/{Sanitize(libName)}";
            EnsureFolderPath(libPath);
            return libPath;
        }

        /// <summary>Creates every missing folder of an <c>Assets/…</c> path.</summary>
        private static void EnsureFolderPath(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
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
                    Logging.Warning("GraphLocalization.Validation", $"[UnityLocalizationSyncer] Could not move '{path}' → '{newPath}': {error}");
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
                if (!path.StartsWith(libFolder + "/", StringComparison.Ordinal)) continue;
                if (!desired.Contains(col.TableCollectionName))
                    report.OrphanCollections.Add(col.TableCollectionName);
            }

            if (report.OrphanCollections.Count > 0)
                Logging.Warning("GraphLocalization.Validation", $"[UnityLocalizationSyncer] [{report.LibName}] Orphan collection(s) under '{libFolder}' " +
                    $"no longer produced by the build (not deleted automatically): {string.Join(", ", report.OrphanCollections)}. " +
                    "Translations of any key that moved to another table were carried over; once you have checked " +
                    "nothing else uses them, these collections can be deleted.");
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

        private static void CreatePerTypeAssetCollections(string graphPrefix, string folder,
            IReadOnlyList<LocalizationKeyEntry> keys, IList<Locale> locales, List<string> outNames)
        {
            foreach (var (flag, typeName) in LocalizationTableNames.AssetTypes)
            {
                var colName = LocalizationTableNames.AssetCollection(graphPrefix, typeName);
                var filtered = new List<LocalizationKeyEntry>();
                foreach (var k in keys)
                    if (k != null && (k.AssetFlags & flag) != 0) filtered.Add(k);

                if (filtered.Count == 0) continue;

                EnsureAssetCollection(colName, folder, filtered, locales);
                outNames.Add(colName);
            }
        }

        // ── Entries ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Adds/keeps the desired keys and removes the rest. An EMPTY value (in any locale) is filled, in order of
        /// precedence, from the value carried over from the key's previous collection, then — source locale only —
        /// from the key's default hint. A non-empty value already in this collection is never overwritten.
        /// </summary>
        private static void SyncEntries(StringTableCollection col, IReadOnlyList<LocalizationKeyEntry> keys,
            Locale sourceLocale, Dictionary<string, Dictionary<string, string>> carry, SyncReport report)
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

                Dictionary<string, string> carried = null;
                if (carry != null) carry.TryGetValue(keyEntry.Key, out carried);

                foreach (var table in col.StringTables)
                {
                    if (table == null) continue;
                    var entry = table.GetEntry(sharedEntry.Id);
                    if (entry != null && !string.IsNullOrEmpty(entry.Value)) continue;

                    string value = null;
                    if (carried != null && carried.TryGetValue(table.LocaleIdentifier.Code, out var c) && !string.IsNullOrEmpty(c))
                    {
                        value = c;
                        report.ValuesCarried++;
                    }
                    else if (table == sourceTable && !string.IsNullOrEmpty(keyEntry.DefaultHint))
                        value = keyEntry.DefaultHint;

                    if (value == null) continue;
                    if (entry == null) table.AddEntry(sharedEntry.Id, value);
                    else entry.Value = value;
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
                if (validation == LocaleValidationMode.Strict) Logging.Error("GraphLocalization.Validation", msg);
                else Logging.Warning("GraphLocalization.Validation", msg);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        // Resolution order: the explicit code from LocalizationSettingsAsset.UnitySourceLocale, then the
        // Project Locale, then the first configured locale — the last one WITH a warning, because it is an
        // alphabetical accident: authored French text would silently be filed as the 'en' source column.
        private static Locale GetSourceLocale(string libName, IList<Locale> locales, string sourceLocaleCode)
        {
            if (!string.IsNullOrEmpty(sourceLocaleCode) && locales != null)
            {
                foreach (var l in locales)
                    if (l != null && l.Identifier.Code == sourceLocaleCode) return l;
                Logging.Warning("GraphLocalization.Validation", $"[UnityLocalizationSyncer] [{libName}] Unity Source Locale '{sourceLocaleCode}' " +
                    "(from the localization settings) is not among the project's locales " +
                    "(Project Settings ▸ Localization); falling back.");
            }
            try { var p = UnityLocalizationSettings.ProjectLocale; if (p != null) return p; } catch { }
            if (locales?.Count > 0)
            {
                Logging.Warning("GraphLocalization.Validation", $"[UnityLocalizationSyncer] [{libName}] No source locale declared: neither the " +
                    "settings' Unity Source Locale nor a Project Locale is set. Using the FIRST configured " +
                    $"locale ('{locales[0].Identifier.Code}') as the source — authored text will be pre-filled " +
                    "into that column. Set one of the two if this is not the authoring language.");
                return locales[0];
            }
            return null;
        }

        private static int Pct(int n, int d) => d <= 0 ? 100 : Mathf.RoundToInt(100f * n / d);

        private static string Sanitize(string name) => LocalizationTableNames.Sanitize(name);

        private sealed class SyncReport
        {
            public readonly string LibName;
            public int CollectionsCreated, CollectionsMoved, KeysAdded, KeysRemoved, ValuesCarried;
            public readonly List<string> OrphanCollections = new();
            public readonly List<(string code, int filled, int total, bool isSource)> Coverage = new();
            public LocaleValidationMode Validation;
            public SyncReport(string libName) => LibName = libName;
        }
    }
}
