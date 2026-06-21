using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;
using Faolline.StarterGraph;

namespace Faolline.StarterGraph.Editor
{
    /// <summary>
    /// Inspector panel for the StarterGraph template. Adds the lib-specific bits (the statement node Label and
    /// the Choice section wired to the canvas) on top of the shared <see cref="BaseNodeInspectorView"/>, which
    /// owns the graph state, the parameter panel, the End/SubGraph sections, and the universal node section.
    /// </summary>
    public class StarterNodeInspectorView : BaseNodeInspectorView
    {
        private StarterGraphView _graphView;

        /// <summary>
        /// Provides the canvas view so the inspector can rebuild a choice node's output ports and remove edges
        /// when choices are added or removed. May be null in unit tests.
        /// </summary>
        public void SetGraphView(StarterGraphView graphView) => _graphView = graphView;

        protected override void OnNodeVisualsChanged() => _graphView?.RefreshNodeColors();
        protected override string LogContext => "StarterGraph";

        public override void BindNode(BaseNodeData node)
        {
            // node == null shows the no-selection content (Parameters); a real node shows ONLY its sections —
            // so clear without rebuilding the no-selection panel (else they'd stack/overlap).
            if (node == null) { ClearInspector(); return; }
            Clear();

            BoundNode = node;

            // A GraphLink is a non-executing documentary reference → dedicated minimal panel (target + note).
            if (node is GraphLinkNodeData link) { AddGraphLinkSection(link, MarkGraphDirty); return; }

            RefreshSerializedGraph();
            var nodeElement = FindNodeProperty(SerializedGraph, node.Id);

            if (node is StarterStatementNodeData statement)
            {
                var labelField = nodeElement != null
                    ? BuildBoundLabelField(nodeElement)
                    : BuildUnboundLabelField(statement);
                Add(labelField);
            }

            if (node is ChoiceNodeData choiceNode) BuildChoiceSection(choiceNode);
            BuildUniversalNodeSections(node);   // End + SubGraph (shared)

            if (nodeElement != null && SerializedGraph != null)
                AddBaseNodeSection(nodeElement, SerializedGraph);
        }

        public override void ClearInspector()
        {
            Clear();
            BoundNode = null;

            if (Graph != null) BuildNoSelectionContent();
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

        // ── Statement node field ──────────────────────────────────────────────
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
