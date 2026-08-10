using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace Faolline.GraphImport.Editor
{
    /// <summary>
    /// Minimal -executeMethod entry point for an unattended/CI run of dialogue generation from a
    /// dialogue-studio-style interchange JSON export — the "other direction" of
    /// <see cref="GraphImportBatch"/>: something (a cron job, a CI step) drops an exported JSON file
    /// somewhere, this method turns it into real DialogueGraph assets. Wires together already-tested
    /// pieces (InterchangeDialogueSet/DialoguePivotBuilder/PlanBuilder/PlanApplier/ProjectAssetResolver)
    /// for a batchmode invocation; introduces no new business logic of its own.
    ///
    /// Command line: -dialoguesJson &lt;path&gt; -dialoguePathTemplate &lt;template&gt;
    /// [-speakerFolder &lt;path&gt;]. The path template supports {id}/{name} tokens
    /// (<see cref="TemplatePathResolver"/>); speakerFolder defaults to "Assets/Generated/Speakers".
    /// Exits 0 only if the run is fully clean (no conflicts, no generator failures).
    /// </summary>
    public static class DialogueImportBatch
    {
        public static void Run()
        {
            try
            {
                RunInternal();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[DialogueImportBatch] Fatal: {ex}");
                EditorApplication.Exit(1);
            }
        }

        static void RunInternal()
        {
            var args = BatchArgs.Parse(Environment.GetCommandLineArgs());

            if (!args.TryGetValue("-dialoguesJson", out var jsonPath) || !File.Exists(jsonPath))
                throw new InvalidOperationException("Missing or unreadable -dialoguesJson <path>.");
            if (!args.TryGetValue("-dialoguePathTemplate", out var pathTemplate))
                throw new InvalidOperationException("Missing -dialoguePathTemplate <template> (e.g. \"Assets/Generated/Dialogues/{name}.asset\").");
            var speakerFolder = args.TryGetValue("-speakerFolder", out var sf) ? sf : "Assets/Generated/Speakers";

            var interchange = InterchangeDialogueSet.LoadFromJson(File.ReadAllText(jsonPath));
            var dialogues = new DialoguePivotBuilder().Build(interchange);

            var pathResolver = new TemplatePathResolver(new Dictionary<PlanEntryKind, string>
            {
                [PlanEntryKind.DialogueAsset] = pathTemplate
            });
            var plan = new PlanBuilder(pathResolver).BuildDialogues(dialogues);

            var report = PlanConflictDetector.Detect(plan);
            var resolver = new ProjectAssetResolver(plan, speakerFolder);
            var generators = new Dictionary<PlanEntryKind, IAssetGenerator>
            {
                [PlanEntryKind.DialogueAsset] = new DialogueAssetGenerator(resolver)
            };
            var applyResult = PlanApplier.Apply(plan, report, generators);

            foreach (var conflict in report.Conflicts)
                Console.Error.WriteLine($"[DialogueImportBatch] Conflict ({conflict.Reason}): {conflict.PlanEntry.ProposedPath}");
            foreach (var failure in applyResult.Failures)
                Console.Error.WriteLine($"[DialogueImportBatch] Failed to generate '{failure.Entry.ProposedPath}': {failure.Exception.Message}");

            var isClean = report.IsClean && applyResult.IsClean;
            Console.WriteLine($"[DialogueImportBatch] Created {applyResult.Created.Count} asset(s), {report.Conflicts.Count} conflict(s), {applyResult.Failures.Count} failure(s).");

            EditorApplication.Exit(isClean ? 0 : 1);
        }
    }
}
