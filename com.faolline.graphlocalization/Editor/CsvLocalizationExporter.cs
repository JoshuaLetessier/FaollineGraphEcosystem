using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Faolline.GraphLocalization.Editor
{
    /// <summary>
    /// Generates one CSV file per graph lib from its <see cref="LocalizationDatabase"/> (Csv mode).
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
        /// Writes one CSV per graph plus a global CSV under <c>{outputFolder}/{libName}/</c>, merging with any
        /// existing files (translations preserved, orphan keys dropped). Returns the asset paths written, so
        /// the builder can record them in the runtime manifest. The previous flat
        /// <c>{outputFolder}/{libName}.csv</c> is removed if present.
        /// </summary>
        public static List<string> Export(string libName, LocalizationDatabase db, IReadOnlyList<string> locales,
            string sourceLocale, string outputFolder, LocaleValidationMode validation)
        {
            var written = new List<string>();
            if (db == null) return written;
            if (locales == null || locales.Count == 0)
            {
                Debug.LogWarning($"[CsvLocalizationExporter] [{libName}] No CSV locales configured. Skipping.");
                return written;
            }

            var libFolder = $"{outputFolder}/{Sanitize(libName)}";
            EnsureFolder(libFolder);

            // Migrate away from the old flat single-file layout.
            var oldFlat = $"{outputFolder}/{Sanitize(libName)}.csv";
            if (System.IO.File.Exists(oldFlat)) AssetDatabase.DeleteAsset(oldFlat);

            // One file per graph: Csv/{lib}/{graph}.csv
            foreach (var graph in db.Graphs)
            {
                var path = $"{libFolder}/{Sanitize(graph.GraphName)}.csv";
                WriteCsv(path, $"{libName}/{graph.GraphName}", CollectKeys(graph.Keys), locales, sourceLocale, validation);
                written.Add(path);
            }

            // Global keys (speakers, etc.): Csv/{lib}/{lib}_Global.csv
            if (db.GlobalKeys.Count > 0)
            {
                var path = $"{libFolder}/{Sanitize(libName)}_Global.csv";
                WriteCsv(path, $"{libName}/_Global", CollectKeys(db.GlobalKeys), locales, sourceLocale, validation);
                written.Add(path);
            }

            return written;
        }

        private static void WriteCsv(string path, string label, IReadOnlyList<(string key, string hint)> desired,
            IReadOnlyList<string> locales, string sourceLocale, LocaleValidationMode validation)
        {
            var existing = System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : null;
            var csv = BuildCsv(existing, desired, locales, sourceLocale, out var coverage, out int removed);
            System.IO.File.WriteAllText(path, csv);
            AssetDatabase.ImportAsset(path);
            ReportCoverage(label, coverage, validation, removed);
        }

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
                sb.Append(Escape(key));
                foreach (var loc in locales)
                {
                    string value = row != null && row.TryGetValue(loc, out var v) ? v : string.Empty;
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
            LocaleValidationMode validation, int removed)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[CsvLocalizationExporter] [{libName}] CSV written. Orphans removed: {removed}");
            foreach (var (loc, filled, total) in coverage)
                sb.AppendLine($"  {loc}: {filled}/{total} ({Pct(filled, total)}%)");
            Debug.Log(sb.ToString().TrimEnd());

            if (validation == LocaleValidationMode.Permissive) return;
            foreach (var (loc, filled, total) in coverage)
            {
                if (total == 0 || filled >= total) continue;
                var msg = $"[CsvLocalizationExporter] [{libName}] Locale '{loc}': {filled}/{total} ({Pct(filled, total)}%), {total - filled} missing.";
                if (validation == LocaleValidationMode.Strict) Debug.LogError(msg);
                else Debug.LogWarning(msg);
            }
        }

        // ── CSV parse / escape ──────────────────────────────────────────────────────

        /// <summary>Parses CSV into key → (locale → value). Returns an empty map for null/empty input.</summary>
        private static Dictionary<string, Dictionary<string, string>> ParseCsv(string csv, out List<string> locales)
        {
            locales = new List<string>();
            var table = new Dictionary<string, Dictionary<string, string>>();
            var records = ParseRecords(csv);
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

        // Full-text RFC4180 tokenizer. Unlike a Split('\n')-then-parse approach, a quoted field may contain
        // commas, doubled quotes AND newlines — required so a multi-line value written by Escape survives the
        // merge-preserve pass of the next rebuild instead of corrupting the row.
        // Kept in sync with the identical copy in CsvLocalizationProvider (runtime assembly).
        private static List<List<string>> ParseRecords(string csvText)
        {
            var records = new List<List<string>>();
            if (string.IsNullOrEmpty(csvText)) return records;

            var row = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            void EndCell() { row.Add(sb.ToString()); sb.Clear(); }
            void EndRecord()
            {
                EndCell();
                // A blank/whitespace-only line parses as a single blank cell — skip it.
                if (row.Count > 1 || row[0].Trim().Length > 0)
                    records.Add(new List<string>(row));
                row.Clear();
            }

            for (int i = 0; i < csvText.Length; i++)
            {
                char ch = csvText[i];
                if (inQuotes)
                {
                    if (ch == '"') { if (i + 1 < csvText.Length && csvText[i + 1] == '"') { sb.Append('"'); i++; } else inQuotes = false; }
                    else sb.Append(ch);
                }
                else if (ch == '"') inQuotes = true;
                else if (ch == ',') EndCell();
                else if (ch == '\r') { if (i + 1 >= csvText.Length || csvText[i + 1] != '\n') EndRecord(); }   // lone \r ends the record; \r\n defers to the \n
                else if (ch == '\n') EndRecord();
                else sb.Append(ch);
            }
            if (sb.Length > 0 || row.Count > 0) EndRecord();
            return records;
        }

        private static string Escape(string field)
        {
            field ??= string.Empty;
            if (field.IndexOf(',') >= 0 || field.IndexOf('"') >= 0 || field.IndexOf('\n') >= 0 || field.IndexOf('\r') >= 0)
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            return field;
        }

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

        private static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unnamed";
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            var chars = System.Array.ConvertAll(name.ToCharArray(), c => System.Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            return new string(chars);
        }
    }
}
