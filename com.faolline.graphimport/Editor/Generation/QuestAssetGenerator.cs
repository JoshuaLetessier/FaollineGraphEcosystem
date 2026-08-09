using Faolline.GraphQuest;
using Faolline.GraphStandard.Editor;

namespace Faolline.GraphImport.Editor
{
    /// <summary>
    /// Builds a graphquest asset from a <see cref="PivotQuest"/>: one objective per step (or, for a
    /// quest with no steps yet, a single default objective standing in for the quest itself — a
    /// quest with zero objectives cannot be built). Completion/fail conditions are not authored from
    /// data in V1 — expressing arbitrary condition logic from a spreadsheet cell is out of this
    /// feature's scope; only identity, steps, and cross-quest references are generated.
    /// </summary>
    public sealed class QuestAssetGenerator : IAssetGenerator
    {
        public void Generate(PlanEntry entry)
        {
            var quest = (PivotQuest)entry.Data;
            var builder = QuestBuilder.Create(quest.Id).Named(quest.Name);

            if (quest.Steps.Count == 0)
            {
                builder.AddObjective(quest.Id).Named(quest.Name);
            }
            else
            {
                foreach (var step in quest.Steps)
                    builder.AddObjective(step.Id).Named(step.ContentRef != null ? step.ContentRef.TargetId : step.Id);
            }

            var graph = builder.Build();
            GraphAssetBuilder.Save(graph, entry.ProposedPath);
        }
    }
}
