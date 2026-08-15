using System.Collections.Generic;
using System.Linq;

namespace Faolline.GraphImport
{
    /// <summary>
    /// Pure, deterministic: pivot data + a path template resolver in, a full preview out.
    /// No disk access, no AssetDatabase — safe to call from a test or a CI process alike.
    /// </summary>
    public sealed class PlanBuilder
    {
        readonly IPathTemplateResolver _pathResolver;

        public PlanBuilder(IPathTemplateResolver pathResolver)
        {
            _pathResolver = pathResolver;
        }

        /// <summary>One DialogueAsset entry per dialogue.</summary>
        public GenerationPlan BuildDialogues(IReadOnlyList<PivotDialogue> dialogues)
        {
            var entries = new List<PlanEntry>();

            foreach (var dialogue in OrderByDependency(dialogues))
            {
                var path = _pathResolver.Resolve(PlanEntryKind.DialogueAsset, dialogue);
                entries.Add(new PlanEntry($"dialogue:{dialogue.Id}", PlanEntryKind.DialogueAsset, path, dialogue.Id, dialogue));
            }

            return new GenerationPlan(entries);
        }

        /// <summary>
        /// Orders dialogues so that any dialogue targeted by a sub-dialogue link always comes before
        /// the dialogue that links to it. PlanApplier applies entries in list order, and
        /// ProjectAssetResolver can only resolve a sub-dialogue link to an asset that already exists
        /// on disk — a referenced dialogue generated later in the same run would otherwise resolve to
        /// null even though it's part of the very run that's about to create it. Safe to assume
        /// acyclic: DialoguePivotBuilder already rejects a sub-dialogue reference cycle before this
        /// method ever runs. Falls back to input order among dialogues with no dependency relation,
        /// keeping the result deterministic (SC-004).
        /// </summary>
        static List<PivotDialogue> OrderByDependency(IReadOnlyList<PivotDialogue> dialogues)
        {
            var byId = dialogues.ToDictionary(d => d.Id);
            var ordered = new List<PivotDialogue>();
            var visited = new HashSet<string>();

            void Visit(PivotDialogue dialogue)
            {
                if (!visited.Add(dialogue.Id))
                    return;

                foreach (var link in dialogue.Nodes.Values.OfType<PivotSubDialogueLink>())
                    if (byId.TryGetValue(link.TargetDialogueRef.TargetId, out var target))
                        Visit(target);

                ordered.Add(dialogue);
            }

            foreach (var dialogue in dialogues)
                Visit(dialogue);

            return ordered;
        }
    }
}
