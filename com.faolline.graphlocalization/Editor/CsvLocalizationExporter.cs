using System.Collections.Generic;
using System.Text;
using Faolline.GraphLogging;
using UnityEditor;
using UnityEngine;

namespace Faolline.GraphLocalization.Editor
{
    /// <summary>
    /// Generates a graph lib's CSV files (one per graph, one per global-key group) from its
    /// <see cref="LocalizationDatabase"/> (Csv mode).
    /// Format: <c>Key,&lt;locale1&gt;,&lt;locale2&gt;,…</c> — directly consumable by
    /// <see cref="CsvLocalizationProvider"/>. The source locale column is pre-filled with each key's
    /// default text; existing translations are preserved across rebuilds and orphan keys are dropped.
    /// <para>
    /// <b>Note:</b> CSV mode exports text keys only. Per-node <c>LocalizedAssetFlags</c> (Audio, Sprite, etc.)
    /// are not represented in CSV — localized asset management requires the Unity Localization backend (Both mode).
    /// </para>
    /// </summary>
    public static class CsvLocalizationExporter
    {
        /// <summary>
        /// Writes one CSV per graph (<c>{graph}.csv</c>) plus one per global-key group (<c>{lib}_{group}.csv</c>,
        /// see <see cref="LocalizationTableNames"/>) under <c>{outputFolder}/{libName}/</c>, merging with any
        /// existing files (translations preserved, orphan keys dropped). A key that moved to another file of the
        /// same lib (group change, graph rename, the pre-0.10 single <c>{lib}_Global.csv</c>) keeps its
        /// translations: missing cells are filled from the file it sat in before. Files in the lib folder this
        /// build no longer writes are reported, not deleted. Returns the asset paths written, so the builder can
        /// record them in the runtime manifest. The previous flat <c>{outputFolder}/{libName}.csv</c> is removed
        /// if present.
        /// </summary>
        public static List<string> Export(string libName, LocalizationDatabase db, IReadOnlyList<string> locales,
            string sourceLocale, string outputFolder, LocaleValidationMode validation)
        {
            var written = new List<string>();
            if (db == null) return written;
            if (locales == null || locales.Count == 0)
            {
                Logging.Warning("GraphLocalization.Validation", $"[CsvLocalizationExporter] [{libName}] No CSV locales configured. Skipping.");
                return written;
            }

            var libFolder = $"{outputFolder}/{Sanitize(libName)}";
            EnsureFolder(libFolder);

            // Migrate away from the old flat single-file layout.
            var oldFlat = $"{outputFolder}/{Sanitize(libName)}.csv";
            if (System.IO.File.Exists(oldFlat)) AssetDatabase.DeleteAsset(oldFlat);

            // Every file this build writes, with its keys: Csv/{lib}/{graph}.csv and Csv/{lib}/{lib}_{group}.csv.
            var files = new List<(string path, string label, List<(string key, string hint)> keys)>();
            foreach (var graph in db.Graphs)
                files.Add(($"{libFolder}/{LocalizationTableNames.ForGraph(graph.GraphName)}.csv",
                    $"{libName}/{graph.GraphName}", CollectKeys(graph.Keys)));
            foreach (var (group, keys) in db.GlobalKeysByGroup())
            {
                var table = LocalizationTableNames.ForGroup(libName, group);
                files.Add(($"{libFolder}/{table}.csv", $"{libName}/{table}", CollectKeys(keys)));
            }

            var carry = CollectCarry(libFolder, files);
            foreach (var (path, label, keys) in files)
            {
                WriteCsv(path, label, keys, locales, sourceLocale, carry, validation);
                written.Add(path);
            }

            ReportUnusedFiles(libName, libFolder, written);
            return written;
        }

        private static void WriteCsv(string path, string label, IReadOnlyList<(string key, string hint)> desired,
            IReadOnlyList<string> locales, string sourceLocale,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> carry, LocaleValidationMode validation)
        {
            var existing = System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : null;
            var csv = BuildCsv(existing, desired, locales, sourceLocale, carry, out var coverage, out _);
            System.IO.File.WriteAllText(path, csv);
            AssetDatabase.ImportAsset(path);
            ReportCoverage(label, coverage, validation);
        }

