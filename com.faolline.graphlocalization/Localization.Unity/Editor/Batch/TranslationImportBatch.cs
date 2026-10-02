using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Faolline.GraphLocalization.Editor;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.CSV;
using UnityEditor.Localization.Plugins.CSV.Columns;

namespace Faolline.GraphLocalization.Unity.Editor
{
    /// <summary>
    /// Minimal -executeMethod entry point that imports externally-authored translation CSVs
    /// (Dialogue Studio's export format -- "Key" + one column per locale, RFC4180) into the
    /// String Table Collections <see cref="UnityLocalizationSyncer"/> already created for this
    /// project's graphs. Never creates a collection itself -- an import for a dialogue whose
    /// collection doesn't exist yet (i.e. its graph was never generated/synced) is reported as a
    /// failure, not silently skipped nor auto-created, same never-guess precedent as the rest of
    /// this ecosystem's tooling.
    ///
    /// Command line: -dialogueTranslationsDir &lt;path&gt; (every "*.csv" in this folder is
    /// imported into the collection of the graph the file is named after --
    /// <see cref="LocalizationTableNames.ForGraph"/>, exactly the rule <see cref="UnityLocalizationSyncer"/>
    /// used to create it, so no separate manifest/lookup is needed) and/or -speakersCsv &lt;path&gt;
    /// (a single CSV of global keys -- dialogue-studio's speakers.csv -- routed row by row to the
    /// collection holding each key, see <see cref="GlobalKeyCsvRouter"/>: speaker names live in one
    /// collection per group; a key held by no collection, or by several, is a failure). Locale columns
    /// are matched by locale code ("fr", or Unity's own "French(fr)"); a column matching no project
    /// locale is a failure. At least one flag must be given. Exits 0 only if every requested import
    /// succeeded; valid rows are imported even when others fail.
    /// </summary>
    public static class TranslationImportBatch
    {
        public static void Run()
        {
            try
            {
                RunInternal();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[TranslationImportBatch] Fatal: {ex}");
                EditorApplication.Exit(1);
            }
        }

        static void RunInternal()
        {
            var args = ParseArgs(Environment.GetCommandLineArgs());

            var failures = new List<string>();
            var imported = 0;
            var anyFlag = false;

            if (args.TryGetValue("-speakersCsv", out var speakersCsv))
            {
                anyFlag = true;
                var (count, speakerFailures) = ImportGlobalKeysCsv(speakersCsv, UnityLocalizationSyncer.CollectionsRoot);
                imported += count;
                failures.AddRange(speakerFailures);
            }

            if (args.TryGetValue("-dialogueTranslationsDir", out var dir))
            {
                anyFlag = true;
                if (!Directory.Exists(dir))
                {
                    failures.Add($"-dialogueTranslationsDir does not exist: {dir}");
                }
                else
                {
                    foreach (var csvPath in Directory.GetFiles(dir, "*.csv"))
                    {
                        var collectionName = LocalizationTableNames.TextCollection(
                            LocalizationTableNames.ForGraph(Path.GetFileNameWithoutExtension(csvPath)));
                        if (TryImport(csvPath, collectionName, failures))
                            imported++;
                    }
                }
            }

            if (!anyFlag)
                throw new InvalidOperationException("Nothing to import: neither -speakersCsv nor -dialogueTranslationsDir given.");

            // Persist the imported tables: batchmode exit does not save dirty assets on its own.
            AssetDatabase.SaveAssets();

            foreach (var failure in failures)
                Console.Error.WriteLine($"[TranslationImportBatch] {failure}");

            Console.WriteLine($"[TranslationImportBatch] Imported {imported} collection(s), {failures.Count} failure(s).");

            EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
        }

