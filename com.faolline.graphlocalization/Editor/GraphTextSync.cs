using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Faolline.GraphLogging;
using UnityEditor;

namespace Faolline.GraphLocalization.Editor
{
    /// <summary>
    /// Keeps graph texts in step with their localization tables (054). The build only fills EMPTY cells, so once a
    /// text is in a table, the table is the reference: the runtime reads only the table, and a graph text (a node
    /// Title, a speaker name…) is just the seed copied on the key's first build. This class finds the graph texts
    /// that no longer match the source-language cell of their table (<see cref="FindDrifts()"/>), reports them
    /// (every build, and <see cref="CheckMenuPath"/>), and — only when asked — copies the table text back into the
    /// graphs through each lib's <see cref="IGraphTextWriter"/> (<see cref="Apply"/>, <see cref="UpdateMenuPath"/>).
    /// It never writes to a table.
    /// </summary>
    public static class GraphTextSync
    {
        public const string CheckMenuPath = "Faolline/Localization/Check Graph Texts Against Tables";
        public const string UpdateMenuPath = "Faolline/Localization/Update Graph Texts From Tables";

        private const string LogCategory = "GraphLocalization.Validation";
        private const int ReportedEntries = 5;

        // ── Detection ───────────────────────────────────────────────────────────

        /// <summary>
        /// Pure comparison: for every key of <paramref name="database"/>, in build order (graph tables, then
        /// global-key group tables), its graph text (<see cref="LocalizationKeyEntry.DefaultHint"/>) against
        /// <paramref name="tableTexts"/> (key → source-language cell). Surrounding whitespace and <c>\r\n</c> vs
        /// <c>\n</c> are ignored; a key with no or an empty cell is skipped (the next build fills it). A key filed
        /// twice is checked once, in the first table that holds it (as the build does).
        /// </summary>
        public static List<GraphTextDrift> Compare(string libName, LocalizationDatabase database,
            IReadOnlyDictionary<string, string> tableTexts)
        {
            var result = new List<GraphTextDrift>();
            if (database == null || tableTexts == null) return result;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var t in database.Tables(libName))
            {
                foreach (var entry in t.Keys)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Key)) continue;
                    var key = entry.Key.Trim();
                    if (!seen.Add(key)) continue;
                    if (!tableTexts.TryGetValue(key, out var tableText) || string.IsNullOrEmpty(tableText)) continue;

                    var graphText = entry.DefaultHint ?? string.Empty;
                    if (Comparable(graphText) == Comparable(tableText)) continue;

