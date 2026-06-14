using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;
using Faolline.StarterGraph;

namespace Faolline.StarterGraph.Editor
{
    /// <summary>
    /// Inspector panel for the StarterGraph template. Adds the lib-specific bits (the statement node Label, and
    /// the Choice section wired to the canvas) on top of the shared <see cref="BaseNodeInspectorView"/>, which
    /// owns the graph state, the parameter panel, and the universal node section.
    /// </summary>
    public class StarterNodeInspectorView : BaseNodeInspectorView
    {
        private StarterGraphView _graphView;
        private BaseNodeData _boundNode;

        /// <summary>
        /// Provides the canvas view so the inspector can rebuild a choice node's output ports and remove edges
        /// when choices are added or removed. May be null in unit tests.
        /// </summary>
        public void SetGraphView(StarterGraphView graphView) => _graphView = graphView;

        /// <summary>Refreshes node colors on the canvas when a node's color override changes in the inspector.</summary>
        protected override void OnNodeVisualsChanged() => _graphView?.RefreshNodeColors();

        public override void BindNode(BaseNodeData node)
        {
            ClearInspector();
            if (node == null) return;

            _boundNode = node;
            RefreshSerializedGraph();
            var nodeElement = FindNodeProperty(SerializedGraph, node.Id);

            if (node is StarterStatementNodeData statement)
            {
                var labelField = nodeElement != null
                    ? BuildBoundLabelField(nodeElement)
                    : BuildUnboundLabelField(statement);
                Add(labelField);
            }

            if (node is ChoiceNodeData choiceNode)  BuildChoiceSection(choiceNode);
            if (node is EndNodeData endNode)         BuildEndReasonSection(endNode);
            if (node is SubGraphNodeData subNode)    BuildSubGraphSection(subNode);

            if (nodeElement != null && SerializedGraph != null)
                AddBaseNodeSection(nodeElement, SerializedGraph);
        }

        public override void ClearInspector()
        {
            Clear();
            _boundNode = null;
            if (Graph != null) BuildParameterPanel();
        }

        // ── End node ──────────────────────────────────────────────────────────
        /// <summary>Sets <paramref name="node"/>'s end reason, marks dirty, refreshes if bound.</summary>
        public void SetEndReason(EndNodeData node, EndReason reason)
        {
            if (node == null) return;
            node.EndReason = reason;
            MarkGraphDirty();
            RefreshIfBound(node);
        }

        private void BuildEndReasonSection(EndNodeData node)
        {
            var foldout = new Foldout { text = "End", value = true };
            var field = new EnumField("End Reason", node.EndReason);
            field.RegisterValueChangedCallback(e => { node.EndReason = (EndReason)e.newValue; MarkGraphDirty(); });
            foldout.Add(field);
            Add(foldout);
        }

        // ── SubGraph node ─────────────────────────────────────────────────────
        /// <summary>Assigns the sub-graph target, refusing inter-graph cycles. Returns false on refusal.</summary>
        public bool SetSubGraphTarget(SubGraphNodeData node, BaseGraph target)
        {
            if (node == null) return false;
            if (target != null && Graph != null)
            {
                var result = CycleDetector.Check(Graph, target);
                if (result.HasCycle)
                {
                    var path = result.CyclePath != null ? string.Join(" → ", result.CyclePath) : "?";
                    Debug.LogWarning($"[StarterGraph] Cycle refused: {path}");
                    return false;
                }
            }
            node.TargetGraph = target;
            MarkGraphDirty();
            RefreshIfBound(node);
            return true;
        }

        /// <summary>Sets the sub-graph's inherit-parent-context flag, marks dirty, refreshes if bound.</summary>
        public void SetInheritParentContext(SubGraphNodeData node, bool inherit)
        {
            if (node == null) return;
            node.InheritParentContext = inherit;
            MarkGraphDirty();
            RefreshIfBound(node);
        }

