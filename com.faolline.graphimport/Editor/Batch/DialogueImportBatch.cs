using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace Faolline.GraphImport.Editor
{
    /// <summary>
    /// Minimal -executeMethod entry point for an unattended/CI run of dialogue generation from a
    /// dialogue-studio-style interchange JSON export: something (a cron job, a CI step) drops an
    /// exported JSON file somewhere, this method turns it into real DialogueGraph assets. Wires
    /// together already-tested pieces (InterchangeDialogueSet/DialoguePivotBuilder/PlanBuilder/
    /// PlanApplier/ProjectAssetResolver/SpeakerGroupApplier) for a batchmode invocation; introduces no
    /// new business logic of its own.
    ///
    /// Command line: -dialoguesJson &lt;path&gt; -dialoguePathTemplate &lt;template&gt;
    /// [-speakerFolder &lt;path&gt;] [-speakerTablesCsv &lt;path&gt;]. The path template supports {id}/{name}
    /// tokens (<see cref="TemplatePathResolver"/>); speakerFolder defaults to "Assets/Generated/Speakers".
    /// speakerTablesCsv is an optional <see cref="SpeakerGroupMapping"/> (SpeakerKey,Table): it is read and
    /// validated before anything is written; created speakers take their mapped group, and every existing
    /// speaker the export references is realigned with it afterwards (each change printed).
    /// Exits 0 only if the run is fully clean (no conflicts, no generator failures).
    /// </summary>
    public static class DialogueImportBatch
    {
        public static void Run()
        {
            try
            {
                var args = BatchArgs.Parse(Environment.GetCommandLineArgs());
                EditorApplication.Exit(Execute(args, Console.Out, Console.Error));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[DialogueImportBatch] Fatal: {ex}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>The whole run, minus the process exit: returns the exit code.</summary>
        internal static int Execute(IReadOnlyDictionary<string, string> args, TextWriter output, TextWriter error)
        {
            if (!args.TryGetValue("-dialoguesJson", out var jsonPath) || !File.Exists(jsonPath))
                throw new InvalidOperationException("Missing or unreadable -dialoguesJson <path>.");
            if (!args.TryGetValue("-dialoguePathTemplate", out var pathTemplate))
                throw new InvalidOperationException("Missing -dialoguePathTemplate <template> (e.g. \"Assets/Generated/Dialogues/{name}.asset\").");
            var speakerFolder = args.TryGetValue("-speakerFolder", out var sf) ? sf : "Assets/Generated/Speakers";

            // Read and validate the speaker tables mapping before anything is written.
            SpeakerGroupMapping speakerGroups = null;
            if (args.TryGetValue("-speakerTablesCsv", out var mappingPath))
            {
                if (!File.Exists(mappingPath))
                {
                    error.WriteLine($"[DialogueImportBatch] -speakerTablesCsv not found: {mappingPath}");
                    return 1;
                }
                try { speakerGroups = SpeakerGroupMapping.Parse(File.ReadAllText(mappingPath)); }
                catch (SpeakerGroupMappingException ex)
                {
                    error.WriteLine($"[DialogueImportBatch] {mappingPath}: {ex.Message}");
                    return 1;
                }
            }

            var interchange = InterchangeDialogueSet.LoadFromJson(File.ReadAllText(jsonPath));
            var dialogues = new DialoguePivotBuilder().Build(interchange);

            var pathResolver = new TemplatePathResolver(new Dictionary<PlanEntryKind, string>
            {
                [PlanEntryKind.DialogueAsset] = pathTemplate
            });
            var plan = new PlanBuilder(pathResolver).BuildDialogues(dialogues);

            var report = PlanConflictDetector.Detect(plan);
            var resolver = new ProjectAssetResolver(plan, speakerFolder, speakerGroups);
            var generators = new Dictionary<PlanEntryKind, IAssetGenerator>
            {
                [PlanEntryKind.DialogueAsset] = new DialogueAssetGenerator(resolver)
            };
            var applyResult = PlanApplier.Apply(plan, report, generators);

            // Every speaker the export references — including in dialogues that collided and were skipped.
            if (speakerGroups != null)
            {
                var referenced = dialogues.SelectMany(d => d.Nodes.Values).OfType<PivotLine>().Select(l => l.SpeakerKey);
                foreach (var change in SpeakerGroupApplier.Apply(referenced, speakerGroups))
                    output.WriteLine($"[DialogueImportBatch] Speaker '{change.SpeakerKey}' group '{change.OldGroup}' → '{change.NewGroup}' ({change.AssetPath})");
            }

            foreach (var conflict in report.Conflicts)
                error.WriteLine($"[DialogueImportBatch] Conflict ({conflict.Reason}): {conflict.PlanEntry.ProposedPath}");
            foreach (var failure in applyResult.Failures)
                error.WriteLine($"[DialogueImportBatch] Failed to generate '{failure.Entry.ProposedPath}': {failure.Exception.Message}");

            var isClean = report.IsClean && applyResult.IsClean;
            output.WriteLine($"[DialogueImportBatch] Created {applyResult.Created.Count} asset(s), {report.Conflicts.Count} conflict(s), {applyResult.Failures.Count} failure(s).");

            return isClean ? 0 : 1;
        }
    }
}
