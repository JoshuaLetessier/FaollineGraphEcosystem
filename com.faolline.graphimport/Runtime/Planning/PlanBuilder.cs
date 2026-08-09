using System.Collections.Generic;

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

        public GenerationPlan Build(IReadOnlyList<PivotQuest> quests)
        {
            var entries = new List<PlanEntry>();

            foreach (var quest in quests)
            {
                var questPath = _pathResolver.Resolve(PlanEntryKind.QuestAsset, quest);
                entries.Add(new PlanEntry($"quest:{quest.Id}", PlanEntryKind.QuestAsset, questPath, quest.Id, quest));

                // A quest with no steps has nothing to build a playable flow from — no FlowAsset entry.
                if (quest.Steps.Count > 0)
                {
                    var flowPath = _pathResolver.Resolve(PlanEntryKind.FlowAsset, quest);
                    entries.Add(new PlanEntry($"flow:{quest.Id}", PlanEntryKind.FlowAsset, flowPath, quest.Id, quest));
                }
            }

            return new GenerationPlan(entries);
        }

        /// <summary>
        /// One DialogueAsset entry per dialogue — a separate method rather than an overload of
        /// <see cref="Build(IReadOnlyList{PivotQuest})"/> because the quest and dialogue pivots don't
        /// share a base type (see research.md in specs/049-dialogue-import-unity-side on why they
        /// deliberately don't). Callers combine the two plans' <see cref="GenerationPlan.Entries"/> when
        /// they want one preview covering both (FR-008 — same downstream Plan/Apply, not a parallel one).
        /// </summary>
        public GenerationPlan BuildDialogues(IReadOnlyList<PivotDialogue> dialogues)
        {
            var entries = new List<PlanEntry>();

            foreach (var dialogue in dialogues)
            {
                var path = _pathResolver.Resolve(PlanEntryKind.DialogueAsset, dialogue);
                entries.Add(new PlanEntry($"dialogue:{dialogue.Id}", PlanEntryKind.DialogueAsset, path, dialogue.Id, dialogue));
            }

            return new GenerationPlan(entries);
        }
    }
}
