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

            // Use the effective id (QuestId, else GraphId) — the SAME resolution QuestEvaluator uses to look up
            // quest_<id> / quest_<id>_desc. Keying off the raw (possibly empty) QuestId here made an
            // editor-authored quest with no QuestId emit nothing while the evaluator queried quest_<GraphId>,
            // yielding a permanent localization-audit miss. Dogfood finding.
            var questId = graph.ResolveQuestId();
            var locData = graph.LocalizationFlags;   // inline flags (ILocalizedGraph); never null
            int graphFlags = (int)locData.DefaultFlags;
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
                var flags = locData.GetFlags(obj.Id);
                bool wantsText = (flags & LocalizedAssetFlags.Text) != 0;
                int rawFlags = (int)flags;

                if (wantsText)
                {
                    var objNameKey = QuestLocalizationKeys.ForObjective(obj.Id);
                    entry.AddKey(objNameKey, LocalizationKeyType.ObjectiveName,
                        nodeId: obj.Id,
                        defaultHint: string.IsNullOrEmpty(obj.Title) ? obj.Id : obj.Title,
                        assetFlags: rawFlags);
                    count++;

                    if (!string.IsNullOrEmpty(obj.Description))
                    {
                        var objDescKey = QuestLocalizationKeys.ForObjectiveDescription(obj.Id);
                        entry.AddKey(objDescKey, LocalizationKeyType.ObjectiveDescription,
                            nodeId: obj.Id,
                            defaultHint: obj.Description, assetFlags: rawFlags);
                        count++;
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// Graph-text write-back (054): the quest name/description keys → <see cref="QuestGraph.DisplayName"/> /
        /// <see cref="QuestGraph.Description"/>, an objective's name/description keys → its <c>Title</c> /
        /// <see cref="ObjectiveNodeData.Description"/> — the texts <see cref="ExtractGraphKeys"/> seeds the table with,
        /// under the same effective quest id.
        /// </summary>
        protected override bool ApplyGraphText(QuestGraph graph, GraphTextDrift drift)
        {
            var questId = graph.ResolveQuestId();
            if (!string.IsNullOrEmpty(questId))
            {
                if (drift.Key == QuestLocalizationKeys.ForQuest(questId)) { graph.DisplayName = drift.TableText; return true; }
                if (drift.Key == QuestLocalizationKeys.ForQuestDescription(questId)) { graph.Description = drift.TableText; return true; }
            }

            if (graph.Nodes == null) return false;
            foreach (var node in graph.Nodes)
            {
                if (!(node is ObjectiveNodeData obj) || string.IsNullOrEmpty(obj.Id)) continue;
                if (drift.Key == QuestLocalizationKeys.ForObjective(obj.Id)) { obj.Title = drift.TableText; return true; }
                if (drift.Key == QuestLocalizationKeys.ForObjectiveDescription(obj.Id)) { obj.Description = drift.TableText; return true; }
            }
            return false;
        }
    }
}