        private void BuildSubGraphSection(SubGraphNodeData node)
        {
            var foldout = new Foldout { text = "SubGraph", value = true };

            var targetField = new ObjectField("Target Graph")
            {
                objectType = typeof(BaseGraph), allowSceneObjects = false, value = node.TargetGraph
            };
            targetField.RegisterValueChangedCallback(e =>
            {
                var proposed = e.newValue as BaseGraph;
                if (proposed != null && Graph != null && CycleDetector.Check(Graph, proposed).HasCycle)
                {
                    var result = CycleDetector.Check(Graph, proposed);
                    var path = result.CyclePath != null ? string.Join(" → ", result.CyclePath) : "?";
                    Debug.LogWarning($"[StarterGraph] Cycle refused: {path}");
                    targetField.SetValueWithoutNotify(node.TargetGraph);
                    return;
                }
                node.TargetGraph = proposed;
                MarkGraphDirty();
            });
            foldout.Add(targetField);

            var inheritToggle = new Toggle("Inherit Parent Context") { value = node.InheritParentContext };
            inheritToggle.RegisterValueChangedCallback(e => { node.InheritParentContext = e.newValue; MarkGraphDirty(); });
            foldout.Add(inheritToggle);

            Add(foldout);
        }

        // ── Choice node (lib choice type + canvas ports) ──────────────────────
        /// <summary>Appends a new <see cref="StarterChoice"/>, rebuilds ports, marks dirty, refreshes.</summary>
        public void AddChoice(ChoiceNodeData node)
        {
            if (node == null) return;
            node.Choices.Add(new StarterChoice { Id = System.Guid.NewGuid().ToString("D"), Label = "New Choice" });
            MarkGraphDirty();
            _graphView?.GetChoiceView(node.Id)?.RebuildPorts();
            _graphView?.ReconnectNodeEdges(node.Id);
            RefreshIfBound(node);
        }

        /// <summary>Removes <paramref name="choice"/>, deletes its edge, rebuilds ports, refreshes.</summary>
        public void RemoveChoice(ChoiceNodeData node, BaseChoice choice)
        {
            if (node == null || choice == null) return;
            if (!node.Choices.Remove(choice)) return;
            _graphView?.RemoveChoiceEdges(node.Id, choice.Id);
            MarkGraphDirty();
            _graphView?.GetChoiceView(node.Id)?.RebuildPorts();
            _graphView?.ReconnectNodeEdges(node.Id);
            RefreshIfBound(node);
        }

        private void BuildChoiceSection(ChoiceNodeData node)
        {
            var foldout = new Foldout { text = "Choices", value = true };

            foreach (var baseChoice in node.Choices)
            {
                if (baseChoice == null) continue;
                var choice = baseChoice;

                var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };

                var labelField = new TextField { value = (choice as StarterChoice)?.Label ?? string.Empty };
                labelField.style.flexGrow = 1;
                labelField.RegisterValueChangedCallback(e =>
                {
                    if (choice is StarterChoice tc)
                    {
                        tc.Label = e.newValue;
                        MarkGraphDirty();
                        _graphView?.GetChoiceView(node.Id)?.UpdateChoiceLabel(choice.Id, e.newValue);
                    }
                });

                var conditionField = new ObjectField
                {
                    objectType = typeof(BaseCondition), allowSceneObjects = false, value = choice.Condition
                };
                conditionField.style.flexGrow = 1;
                conditionField.RegisterValueChangedCallback(e => { choice.Condition = e.newValue as BaseCondition; MarkGraphDirty(); });

                row.Add(labelField);
                row.Add(conditionField);
                row.Add(new Button(() => RemoveChoice(node, choice)) { text = "×" });
                foldout.Add(row);
            }

            foldout.Add(new Button(() => AddChoice(node)) { text = "Add Choice" });
            Add(foldout);
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private void RefreshIfBound(BaseNodeData node)
        {
            if (_boundNode == node) BindNode(node);
        }

        private VisualElement BuildBoundLabelField(SerializedProperty nodeElement)
        {
            var labelProp = nodeElement.FindPropertyRelative("_label");
            if (labelProp == null) return new Label("(no _label property found)");
            var field = new PropertyField(labelProp, "Label");
            field.Bind(SerializedGraph);
            return field;
        }

        private static VisualElement BuildUnboundLabelField(StarterStatementNodeData node)
            => new Label($"Label: {node.Label}");
    }
}
