using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace Faolline.GraphImport.Editor
{
    /// <summary>
    /// Minimal -executeMethod entry point for the combined quest+flow+dialogue run that only
    /// <see cref="GraphImportWindow"/> could do until now (interactive-only) — <see cref="GraphImportBatch"/>
    /// and <see cref="DialogueImportBatch"/> each build and apply their own isolated plan/resolver, so a
    /// quest step's content ref to a dialogue generated in the same run never resolves through either one
    /// alone (see ProjectAssetResolver: it only resolves a target that's part of the SAME GenerationPlan
    /// it was built from). This class reproduces GraphImportWindow's Commit path headlessly: build both
    /// plans, concat dialogues-before-quest/flow (PlanApplier applies in list order, and only quest/flow
    /// ever references a dialogue, never the reverse — see 64a71c5), share one ProjectAssetResolver built
    /// from the combined plan, apply once. Introduces no new business logic beyond that ordering/wiring.
    ///
    /// Command line: -mappingJson &lt;path&gt; plus a -&lt;sourceTableName&gt;Csv per mapping table
    /// (quest/flow side, same as GraphImportBatch) — always required. Dialogue side is optional: pass
    /// -dialoguesJson &lt;path&gt; (+ -dialoguePathTemplate, required together) to include it in the same
    /// combined run; omit both to behave like a quest/flow-only run but still going through the shared
    /// ProjectAssetResolver (harmless — a resolver built from a plan with no dialogue entries just never
    /// finds one, same documented-safe null as before). Optional: -questPathTemplate/-flowPathTemplate/
    /// -speakerFolder overrides, same defaults as the two standalone batch entry points. Exits 0 only if
    /// the combined run is fully clean (no conflicts, no generator failures).
    /// </summary>
    public static class CombinedImportBatch
    {
        public static void Run()
        {
            try
            {
                RunInternal();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[CombinedImportBatch] Fatal: {ex}");
                EditorApplication.Exit(1);
            }
        }

        static void RunInternal()
        {
            var args = BatchArgs.Parse(Environment.GetCommandLineArgs());

            var questFlowEntries = BuildQuestFlowEntries(args);
            var dialogueEntries = BuildDialogueEntries(args);
            if (questFlowEntries.Count == 0 && dialogueEntries.Count == 0)
                throw new InvalidOperationException("Nothing to generate: no quest/flow mapping and no -dialoguesJson given.");

            var speakerFolder = args.TryGetValue("-speakerFolder", out var sf) ? sf : "Assets/Generated/Speakers";
            var result = BuildAndApply(dialogueEntries, questFlowEntries, speakerFolder);

            foreach (var conflict in result.Conflicts.Conflicts)
                Console.Error.WriteLine($"[CombinedImportBatch] Conflict ({conflict.Reason}): {conflict.PlanEntry.ProposedPath}");
            foreach (var failure in result.Apply.Failures)
                Console.Error.WriteLine($"[CombinedImportBatch] Failed to generate '{failure.Entry.ProposedPath}': {failure.Exception.Message}");

            Console.WriteLine($"[CombinedImportBatch] Created {result.Apply.Created.Count} asset(s), {result.Conflicts.Conflicts.Count} conflict(s), {result.Apply.Failures.Count} failure(s).");

            EditorApplication.Exit(result.IsClean ? 0 : 1);
        }

        /// <summary>
        /// Pure combining/apply logic, extracted from <see cref="RunInternal"/> so it's testable
        /// without going through CLI args or EditorApplication.Exit. Dialogues are concatenated
        /// before quest/flow (never the reverse — see 64a71c5) and a single ProjectAssetResolver is
        /// built from the FULL combined plan, so a quest step's content ref and a dialogue's
        /// sub-link can both resolve to anything else generated in this same run.
        /// </summary>
        public static GraphImportRunResult BuildAndApply(IReadOnlyList<PlanEntry> dialogueEntries, IReadOnlyList<PlanEntry> questFlowEntries, string speakerFolder)
        {
            var allEntries = dialogueEntries.Concat(questFlowEntries).ToList();
            var plan = new GenerationPlan(allEntries);
            var report = PlanConflictDetector.Detect(plan);

            var resolver = new ProjectAssetResolver(plan, speakerFolder);
            var generators = new Dictionary<PlanEntryKind, IAssetGenerator>
            {
                [PlanEntryKind.QuestAsset] = new QuestAssetGenerator(),
                [PlanEntryKind.FlowAsset] = new FlowAssetGenerator(resolver),
                [PlanEntryKind.DialogueAsset] = new DialogueAssetGenerator(resolver)
            };
            var applyResult = PlanApplier.Apply(plan, report, generators);

            return new GraphImportRunResult(report, applyResult);
        }

        static List<PlanEntry> BuildQuestFlowEntries(Dictionary<string, string> args)
        {
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

            mapping.Validate(sourceTables);

            var quests = new PivotBuilder(mapping, new IdOrNameReferenceResolver()).Build(sourceTables);
            var questPathTemplate = args.TryGetValue("-questPathTemplate", out var qpt) ? qpt : "Assets/Generated/Quests/{chapter}/{name}.asset";
            var flowPathTemplate = args.TryGetValue("-flowPathTemplate", out var fpt) ? fpt : "Assets/Generated/GameFlow/{chapter}/{name}.asset";
            var pathResolver = new TemplatePathResolver(new Dictionary<PlanEntryKind, string>
            {
                [PlanEntryKind.QuestAsset] = questPathTemplate,
                [PlanEntryKind.FlowAsset] = flowPathTemplate
            });

            return new PlanBuilder(pathResolver).Build(quests).Entries.ToList();
        }

        static List<PlanEntry> BuildDialogueEntries(Dictionary<string, string> args)
        {
            if (!args.TryGetValue("-dialoguesJson", out var jsonPath))
                return new List<PlanEntry>();

            if (!File.Exists(jsonPath))
                throw new InvalidOperationException($"-dialoguesJson path does not exist: {jsonPath}");
            if (!args.TryGetValue("-dialoguePathTemplate", out var pathTemplate))
                throw new InvalidOperationException("-dialoguesJson was given but -dialoguePathTemplate is missing.");

            var interchange = InterchangeDialogueSet.LoadFromJson(File.ReadAllText(jsonPath));
            var dialogues = new DialoguePivotBuilder().Build(interchange);
            var pathResolver = new TemplatePathResolver(new Dictionary<PlanEntryKind, string>
            {
                [PlanEntryKind.DialogueAsset] = pathTemplate
            });

            return new PlanBuilder(pathResolver).BuildDialogues(dialogues).Entries.ToList();
        }
    }
}
