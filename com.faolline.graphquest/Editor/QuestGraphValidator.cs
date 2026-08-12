using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;
using Faolline.GraphLogging;

namespace Faolline.GraphQuest.Editor
{
    /// <summary>
    /// Static, quest-aware validation for a <see cref="QuestGraph"/>. A quest is a reactive objective DAG with
    /// NO Start/End node, so the core <see cref="GraphValidator"/> (which requires them) does not apply — this
    /// reports the quest-specific authoring mistakes that otherwise fail silently at runtime: no objectives, an
    /// objective that can never auto-complete, an unreachable k-of-N prerequisite gate, and an unreachable /
    /// misconfigured <see cref="QuestCompletionRule.Threshold"/>. Pure logic (no scene state), fully
    /// EditMode-testable. Reuses graphcore's <see cref="GraphValidationReport"/> / <see cref="GraphIssue"/>.
    /// Menu: <c>Faolline ▸ Quest ▸ Validate Selected Quest</c>.
    /// </summary>
    public static class QuestGraphValidator
    {
        public static GraphValidationReport Validate(QuestGraph quest)
        {
            var report = new GraphValidationReport();
            if (quest == null)
            {
                report.Issues.Add(new GraphIssue(GraphIssueSeverity.Error, null, "Quest is null."));
                return report;
            }

            var objectives = quest.Nodes?
                .OfType<ObjectiveNodeData>()
                .Where(o => o != null && !string.IsNullOrEmpty(o.Id))
                .ToList() ?? new List<ObjectiveNodeData>();

            if (objectives.Count == 0)
            {
                report.Issues.Add(new GraphIssue(GraphIssueSeverity.Error, null,
                    "Quest has no objectives — it can never progress or complete."));
                return report;
            }

            var edges = quest.Edges?.Where(e => e != null).ToList() ?? new List<BaseEdgeData>();

            int requiredCount = 0;
            foreach (var obj in objectives)
            {
                if (obj.Required) requiredCount++;

                // Never-completes: no completion condition means the objective can never record Completed
                // (completion is condition-driven), so it — and anything gated behind it — stays stuck.
                if (obj.CompletionCondition == null)
                    report.Issues.Add(new GraphIssue(GraphIssueSeverity.Warning, obj.Id,
                        $"Objective '{Label(obj)}' has no Completion Condition — it can never auto-complete, so " +
                        $"the quest (and any objective gated behind it) can stall. Assign a completion condition."));

                // Unreachable k-of-N gate: requiring more prerequisites than exist leaves it Locked forever.
                if (obj.RequiredPrerequisiteCount > 0)
                {
                    int prereqCount = edges.Count(e => e.ToNodeId == obj.Id);
                    if (obj.RequiredPrerequisiteCount > prereqCount)
                        report.Issues.Add(new GraphIssue(GraphIssueSeverity.Error, obj.Id,
                            $"Objective '{Label(obj)}' requires {obj.RequiredPrerequisiteCount} of only " +
                            $"{prereqCount} prerequisite(s) — it can never unlock (stays Locked)."));
                }
            }

            // Threshold rule reachability.
            if (quest.CompletionRule == QuestCompletionRule.Threshold)
            {
                if (quest.CompletionThreshold <= 0)
                    report.Issues.Add(new GraphIssue(GraphIssueSeverity.Warning, null,
                        $"Completion rule is Threshold but the threshold is {quest.CompletionThreshold} (≤ 0) — " +
                        $"set a positive number of required objectives."));
                else if (quest.CompletionThreshold > requiredCount)
                    report.Issues.Add(new GraphIssue(GraphIssueSeverity.Error, null,
                        $"Completion threshold {quest.CompletionThreshold} exceeds the {requiredCount} Required " +
                        $"objective(s) — the quest can never reach Completed."));
            }

            return report;
        }

        private static string Label(ObjectiveNodeData o) => string.IsNullOrEmpty(o.Title) ? o.Id : o.Title;

        // ── Menu ────────────────────────────────────────────────────────────
        [MenuItem("Faolline/Quest/Validate Selected Quest")]
        private static void ValidateSelected()
        {
            if (!(Selection.activeObject is QuestGraph quest))
            {
                Logging.Warning("GraphQuest.Editor", "[QuestGraphValidator] Select a QuestGraph asset first.");
                return;
            }
            GraphValidator.LogReport(quest.name, Validate(quest));
        }
    }
}
