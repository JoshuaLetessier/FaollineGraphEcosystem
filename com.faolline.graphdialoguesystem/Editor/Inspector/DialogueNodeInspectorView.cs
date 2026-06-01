using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphDialogue.Editor
{
    /// <summary>
    /// Inspector panel for dialogue graphs. Renders type-specific sections (line speaker/text/expression,
    /// choice add/remove/label/condition, end reason, sub-dialogue target, edge condition) plus the
    /// shared base-node section and a typed parameter panel when nothing is selected.
    /// </summary>
    public class DialogueNodeInspectorView : BaseNodeInspectorView
    {
        private SerializedObject _serializedGraph;
        private BaseGraph _graph;
        private DialogueGraphView _graphView;
        private BaseNodeData _boundNode;

        /// <summary>Provides the loaded graph asset for SerializedObject binding.</summary>
        public void SetGraph(BaseGraph graph)
        {
            _graph = graph;
            _serializedGraph = graph != null ? new SerializedObject(graph) : null;
        }

        /// <summary>Provides the canvas view so the inspector can rebuild choice ports and edges.</summary>
        public void SetGraphView(DialogueGraphView graphView) => _graphView = graphView;

        protected override void OnNodeVisualsChanged() => _graphView?.RefreshNodeColors();

        // ── Node binding ──────────────────────────────────────────────────────

        public override void BindNode(BaseNodeData node)
        {
            ClearInspector();
            if (node == null) return;

            _boundNode = node;

            if (_serializedGraph != null && _serializedGraph.targetObject == null)
                _serializedGraph = _graph != null ? new SerializedObject(_graph) : null;
            _serializedGraph?.Update();

            var nodeElement = FindNodeProperty(_serializedGraph, node.Id);

            if (node is DialogueLineNodeData)
                BuildLineSection(nodeElement, (DialogueLineNodeData)node);

            if (node is ChoiceNodeData choiceNode)
                BuildChoiceSection(choiceNode);

            if (node is EndNodeData endNode)
                BuildEndReasonSection(endNode);

            if (node is SubGraphNodeData subNode)
                BuildSubGraphSection(subNode);

            if (nodeElement != null && _serializedGraph != null)
                AddBaseNodeSection(nodeElement, _serializedGraph);
        }

        public override void ClearInspector()
        {
            Clear();
            _boundNode = null;
            if (_graph != null)
                BuildParameterPanel();
        }

        // ── Edge binding (FR-021: condition on a connection) ──────────────────

        /// <summary>
        /// Binds a single selected edge, exposing an <see cref="ObjectField"/> for its gating
        /// <see cref="BaseEdgeData.Condition"/>. Setting a condition marks the graph dirty.
        /// </summary>
        public void BindEdge(BaseEdgeData edge)
        {
            Clear();
            _boundNode = null;
            if (edge == null) { ClearInspector(); return; }

            var foldout = new Foldout { text = "Edge", value = true };

            var conditionField = new ObjectField("Condition")
            {
                objectType = typeof(BaseCondition),
                allowSceneObjects = false,
                value = edge.Condition
            };
            conditionField.RegisterValueChangedCallback(e =>
            {
                edge.Condition = e.newValue as BaseCondition;
                MarkGraphDirty();
            });
            foldout.Add(conditionField);

            Add(foldout);
        }

        // ── Line section ──────────────────────────────────────────────────────

        private void BuildLineSection(SerializedProperty nodeElement, DialogueLineNodeData node)
        {
            var foldout = new Foldout { text = "Line", value = true };

            foldout.Add(BuildBoundOrPlainField(nodeElement, "_speakerKey", "Speaker Key",
                () => node.SpeakerKey, v => node.SpeakerKey = v));
            foldout.Add(BuildBoundOrPlainField(nodeElement, "_expressionKey", "Expression Key",
                () => node.ExpressionKey, v => node.ExpressionKey = v));

            Add(foldout);
        }

        private VisualElement BuildBoundOrPlainField(
            SerializedProperty nodeElement, string relativeProp, string label,
            System.Func<string> getter, System.Action<string> setter)
        {
            if (nodeElement != null && _serializedGraph != null)
            {
                var prop = nodeElement.FindPropertyRelative(relativeProp);
                if (prop != null)
                {
                    var field = new PropertyField(prop, label);
                    field.Bind(_serializedGraph);
                    return field;
                }
            }

            // Fallback (unit tests without a SerializedObject): plain text field on the data.
            var tf = new TextField(label) { value = getter() };
            tf.RegisterValueChangedCallback(e => { setter(e.newValue); MarkGraphDirty(); });
            return tf;
        }

        // ── End / SubGraph sections ───────────────────────────────────────────

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
            field.RegisterValueChangedCallback(e =>
            {
                node.EndReason = (EndReason)e.newValue;
                MarkGraphDirty();
            });
            foldout.Add(field);
            Add(foldout);
        }

        public bool SetSubGraphTarget(SubGraphNodeData node, BaseGraph target)
        {
            if (node == null) return false;
            if (target != null && _graph != null)
            {
                var result = CycleDetector.Check(_graph, target);
                if (result.HasCycle)
                {
                    var path = result.CyclePath != null ? string.Join(" → ", result.CyclePath) : "?";
                    Debug.LogWarning($"[GraphDialogue] Cycle refused: {path}");
                    return false;
                }
            }
            node.TargetGraph = target;
            MarkGraphDirty();
            RefreshIfBound(node);
            return true;
        }

        public void SetInheritParentContext(SubGraphNodeData node, bool inherit)
        {
            if (node == null) return;
            node.InheritParentContext = inherit;
            MarkGraphDirty();
            RefreshIfBound(node);
        }

        private void BuildSubGraphSection(SubGraphNodeData node)
        {
            var foldout = new Foldout { text = "SubDialogue", value = true };

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
                    Debug.LogWarning($"[GraphDialogue] Cycle refused: {path}");
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

        // ── Choice section ────────────────────────────────────────────────────

        /// <summary>Appends a new <see cref="DialogueChoice"/> and rebuilds the canvas ports.</summary>
        public void AddChoice(ChoiceNodeData node)
        {
            if (node == null) return;
            node.Choices.Add(new DialogueChoice
            {
                Id = System.Guid.NewGuid().ToString("D")
            });
            MarkGraphDirty();
            _graphView?.GetChoiceView(node.Id)?.RebuildPorts();
            _graphView?.ReconnectNodeEdges(node.Id);
            RefreshIfBound(node);
        }

        /// <summary>Removes a choice, deletes its bound edge, rebuilds ports, refreshes inspector.</summary>
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

                // Friendly name → shown on the choice's output port and used as localization source text.
                // The localization key itself is derived from the choice Id (no hand-typed key field).
                var nameField = new TextField("Name") { value = choice.Title };
                nameField.style.flexGrow = 1;
                nameField.RegisterValueChangedCallback(e =>
                {
                    choice.Title = e.newValue;
                    MarkGraphDirty();
                    _graphView?.GetChoiceView(node.Id)?.UpdateChoiceLabel(choice.Id, ChoiceNodeView.ResolveLabel(choice));
                });

                var conditionField = new ObjectField
                {
                    objectType = typeof(BaseCondition),
                    allowSceneObjects = false,
                    value = choice.Condition
                };
                conditionField.style.flexGrow = 1;
                conditionField.RegisterValueChangedCallback(e =>
                {
                    choice.Condition = e.newValue as BaseCondition;
                    MarkGraphDirty();
                });

                var removeBtn = new Button(() => RemoveChoice(node, choice)) { text = "×" };

                row.Add(nameField);
                row.Add(conditionField);
                row.Add(removeBtn);
                foldout.Add(row);
            }

            foldout.Add(new Button(() => AddChoice(node)) { text = "Add Choice" });
            Add(foldout);
        }

        // ── Parameter panel ───────────────────────────────────────────────────

        public void AddParameter(string key, ParameterType type, string defaultValue)
        {
            if (_graph == null) return;
            _graph.AddParameter(new ParameterData
            {
                Key = key,
                Type = type,
                DefaultValue = defaultValue ?? string.Empty
            });
            EditorUtility.SetDirty(_graph);
            RebuildParameterPanel();
        }

        public void RemoveParameter(string key)
        {
            if (_graph == null) return;
            for (int i = _graph.Parameters.Count - 1; i >= 0; i--)
            {
                if (_graph.Parameters[i].Key == key)
                {
                    _graph.RemoveParameter(_graph.Parameters[i]);
                    EditorUtility.SetDirty(_graph);
                    break;
                }
            }
            RebuildParameterPanel();
        }

        private void BuildParameterPanel()
        {
            var foldout = new Foldout { text = "Parameters", value = true };

            foreach (var param in _graph.Parameters)
            {
                var capturedKey = param.Key;
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.Add(new Label(param.Key) { style = { flexGrow = 1 } });
                row.Add(new Label(param.Type.ToString()) { style = { width = 56 } });
                row.Add(new Label($"= {param.DefaultValue}") { style = { flexGrow = 1 } });
                row.Add(new Button(() => RemoveParameter(capturedKey)) { text = "×" });
                foldout.Add(row);
            }

            var addRow = new VisualElement();
            addRow.style.flexDirection = FlexDirection.Row;
            var keyField = new TextField("key") { style = { flexGrow = 1 } };
            var typeField = new EnumField(ParameterType.Bool) { style = { width = 72 } };
            var defaultField = new TextField("default") { style = { flexGrow = 1 } };
            addRow.Add(keyField);
            addRow.Add(typeField);
            addRow.Add(defaultField);
            addRow.Add(new Button(() =>
            {
                if (!string.IsNullOrWhiteSpace(keyField.value))
                    AddParameter(keyField.value.Trim(), (ParameterType)typeField.value, defaultField.value ?? string.Empty);
            }) { text = "Add" });
            foldout.Add(addRow);

            Add(foldout);
        }

        private void RebuildParameterPanel()
        {
            Clear();
            if (_graph != null) BuildParameterPanel();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

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