        /// <summary>
        /// Non-empty cells (key → locale → value) of every key currently in a CSV of this lib folder OTHER than the
        /// file it is now desired in — including files this build no longer writes. Files are read in ordinal
        /// name order; the first non-empty value per (key, locale) wins.
        /// </summary>
        private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> CollectCarry(string libFolder,
            List<(string path, string label, List<(string key, string hint)> keys)> files)
        {
            var carry = new Dictionary<string, IReadOnlyDictionary<string, string>>();
            if (!System.IO.Directory.Exists(libFolder)) return carry;

            var targetByKey = new Dictionary<string, string>();
            foreach (var (path, _, keys) in files)
                foreach (var (key, _) in keys)
                    if (!targetByKey.ContainsKey(key)) targetByKey[key] = Normalize(path);

            var existingFiles = System.IO.Directory.GetFiles(libFolder, "*.csv");
            System.Array.Sort(existingFiles, System.StringComparer.Ordinal);
            foreach (var file in existingFiles)
            {
                var filePath = Normalize(file);
                var rows = ParseCsv(System.IO.File.ReadAllText(file), out _);
                foreach (var row in rows)
                {
                    if (!targetByKey.TryGetValue(row.Key, out var target) || target == filePath) continue;
                    if (!carry.TryGetValue(row.Key, out var existing))
                        carry[row.Key] = existing = new Dictionary<string, string>();
                    var byLocale = (Dictionary<string, string>)existing;
                    foreach (var cell in row.Value)
                        if (!string.IsNullOrEmpty(cell.Value) && !byLocale.ContainsKey(cell.Key)) byLocale[cell.Key] = cell.Value;
                }
            }
            return carry;
        }

        private static void ReportUnusedFiles(string libName, string libFolder, List<string> written)
        {
            if (!System.IO.Directory.Exists(libFolder)) return;
            var writtenSet = new HashSet<string>();
            foreach (var w in written) writtenSet.Add(Normalize(w));

            var unused = new List<string>();
            foreach (var file in System.IO.Directory.GetFiles(libFolder, "*.csv"))
                if (!writtenSet.Contains(Normalize(file))) unused.Add(System.IO.Path.GetFileName(file));

            if (unused.Count > 0)
            {
                unused.Sort(System.StringComparer.Ordinal);
                Logging.Warning("GraphLocalization.Validation", $"[CsvLocalizationExporter] [{libName}] CSV file(s) under '{libFolder}' " +
                    $"no longer produced by the build (not deleted automatically, not loaded at runtime): {string.Join(", ", unused)}. " +
                    "Translations of any key that moved to another file were carried over; once you have checked nothing " +
                    "else uses them, these files can be deleted.");
            }
        }

        private static string Normalize(string path) => path.Replace('\\', '/');

        // ── Desired keys ────────────────────────────────────────────────────────────

        /// <summary>Flattens a key list into an ordered, de-duplicated (key, hint) list.</summary>
        private static List<(string key, string hint)> CollectKeys(IReadOnlyList<LocalizationKeyEntry> keys)
        {
            var seen = new Dictionary<string, string>();
            var ordered = new List<string>();

            void Add(string key, string hint)
            {
                if (string.IsNullOrWhiteSpace(key)) return;
                var k = key.Trim();
                if (!seen.ContainsKey(k)) { seen[k] = hint ?? string.Empty; ordered.Add(k); }
                else if (string.IsNullOrEmpty(seen[k]) && !string.IsNullOrEmpty(hint)) seen[k] = hint;
            }

            if (keys != null)
                foreach (var entry in keys)
                    Add(entry.Key, entry.DefaultHint);

            var result = new List<(string, string)>(ordered.Count);
            foreach (var k in ordered) result.Add((k, seen[k]));
            return result;
        }

        // ── Pure CSV build (testable) ─────────────────────────────────────────────────

        /// <summary>
        /// Builds the CSV text. Merges <paramref name="existingCsv"/> (preserving translations), pre-fills
        /// the source-locale column from each key's hint when empty, drops orphan keys, and emits exactly
        /// the requested <paramref name="locales"/> columns in order.
        /// </summary>
        public static string BuildCsv(string existingCsv, IReadOnlyList<(string key, string hint)> desired,
            IReadOnlyList<string> locales, string sourceLocale,
            out List<(string locale, int filled, int total)> coverage, out int orphansRemoved)
            => BuildCsv(existingCsv, desired, locales, sourceLocale, null, out coverage, out orphansRemoved);

