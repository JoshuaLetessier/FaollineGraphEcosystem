using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphQuest.Editor
{
    /// <summary>
    /// Inspector panel for quest graphs. With an objective selected it edits that objective's fields (display
    /// name, description, required flag, k-of-N prerequisite count, time limit, completion/fail conditions,
    /// reward). With nothing selected it edits the quest-level metadata (display name, description, unlock
    /// condition, completion reward, completion rule) plus the shared parameter panel.
    /// </summary>
    public sealed class QuestNodeInspectorView : BaseNodeInspectorView
    {
        protected override string LogContext => "GraphQuest";

        public override void BindNode(BaseNodeData node)
        {
            if (node == null) { ClearInspector(); return; }
            Clear();

            BoundNode = node;

            // A GraphLink is a non-executing documentary reference → dedicated minimal panel (target + note).
            if (node is GraphLinkNodeData link) { AddGraphLinkSection(link, MarkGraphDirty); return; }

            RefreshSerializedGraph();
            var element = FindNodeProperty(SerializedGraph, node.Id);
            if (node is ObjectiveNodeData && element != null && SerializedGraph != null)
                BuildObjectiveSection(element);
        }

        public override void ClearInspector()
        {
            Clear();
            BoundNode = null;

            if (Graph != null) BuildNoSelectionContent();
        }

        /// <summary>No-selection content: the quest-level metadata section above the shared parameter panel.</summary>
        protected override void BuildNoSelectionContent()
        {
            BuildQuestSection();
            base.BuildNoSelectionContent();
        }

        private void BuildObjectiveSection(SerializedProperty element)
        {
            var foldout = new Foldout { text = "Objective", value = true };
            AddRelative(foldout, element, "_title", "Display Name");
            AddRelative(foldout, element, "_description", "Description");
            AddRelative(foldout, element, "_required", "Required");
            AddRelative(foldout, element, "_requiredPrerequisiteCount", "Required Prereqs (-1 = all)");
            AddRelative(foldout, element, "_timeLimitSeconds", "Time Limit (s, 0 = none)");
            AddRelative(foldout, element, "_completionCondition", "Completion Condition");
            AddRelative(foldout, element, "_failCondition", "Fail Condition");
            AddRelative(foldout, element, "_reward", "Reward");
            foldout.Bind(SerializedGraph);
            foldout.RegisterCallback<SerializedPropertyChangeEvent>(_ => MarkGraphDirty());
            Add(foldout);
        }

        private void BuildQuestSection()
        {
            if (Graph == null) return;
            RefreshSerializedGraph();
            if (SerializedGraph == null) return;

            var foldout = new Foldout { text = "Quest", value = true };
            AddGraph(foldout, "_questId", "Quest Id (stable; referenced by UnlockAfter / QuestCompletedCondition)");
            AddGraph(foldout, "_displayName", "Display Name");
            AddGraph(foldout, "_description", "Description");
            AddGraph(foldout, "_unlockCondition", "Unlock Condition");
            AddGraph(foldout, "_completionReward", "Completion Reward");
            AddGraph(foldout, "_completionRule", "Completion Rule");

            var thresholdProp = SerializedGraph.FindProperty("_completionThreshold");
            var ruleProp = SerializedGraph.FindProperty("_completionRule");
            if (thresholdProp != null)
            {
                var thresholdField = new PropertyField(thresholdProp, "Completion Threshold");
                thresholdField.style.display = ruleProp != null && ruleProp.enumValueIndex == (int)QuestCompletionRule.Threshold
                    ? DisplayStyle.Flex : DisplayStyle.None;
                foldout.Add(thresholdField);
            }

            foldout.Bind(SerializedGraph);
            foldout.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                MarkGraphDirty();
                if (thresholdProp == null) return;
                SerializedGraph.Update();
                var rp = SerializedGraph.FindProperty("_completionRule");
                if (rp != null)
                {
                    bool show = rp.enumValueIndex == (int)QuestCompletionRule.Threshold;
                    foreach (var pf in foldout.Query<PropertyField>().ToList())
                        if (pf.bindingPath == "_completionThreshold")
                            pf.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                }
            });
            Add(foldout);
        }

        private static void AddRelative(VisualElement parent, SerializedProperty element, string relativePath, string label)
        {
            var prop = element.FindPropertyRelative(relativePath);
            if (prop != null) parent.Add(new PropertyField(prop, label));
        }

        private void AddGraph(VisualElement parent, string path, string label)
        {
            var prop = SerializedGraph.FindProperty(path);
            if (prop != null) parent.Add(new PropertyField(prop, label));
        }
    }
}