                    result.Add(new GraphTextDrift(libName, t.GraphGuid, t.GraphName, t.Table, key, entry.Type,
                        entry.NodeId, graphText, NormalizeLineEndings(tableText)));
                }
            }
            return result;
        }

        /// <summary>
        /// The source-language cell (key → text, non-empty only) of every key of <paramref name="database"/>, read
        /// from the backend and the source language the build uses (see <see cref="LocalizationBuilderCore"/>):
        /// Unity Localization → the settings' Unity Source Locale (else Project Locale, else first locale);
        /// CSV → the first CSV locale under the CSV output folder. Empty when there is nothing to read.
        /// </summary>
        public static IReadOnlyDictionary<string, string> ReadTableTexts(string libName, LocalizationDatabase database)
        {
            var empty = new Dictionary<string, string>();
            if (database == null) return empty;

            var settings = LocalizationSettingsLoader.Load();
            var mode = settings != null ? settings.Mode : LocalizationMode.Csv;

            if (mode == LocalizationMode.UnityLocalization)
            {
                if (settings == null || !settings.GeneratesStringTables) return empty;
                var syncer = Type.GetType("Faolline.GraphLocalization.Unity.Editor.UnityLocalizationSyncer, " +
                                          "com.faolline.graphlocalization.Localization.Unity.Editor");
                var method = syncer?.GetMethod("ReadSourceTexts",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (method == null) return empty;
                return method.Invoke(null, new object[] { libName, database, settings.UnitySourceLocale })
                    as IReadOnlyDictionary<string, string> ?? empty;
            }

            var locales = settings != null ? settings.CsvLocales : new[] { "en" };
            var sourceLocale = locales != null && locales.Count > 0 ? locales[0] : "en";
            var folder = settings != null ? settings.CsvOutputFolder : "Assets/Localization/Csv";
            return CsvLocalizationExporter.ReadSourceTexts(libName, database, sourceLocale, folder);
        }

        /// <summary>Every drift of every lib the build knows (<see cref="GraphLocalizationAdapterRegistry.DiscoverAdapters"/>).</summary>
        public static List<GraphTextDrift> FindDrifts() => FindDrifts(GraphLocalizationAdapterRegistry.DiscoverAdapters());

        /// <summary>Every drift of the given adapters' libs: scan each, read its tables, compare.</summary>
        public static List<GraphTextDrift> FindDrifts(IEnumerable<IGraphLocalizationAdapter> adapters)
        {
            var result = new List<GraphTextDrift>();
            if (adapters == null) return result;
            foreach (var adapter in adapters)
            {
                if (adapter == null) continue;
                var db = new LocalizationDatabase();
                adapter.ScanAndIndex(db);
                result.AddRange(Compare(adapter.LibName, db, ReadTableTexts(adapter.LibName, db)));
            }
            return result;
        }

        /// <summary>
        /// Called by the build after a lib's export, with the database it just exported: logs the lib's drifts as
        /// one warning. Never throws (a failed check must not fail the build).
        /// </summary>
        internal static void ReportAfterBuild(string libName, LocalizationDatabase database)
        {
            try
            {
                var report = FormatReport(libName, Compare(libName, database, ReadTableTexts(libName, database)));
                if (report != null) Logging.Warning(LogCategory, report);
            }
            catch (Exception ex)
            {
                Logging.Error(LogCategory, $"[GraphTextSync] [{libName}] Graph-text check failed: {ex}");
            }
        }

        /// <summary>
        /// The warning text for one lib's drifts (null when there are none): the count, the first few entries with
        /// both texts, and what to do.
        /// </summary>
        public static string FormatReport(string libName, IReadOnlyList<GraphTextDrift> drifts)
        {
            if (drifts == null || drifts.Count == 0) return null;

            var sb = new StringBuilder();
            sb.Append($"[GraphTextSync] [{libName}] {drifts.Count} graph text");
            sb.Append(drifts.Count == 1 ? " differs from its localization table" : "s differ from their localization tables");
            sb.Append(". The table is the reference: the game shows the table text, " +
                      "and the build never overwrites a filled cell, so edit texts in the table. To bring the graphs " +
                      $"back in step, run {MenuLabel(UpdateMenuPath)}.");
            for (int i = 0; i < drifts.Count && i < ReportedEntries; i++)
            {
                var d = drifts[i];
                if (d == null) continue;
                sb.Append($"\n  - {d.Location}: graph \"{Shorten(d.GraphText)}\" ≠ table \"{Shorten(d.TableText)}\"");
            }
            if (drifts.Count > ReportedEntries)
                sb.Append($"\n  … and {drifts.Count - ReportedEntries} more ({MenuLabel(CheckMenuPath)} lists them all).");
            return sb.ToString();
        }

        // ── Write-back (table → graph) ──────────────────────────────────────────

        /// <summary>Copies one drift's table text into its graph (Undo), then saves unless <paramref name="save"/> is false.</summary>
        public static bool Apply(GraphTextDrift drift, bool save = true) => ApplyAll(new[] { drift }, save) == 1;

        /// <summary>Copies every drift's table text into its graph through the discovered adapters.</summary>
        public static int ApplyAll(IEnumerable<GraphTextDrift> drifts, bool save = true)
            => ApplyAll(drifts, GraphLocalizationAdapterRegistry.DiscoverAdapters(), save);

        /// <summary>
        /// Copies every drift's table text into its graph through the <see cref="IGraphTextWriter"/> of the adapter
        /// whose <see cref="IGraphLocalizationAdapter.LibName"/> matches. Returns how many were written; the others
        /// (no writer, text not found) are reported in one warning. Saves the assets once at the end when
        /// <paramref name="save"/> is true and something was written.
        /// </summary>
        public static int ApplyAll(IEnumerable<GraphTextDrift> drifts, IEnumerable<IGraphLocalizationAdapter> adapters,
            bool save = true)
        {
            if (drifts == null) return 0;
            var writers = new Dictionary<string, IGraphTextWriter>(StringComparer.Ordinal);
            if (adapters != null)
                foreach (var a in adapters)
                    if (a is IGraphTextWriter w && !string.IsNullOrEmpty(a.LibName) && !writers.ContainsKey(a.LibName))
                        writers[a.LibName] = w;

            int applied = 0;
            var skipped = new List<GraphTextDrift>();
            foreach (var drift in drifts)
            {
                if (drift == null) continue;
                if (writers.TryGetValue(drift.LibName, out var writer) && writer.WriteGraphText(drift)) applied++;
                else skipped.Add(drift);
            }

            if (skipped.Count > 0)
                Logging.Warning(LogCategory, $"[GraphTextSync] {skipped.Count} graph text(s) not updated (no writer for " +
                    "their lib, or the text is no longer in the graph): " +
                    string.Join(", ", skipped.Take(ReportedEntries).Select(d => $"{d.LibName}: {d.Location}")) +
                    (skipped.Count > ReportedEntries ? ", …" : "."));

            if (save && applied > 0) AssetDatabase.SaveAssets();
            return applied;
        }

        // ── Menus ───────────────────────────────────────────────────────────────

        [MenuItem(CheckMenuPath)]
        private static void CheckMenu()
        {
            int total = 0;
            foreach (var adapter in GraphLocalizationAdapterRegistry.DiscoverAdapters())
            {
                var db = new LocalizationDatabase();
                adapter.ScanAndIndex(db);
                var drifts = Compare(adapter.LibName, db, ReadTableTexts(adapter.LibName, db));
                total += drifts.Count;
                foreach (var d in drifts) Logging.Warning(LogCategory, $"[GraphTextSync] [{adapter.LibName}] {d}");
            }
            if (total == 0) Logging.Info(LogCategory, "[GraphTextSync] Every graph text matches its localization table.");
            else Logging.Warning(LogCategory, $"[GraphTextSync] {total} graph text(s) differ from their table (listed above). " +
                $"The table is the reference; run {MenuLabel(UpdateMenuPath)} to copy the table texts into the graphs.");
        }

        [MenuItem(UpdateMenuPath)]
        private static void UpdateMenu()
        {
            var drifts = FindDrifts();
            if (drifts.Count == 0)
            {
                Logging.Info(LogCategory, "[GraphTextSync] Every graph text already matches its localization table.");
                return;
            }
            if (!EditorUtility.DisplayDialog("Update Graph Texts From Tables",
                    $"{drifts.Count} graph text(s) differ from their localization table. Replace each with the table " +
                    "text (the table is the reference)? This can be undone.", "Update", "Cancel"))
                return;

            int applied = ApplyAll(drifts);
            Logging.Info(LogCategory, $"[GraphTextSync] Updated {applied}/{drifts.Count} graph text(s) from their table.");
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private static string NormalizeLineEndings(string s) => (s ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');

        private static string Comparable(string s) => NormalizeLineEndings(s).Trim();

        private static string MenuLabel(string path) => path.Replace("/", " ▸ ");

        private static string Shorten(string s)
        {
            var oneLine = NormalizeLineEndings(s).Replace("\n", "⏎");
            return oneLine.Length <= 80 ? oneLine : oneLine.Substring(0, 77) + "…";
        }
    }
}
