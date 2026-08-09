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
    }
}
