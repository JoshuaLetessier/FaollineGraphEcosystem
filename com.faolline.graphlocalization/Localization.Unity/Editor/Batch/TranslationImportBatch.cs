using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization.Plugins.CSV;

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
    /// imported into "{Sanitize(fileNameWithoutExtension)}_Text" -- the caller names each file
    /// after the generated graph asset's own name, exactly the convention
    /// <see cref="UnityLocalizationSyncer"/> used to create the collection in the first place, so
    /// no separate manifest/lookup is needed) and/or -speakersCsv &lt;path&gt; (imported into the
    /// fixed "Global_Text" collection). At least one of the two must be given. Exits 0 only if
    /// every requested import succeeded.
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

            if (args.TryGetValue("-speakersCsv", out var speakersCsv))
            {
                if (TryImport(speakersCsv, "Global_Text", failures))
                    imported++;
            }

            if (args.TryGetValue("-dialogueTranslationsDir", out var dir))
            {
                if (!Directory.Exists(dir))
                {
                    failures.Add($"-dialogueTranslationsDir does not exist: {dir}");
                }
                else
                {
                    foreach (var csvPath in Directory.GetFiles(dir, "*.csv"))
                    {
                        var collectionName = $"{Sanitize(Path.GetFileNameWithoutExtension(csvPath))}_Text";
                        if (TryImport(csvPath, collectionName, failures))
                            imported++;
                    }
                }
            }

            if (imported == 0 && failures.Count == 0)
                throw new InvalidOperationException("Nothing to import: neither -speakersCsv nor -dialogueTranslationsDir given.");

            foreach (var failure in failures)
                Console.Error.WriteLine($"[TranslationImportBatch] {failure}");

            Console.WriteLine($"[TranslationImportBatch] Imported {imported} collection(s), {failures.Count} failure(s).");

            EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
        }

        static bool TryImport(string csvPath, string collectionName, List<string> failures)
        {
            if (!File.Exists(csvPath))
            {
                failures.Add($"CSV not found for collection '{collectionName}': {csvPath}");
                return false;
            }

            var collection = UnityEditor.Localization.LocalizationEditorSettings.GetStringTableCollections()
                .FirstOrDefault(c => c.TableCollectionName == collectionName);
            if (collection == null)
            {
                failures.Add($"No String Table Collection named '{collectionName}' -- run the graph generation/sync first.");
                return false;
            }

            try
            {
                using var reader = new StreamReader(csvPath);
                Csv.ImportInto(reader, collection);
                return true;
            }
            catch (Exception ex)
            {
                failures.Add($"Import failed for collection '{collectionName}' ({csvPath}): {ex.Message}");
                return false;
            }
        }

        // Même logique que UnityLocalizationSyncer.Sanitize -- doit rester identique pour que le
        // nom de collection calculé ici matche exactement celui que le syncer a déjà créé.
        static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unnamed";
            var invalid = Path.GetInvalidFileNameChars();
            return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
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