        /// <summary>
        /// Imports a CSV of global keys (e.g. speakers.csv) into the String Table Collections under
        /// <paramref name="collectionsRoot"/>, each row into the one collection holding its key. Returns how many
        /// collections were imported into, and one failure per problem (missing file, unroutable key, unknown
        /// locale column, import error). Never calls <c>EditorApplication.Exit</c>.
        /// </summary>
        internal static (int imported, List<string> failures) ImportGlobalKeysCsv(string csvPath, string collectionsRoot)
        {
            var failures = new List<string>();
            if (!File.Exists(csvPath))
            {
                failures.Add($"-speakersCsv not found: {csvPath}");
                return (0, failures);
            }

            var prefix = collectionsRoot.TrimEnd('/') + "/";
            var collections = new Dictionary<string, StringTableCollection>(StringComparer.Ordinal);
            var holdersByKey = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var col in LocalizationEditorSettings.GetStringTableCollections())
            {
                if (col == null || col.SharedData == null) continue;
                if (!AssetDatabase.GetAssetPath(col).StartsWith(prefix, StringComparison.Ordinal)) continue;
                collections[col.TableCollectionName] = col;
                foreach (var entry in col.SharedData.Entries)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.Key)) continue;
                    if (!holdersByKey.TryGetValue(entry.Key, out var holders))
                        holdersByKey[entry.Key] = holders = new List<string>();
                    holders.Add(col.TableCollectionName);
                }
            }

            var routed = GlobalKeyCsvRouter.Route(File.ReadAllText(csvPath),
                key => holdersByKey.TryGetValue(key, out var holders) ? holders : (IReadOnlyList<string>)Array.Empty<string>());
            foreach (var failure in routed.Failures) failures.Add($"{csvPath}: {failure}");

            var imported = 0;
            foreach (var bucket in routed.CsvByCollection)
                if (ImportText(bucket.Value, collections[bucket.Key], csvPath, failures))
                    imported++;
            return (imported, failures);
        }

        static bool TryImport(string csvPath, string collectionName, List<string> failures)
        {
            if (!File.Exists(csvPath))
            {
                failures.Add($"CSV not found for collection '{collectionName}': {csvPath}");
                return false;
            }

            var collection = LocalizationEditorSettings.GetStringTableCollections()
                .FirstOrDefault(c => c.TableCollectionName == collectionName);
            if (collection == null)
            {
                failures.Add($"No String Table Collection named '{collectionName}' -- run the graph generation/sync first.");
                return false;
            }

            return ImportText(File.ReadAllText(csvPath), collection, csvPath, failures);
        }

        /// <summary>
        /// Imports CSV text into <paramref name="collection"/> with columns mapped from ITS header: "Key", then one
        /// locale column per header cell, matched by locale code (<see cref="GlobalKeyCsvRouter.LocaleCodeOfColumn"/>).
        /// Unity's default mapping only recognizes its own "French(fr)" headers, so a "Key,en,fr" file would
        /// otherwise import empty values. Entries absent from the CSV are left untouched.
        /// </summary>
        static bool ImportText(string csvText, StringTableCollection collection, string sourcePath, List<string> failures)
        {
            try
            {
                var records = LocalizationCsv.ParseRecords(csvText);
                if (records.Count == 0) return false;

                var columns = new List<CsvColumns> { new KeyIdColumns { KeyFieldName = records[0][0].Trim(), IncludeSharedComments = false } };
                var locales = LocalizationEditorSettings.GetLocales();
                for (int i = 1; i < records[0].Count; i++)
                {
                    var header = records[0][i];
                    var code = GlobalKeyCsvRouter.LocaleCodeOfColumn(header);
                    var locale = locales?.FirstOrDefault(l => l != null && l.Identifier.Code == code);
                    if (locale == null)
                    {
                        failures.Add($"{sourcePath}: column '{header}' matches no project locale -- its values were not imported.");
                        continue;
                    }
                    columns.Add(new LocaleColumns { LocaleIdentifier = locale.Identifier, FieldName = header, IncludeComments = false });
                }

                using var reader = new StringReader(csvText);
                Csv.ImportInto(reader, collection, columns);
                return true;
            }
            catch (Exception ex)
            {
                failures.Add($"Import failed for collection '{collection.TableCollectionName}' ({sourcePath}): {ex.Message}");
                return false;
            }
        }

        static Dictionary<string, string> ParseArgs(string[] rawArgs)
        {
            var map = new Dictionary<string, string>();
            for (var i = 0; i < rawArgs.Length - 1; i++)
                if (rawArgs[i].StartsWith("-"))
                    map[rawArgs[i]] = rawArgs[i + 1];
            return map;
        }
    }
}
