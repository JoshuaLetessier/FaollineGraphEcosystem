using Faolline.GraphLocalization;
using Faolline.GraphLocalization.Editor;

namespace Faolline.GraphQuest.Editor
{
    /// <summary>
    /// Indexes the GraphQuest lib for the central localization builder. Auto-discovered via TypeCache.
    /// Scans all <see cref="QuestGraph"/> assets and emits deterministic keys for quest names,
    /// descriptions, objective names, and objective descriptions via <see cref="QuestLocalizationKeys"/>.
    /// </summary>
    public sealed class QuestGraphLocalizationAdapter : BaseGraphLocalizationAdapter<QuestGraph>
    {
        public override string LibName => "GraphQuest";

        protected override int ExtractGraphKeys(QuestGraph graph, LocalizationGraphEntry entry)
        {
            if (graph == null) return 0;
            int count = 0;

            var questId = graph.QuestId;
            int graphFlags = (int)graph.DefaultLocalizedAssetFlags;
            if (!string.IsNullOrEmpty(questId))
            {
                var nameKey = QuestLocalizationKeys.ForQuest(questId);
                entry.AddKey(nameKey, LocalizationKeyType.QuestName, defaultHint: graph.DisplayName, assetFlags: graphFlags);
                count++;

                if (!string.IsNullOrEmpty(graph.Description))
                {
                    var descKey = QuestLocalizationKeys.ForQuestDescription(questId);
                    entry.AddKey(descKey, LocalizationKeyType.ObjectiveDescription, defaultHint: graph.Description, assetFlags: graphFlags);
                    count++;
                }
            }

            if (graph.Nodes == null) return count;

            foreach (var node in graph.Nodes)
            {
                if (!(node is ObjectiveNodeData obj)) continue;
                if (string.IsNullOrEmpty(obj.Id)) continue;
                var flags = obj.LocalizedAssetFlags;
                bool wantsText = (flags & Faolline.GraphCore.LocalizedAssetFlags.Text) != 0;
                int rawFlags = (int)flags;

                if (wantsText)
                {
                    var objNameKey = QuestLocalizationKeys.ForObjective(obj.Id);
                    entry.AddKey(objNameKey, LocalizationKeyType.ObjectiveName,
                        defaultHint: string.IsNullOrEmpty(obj.Title) ? obj.Id : obj.Title,
                        assetFlags: rawFlags);
                    count++;

                    if (!string.IsNullOrEmpty(obj.Description))
                    {
                        var objDescKey = QuestLocalizationKeys.ForObjectiveDescription(obj.Id);
                        entry.AddKey(objDescKey, LocalizationKeyType.ObjectiveDescription,
                            defaultHint: obj.Description, assetFlags: rawFlags);
                        count++;
                    }
                }
            }

            return count;
        }
    }
}