        /// <summary>
        /// As the overload without <paramref name="carry"/>, plus: an EMPTY cell (key absent from
        /// <paramref name="existingCsv"/>, or present with an empty value) is filled from <paramref name="carry"/>
        /// (key → locale → value, the translations the key had in the file it moved from) before the source-locale
        /// hint applies. A non-empty existing cell is never overwritten. Null <paramref name="carry"/> = no carry-over.
        /// </summary>
        public static string BuildCsv(string existingCsv, IReadOnlyList<(string key, string hint)> desired,
            IReadOnlyList<string> locales, string sourceLocale,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> carry,
            out List<(string locale, int filled, int total)> coverage, out int orphansRemoved)
        {
            var existing = ParseCsv(existingCsv, out _);
            int existingCount = existing.Count;

            var desiredKeys = new HashSet<string>();
            foreach (var (key, _) in desired) desiredKeys.Add(key);
            orphansRemoved = 0;
            foreach (var k in existing.Keys)
                if (!desiredKeys.Contains(k)) orphansRemoved++;

            // Build rows for desired keys, preserving existing translations.
            var sb = new StringBuilder();
            sb.Append("Key");
            foreach (var loc in locales) { sb.Append(','); sb.Append(Escape(loc)); }
            sb.Append('\n');

            var filledPerLocale = new Dictionary<string, int>();
            foreach (var loc in locales) filledPerLocale[loc] = 0;

            foreach (var (key, hint) in desired)
            {
                existing.TryGetValue(key, out var row);
                IReadOnlyDictionary<string, string> carried = null;
                if (carry != null) carry.TryGetValue(key, out carried);
                sb.Append(Escape(key));
                foreach (var loc in locales)
                {
                    string value = row != null && row.TryGetValue(loc, out var v) ? v : string.Empty;
                    // Then the value carried over from the file the key moved from.
                    if (string.IsNullOrEmpty(value) && carried != null && carried.TryGetValue(loc, out var c))
                        value = c ?? string.Empty;
                    // Pre-fill the source locale from the hint when no existing value.
                    if (string.IsNullOrEmpty(value) && loc == sourceLocale && !string.IsNullOrEmpty(hint))
                        value = hint;

                    if (!string.IsNullOrEmpty(value)) filledPerLocale[loc]++;
                    sb.Append(','); sb.Append(Escape(value));
                }
                sb.Append('\n');
            }

            coverage = new List<(string, int, int)>();
            foreach (var loc in locales) coverage.Add((loc, filledPerLocale[loc], desired.Count));

            return sb.ToString();
        }

        // ── Reporting ─────────────────────────────────────────────────────────────────

        private static void ReportCoverage(string libName, List<(string locale, int filled, int total)> coverage,
            LocaleValidationMode validation)
        {
            if (validation == LocaleValidationMode.Permissive) return;
            foreach (var (loc, filled, total) in coverage)
            {
                if (total == 0 || filled >= total) continue;
                var msg = $"[CsvLocalizationExporter] [{libName}] Locale '{loc}': {filled}/{total} ({Pct(filled, total)}%), {total - filled} missing.";
                if (validation == LocaleValidationMode.Strict) Logging.Error("GraphLocalization.Validation", msg);
                else Logging.Warning("GraphLocalization.Validation", msg);
            }
        }

        // ── CSV parse / escape ──────────────────────────────────────────────────────

        /// <summary>Parses CSV into key → (locale → value). Returns an empty map for null/empty input.</summary>
        private static Dictionary<string, Dictionary<string, string>> ParseCsv(string csv, out List<string> locales)
        {
            locales = new List<string>();
            var table = new Dictionary<string, Dictionary<string, string>>();
            var records = LocalizationCsv.ParseRecords(csv);
            if (records.Count == 0) return table;

            var header = records[0];
            if (header.Count < 2) return table;
            for (int c = 1; c < header.Count; c++) locales.Add(header[c].Trim());

            for (int i = 1; i < records.Count; i++)
            {
                var cols = records[i];
                if (cols.Count == 0) continue;
                var key = cols[0].Trim();
                if (string.IsNullOrEmpty(key)) continue;

                var row = new Dictionary<string, string>();
                for (int c = 1; c < header.Count && c < cols.Count; c++)
                    row[locales[c - 1]] = cols[c];
                table[key] = row;
            }
            return table;
        }

        private static string Escape(string field) => LocalizationCsv.Escape(field);

        // ── Helpers ───────────────────────────────────────────────────────────────────

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parts = folder.Split('/');
            var current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static int Pct(int n, int d) => d <= 0 ? 100 : Mathf.RoundToInt(100f * n / d);

        private static string Sanitize(string name) => LocalizationTableNames.Sanitize(name);
    }
}
