using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphGameFlow.Editor
{
    /// <summary>
    /// Inspector for a selected gameflow node. Reuses graphcore's base node section (title, checkpoint,
    /// conditions, and the on-enter / on-exit ACTION lists — where a <see cref="LoadSceneAction"/> is dropped
    /// in) and adds a gameflow "Flow" foldout for the await-signal name and wait duration, plus End / SubGraph
    /// / Choice sections per node type.
    /// </summary>
    public class GameFlowNodeInspectorView : BaseNodeInspectorView
    {
        private SerializedObject _serializedGraph;
        private BaseGraph _graph;
        private GameFlowGraphView _graphView;
        private BaseNodeData _boundNode;

        /// <summary>Provides the loaded graph asset for SerializedObject binding.</summary>
        public void SetGraph(BaseGraph graph)
        {
            _graph = graph;
            _serializedGraph = graph != null ? new SerializedObject(graph) : null;
        }

        /// <summary>Provides the canvas view so choice-port rebuilds reach the canvas. May be null in tests.</summary>
        public void SetGraphView(GameFlowGraphView graphView) => _graphView = graphView;

        protected override void OnNodeVisualsChanged() => _graphView?.RefreshNodeColors();

        public override void BindNode(BaseNodeData node)
        {
            ClearInspector();
            if (node == null) return;

            _boundNode = node;

            // A GraphLink is a non-executing documentary reference → dedicated minimal panel (target + note),
            // NOT the flow/execution fields. Render it and stop.
            if (node is GraphLinkNodeData link) { AddGraphLinkSection(link, MarkGraphDirty); return; }

            if (_serializedGraph != null && _serializedGraph.targetObject == null)
                _serializedGraph = _graph != null ? new SerializedObject(_graph) : null;
            _serializedGraph?.Update();

            var nodeElement = FindNodeProperty(_serializedGraph, node.Id);

            if (nodeElement != null && _serializedGraph != null)
                BuildFlowSection(nodeElement);

            if (node is EndNodeData endNode)
                BuildEndReasonSection(endNode);

            if (node is SubGraphNodeData subNode)
                BuildSubGraphSection(subNode);

            if (node is ChoiceNodeData choiceNode)
                BuildChoiceSection(choiceNode);

            if (nodeElement != null && _serializedGraph != null)
                AddBaseNodeSection(nodeElement, _serializedGraph);
        }

        public override void ClearInspector()
        {
            Clear();
            _boundNode = null;
            if (_graph != null) BuildNoSelectionContent();
        }

        // ── Flow section (gameflow-specific: the await-signal + wait fields) ──────

        private void BuildFlowSection(SerializedProperty nodeElement)
        {
            var foldout = new Foldout { text = "Flow", value = true };

            var assetProp = nodeElement.FindPropertyRelative("_awaitSignalAsset");
            if (assetProp != null) foldout.Add(new PropertyField(assetProp, "Await Signal (asset)"));

            var awaitProp = nodeElement.FindPropertyRelative("_awaitSignal");
            if (awaitProp != null) foldout.Add(new PropertyField(awaitProp, "Await Signal (raw)"));

            var awaitAnyProp = nodeElement.FindPropertyRelative("_awaitSignals");
            if (awaitAnyProp != null) foldout.Add(new PropertyField(awaitAnyProp, "Await Any Of (OR)"));

            var waitProp = nodeElement.FindPropertyRelative("_waitDuration");
            if (waitProp != null) foldout.Add(new PropertyField(waitProp, "Wait Duration"));

            foldout.Bind(_serializedGraph);
            Add(foldout);
        }

        // ── End node ──────────────────────────────────────────────────────────────

        private void BuildEndReasonSection(EndNodeData node)
        {
            var foldout = new Foldout { text = "End", value = true };
            var field = new EnumField("End Reason", node.EndReason);
            field.RegisterValueChangedCallback(e =>
            {
                node.EndReason = (EndReason)e.newValue;
                MarkGraphDirty();
            });
            foldout.Add(field);

            var outcomeField = new TextField("Outcome Label") { value = node.OutcomeLabel ?? string.Empty, tooltip = "Semantic label to distinguish End nodes sharing the same End Reason (e.g. \"persuaded\", \"rejected\"). Surfaced in EndStep so the consumer can branch on the outcome." };
            outcomeField.RegisterValueChangedCallback(e =>
            {
                node.OutcomeLabel = e.newValue;
                MarkGraphDirty();
            });
            foldout.Add(outcomeField);

            Add(foldout);
        }

        // ── SubGraph node ──────────────────────────────────────────────────────────

        private void BuildSubGraphSection(SubGraphNodeData node)
        {
            var foldout = new Foldout { text = "SubGraph", value = true };

            var targetField = new ObjectField("Target Graph")
            {
                objectType = typeof(BaseGraph),
                allowSceneObjects = false,
                value = node.TargetGraph
            };
            targetField.RegisterValueChangedCallback(e =>
            {
                var proposed = e.newValue as BaseGraph;
                if (proposed != null && _graph != null && CycleDetector.Check(_graph, proposed).HasCycle)
                {
                    var result = CycleDetector.Check(_graph, proposed);
                    var path = result.CyclePath != null ? string.Join(" → ", result.CyclePath) : "?";
                    Debug.LogWarning($"[GraphGameFlow] Cycle refused: {path}");
                    targetField.SetValueWithoutNotify(node.TargetGraph);
                    return;
                }
                node.TargetGraph = proposed;
                MarkGraphDirty();
            });
            foldout.Add(targetField);

            var inheritToggle = new Toggle("Inherit Parent Context") { value = node.InheritParentContext };
            inheritToggle.RegisterValueChangedCallback(e =>
            {
                node.InheritParentContext = e.newValue;
                MarkGraphDirty();
            });
            foldout.Add(inheritToggle);

            Add(foldout);
        }

        // ── Choice node ────────────────────────────────────────────────────────────

        /// <summary>Appends a new choice (fresh GUID, default title) and rebuilds the canvas ports.</summary>
        public void AddChoice(ChoiceNodeData node)
        {
            if (node == null) return;
            node.Choices.Add(new BaseChoice { Id = System.Guid.NewGuid().ToString("D"), Title = "New Choice" });
            MarkGraphDirty();
            _graphView?.GetChoiceView(node.Id)?.RebuildPorts();
            _graphView?.ReconnectNodeEdges(node.Id);
            RefreshIfBound(node);
        }

        /// <summary>Removes a choice, deletes its bound edge, and rebuilds the canvas ports.</summary>
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

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;

                var titleField = new TextField { value = choice.Title };
                titleField.style.flexGrow = 1;
                titleField.RegisterValueChangedCallback(e =>
                {
                    choice.Title = e.newValue;
                    MarkGraphDirty();
                    _graphView?.GetChoiceView(node.Id)?.UpdateChoiceLabel(choice.Id, e.newValue);
                });

                var conditionField = new ObjectField { objectType = typeof(BaseCondition), allowSceneObjects = false, value = choice.Condition };
                conditionField.style.flexGrow = 1;
                conditionField.RegisterValueChangedCallback(e =>
                {
                    choice.Condition = e.newValue as BaseCondition;
                    MarkGraphDirty();
                });

                var removeBtn = new Button(() => RemoveChoice(node, choice)) { text = "×" };

                row.Add(titleField);
                row.Add(conditionField);
                row.Add(removeBtn);
                foldout.Add(row);
            }

            foldout.Add(new Button(() => AddChoice(node)) { text = "Add Choice" });
            Add(foldout);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private void MarkGraphDirty()
        {
            if (_graph != null) EditorUtility.SetDirty(_graph);
        }

        private void RefreshIfBound(BaseNodeData node)
        {
            if (_boundNode == node) BindNode(node);
        }
    }
}
