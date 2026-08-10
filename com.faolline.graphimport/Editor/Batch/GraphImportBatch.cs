using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace Faolline.GraphImport.Editor
{
    /// <summary>
    /// Minimal -executeMethod entry point for an unattended/CI run of <see cref="GraphImportPipeline"/>
    /// (neither 048 nor 049 shipped one — <see cref="GraphImportWindow"/> is interactive-only). Wires
    /// together already-tested pieces (CsvRowSource/MappingConfig/GraphImportPipeline) for a batchmode
    /// invocation; introduces no new business logic of its own.
    ///
    /// Row-level filtering (e.g. only "Validé" quests) is deliberately NOT this class's job — the
    /// caller filters the CSV before it ever reaches this method, keeping this wiring generic rather
    /// than baking in one project's status-column convention.
    ///
    /// Command line: -mappingJson &lt;path&gt; plus, for every table declared in that mapping's
    /// "tables" array, -&lt;sourceTableName&gt;Csv &lt;path&gt; (e.g. a table named "Quêtes" needs
    /// -QuêtesCsv). Optional -questPathTemplate/-flowPathTemplate override the {chapter}/{name}
    /// default templates (kept as defaults for backward compatibility, not baked in as the only
    /// option — a project-specific template belongs on the command line, not hardcoded here, per
    /// the same reasoning already applied to DialogueImportBatch's -dialoguePathTemplate). Exits 0
    /// only if the run is fully clean (no conflicts, no generator failures) — this is the one signal
    /// a CI script needs to fail the job on (FR-013).
    /// </summary>
    public static class GraphImportBatch
    {
        public static void Run()
        {
            try {
                RunInternal();
            } catch (Exception ex) {
                Console.Error.WriteLine($"[GraphImportBatch] Fatal: {ex}");
                EditorApplication.Exit(1);
            }
        }

        static void RunInternal()
        {
            var args = BatchArgs.Parse(Environment.GetCommandLineArgs());

            if (!args.TryGetValue("-mappingJson", out var mappingPath) || !File.Exists(mappingPath))
                throw new InvalidOperationException("Missing or unreadable -mappingJson <path>.");

            var mapping = MappingConfig.LoadFromJson(File.ReadAllText(mappingPath));

            var sourceTables = new Dictionary<string, SourceTable>();
            IRowSource csv = new CsvRowSource();
            foreach (var table in mapping.Tables)
            {
                var argKey = "-" + table.SourceTableName + "Csv";
                if (!args.TryGetValue(argKey, out var csvPath) || !File.Exists(csvPath))
                    throw new InvalidOperationException($"Missing or unreadable {argKey} <path> for table '{table.SourceTableName}'.");

                sourceTables[table.SourceTableName] = csv.Read(csvPath, table.SourceTableName);
            }

            var questPathTemplate = args.TryGetValue("-questPathTemplate", out var qpt) ? qpt : "Assets/Generated/Quests/{chapter}/{name}.asset";
            var flowPathTemplate = args.TryGetValue("-flowPathTemplate", out var fpt) ? fpt : "Assets/Generated/GameFlow/{chapter}/{name}.asset";
            var pathResolver = new TemplatePathResolver(new Dictionary<PlanEntryKind, string>
            {
                [PlanEntryKind.QuestAsset] = questPathTemplate,
                [PlanEntryKind.FlowAsset] = flowPathTemplate
            });

            var generators = new Dictionary<PlanEntryKind, IAssetGenerator>
            {
                [PlanEntryKind.QuestAsset] = new QuestAssetGenerator(),
                [PlanEntryKind.FlowAsset] = new FlowAssetGenerator()
            };

            var result = GraphImportPipeline.Run(mapping, sourceTables, pathResolver, generators);

            foreach (var conflict in result.Conflicts.Conflicts)
                Console.Error.WriteLine($"[GraphImportBatch] Conflict ({conflict.Reason}): {conflict.PlanEntry.ProposedPath}");
            foreach (var failure in result.Apply.Failures)
                Console.Error.WriteLine($"[GraphImportBatch] Failed to generate '{failure.Entry.ProposedPath}': {failure.Exception.Message}");

            Console.WriteLine($"[GraphImportBatch] Created {result.Apply.Created.Count} asset(s), {result.Conflicts.Conflicts.Count} conflict(s), {result.Apply.Failures.Count} failure(s).");

            EditorApplication.Exit(result.IsClean ? 0 : 1);
        }
    }
}
