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
    /// Inspector panel for the StarterGraph verification package.
    /// When a node is selected: renders Label field (for StarterStatementNodeData) and base-node section.
    /// When no node is selected: renders the bool parameter panel for the loaded graph.
    /// </summary>
    public class StarterNodeInspectorView : BaseNodeInspectorView
    {
        private SerializedObject _serializedGraph;
        private BaseGraph _graph;
        private StarterGraphView _graphView;
        private BaseNodeData _boundNode;

        /// <summary>Provides the loaded graph asset for SerializedObject binding.</summary>
        public void SetGraph(BaseGraph graph)
        {
            _graph = graph;
            _serializedGraph = graph != null ? new SerializedObject(graph) : null;
        }

        /// <summary>
        /// Provides the canvas view so the inspector can rebuild a choice node's output ports
        /// and remove edges when choices are added or removed. May be null in unit tests.
        /// </summary>
        public void SetGraphView(StarterGraphView graphView)
        {
            _graphView = graphView;
        }

        public override void BindNode(BaseNodeData node)
        {
            ClearInspector();

            if (node == null) return;

            _boundNode = node;
            _serializedGraph?.Update();

            var nodeElement = FindNodeProperty(_serializedGraph, node.Id);

            if (node is StarterStatementNodeData)
            {
                var labelField = nodeElement != null
                    ? BuildBoundLabelField(nodeElement)
                    : BuildUnboundLabelField((StarterStatementNodeData)node);
                Add(labelField);
            }

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

        // ── Public helpers for tests ──────────────────────────────────────────

        /// <summary>Adds a typed parameter (any <see cref="ParameterType"/>) with a string default to the loaded graph.</summary>
        public void AddParameter(string key, ParameterType type, string defaultValue)
        {
            if (_graph == null) return;
            _graph.AddParameter(new ParameterData
            {
                Key          = key,
                Type         = type,
                DefaultValue = defaultValue ?? string.Empty
            });
            EditorUtility.SetDirty(_graph);
            RebuildParameterPanel();
        }

        /// <summary>Removes the first parameter with the given key, regardless of type.</summary>
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

        /// <summary>Adds a bool parameter (backward-compatible wrapper over <see cref="AddParameter"/>).</summary>
        public void AddBoolParameter(string key, bool defaultValue)
            => AddParameter(key, ParameterType.Bool, defaultValue.ToString());

        /// <summary>Removes the first bool parameter with the given key from the loaded graph.</summary>
        public void RemoveBoolParameter(string key)
        {
            if (_graph == null) return;
            for (int i = _graph.Parameters.Count - 1; i >= 0; i--)
            {
                if (_graph.Parameters[i].Key == key && _graph.Parameters[i].Type == ParameterType.Bool)
                {
                    _graph.RemoveParameter(_graph.Parameters[i]);
                    EditorUtility.SetDirty(_graph);
                    break;
                }
            }
            RebuildParameterPanel();
        }

        // ── End node helpers ──────────────────────────────────────────────────

        /// <summary>
        /// Sets <paramref name="node"/>'s <see cref="EndNodeData.EndReason"/>, marks the graph dirty,
        /// and refreshes the inspector if the node is currently bound.
        /// </summary>
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

        // ── SubGraph node helpers ─────────────────────────────────────────────

        /// <summary>
        /// Assigns <paramref name="target"/> as the sub-graph target, refusing it (returning false,
        /// logging a warning) when it would create an inter-graph cycle. Marks dirty and refreshes on success.
        /// </summary>
        public bool SetSubGraphTarget(SubGraphNodeData node, BaseGraph target)
        {
            if (node == null) return false;

            if (target != null && _graph != null)
            {
                var result = CycleDetector.Check(_graph, target);
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

        /// <summary>Sets the sub-graph's inherit-parent-context flag, marks dirty, and refreshes if bound.</summary>
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
                objectType        = typeof(BaseGraph),
                allowSceneObjects = false,
                value             = node.TargetGraph
            };
            targetField.RegisterValueChangedCallback(e =>
            {
                var proposed = e.newValue as BaseGraph;
                if (proposed != null && _graph != null && CycleDetector.Check(_graph, proposed).HasCycle)
                {
                    var result = CycleDetector.Check(_graph, proposed);
                    var path = result.CyclePath != null ? string.Join(" → ", result.CyclePath) : "?";
                    Debug.LogWarning($"[StarterGraph] Cycle refused: {path}");
                    targetField.SetValueWithoutNotify(node.TargetGraph); // revert
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

        // ── Choice node helpers ───────────────────────────────────────────────

        /// <summary>
        /// Appends a new <see cref="StarterChoice"/> (fresh GUID, default label) to <paramref name="node"/>,
        /// rebuilds the canvas output ports, marks the graph dirty, and refreshes the inspector.
        /// </summary>
        public void AddChoice(ChoiceNodeData node)
        {
            if (node == null) return;

            var choice = new StarterChoice
            {
                Id    = System.Guid.NewGuid().ToString("D"),
                Label = "New Choice"
            };
            node.Choices.Add(choice);

            MarkGraphDirty();
            _graphView?.GetChoiceView(node.Id)?.RebuildPorts();
            _graphView?.ReconnectNodeEdges(node.Id);   // rebuilt ports orphan surviving edges — reconnect
            RefreshIfBound(node);
        }

        /// <summary>
        /// Removes <paramref name="choice"/> from <paramref name="node"/>, deletes any edge bound to
        /// that choice's output port, rebuilds the canvas output ports, and refreshes the inspector.
        /// </summary>
        public void RemoveChoice(ChoiceNodeData node, BaseChoice choice)
        {
            if (node == null || choice == null) return;
            if (!node.Choices.Remove(choice)) return;

            _graphView?.RemoveChoiceEdges(node.Id, choice.Id);
            MarkGraphDirty();
            _graphView?.GetChoiceView(node.Id)?.RebuildPorts();
            _graphView?.ReconnectNodeEdges(node.Id);   // rebuilt ports orphan surviving edges — reconnect
            RefreshIfBound(node);
        }

        private void BuildChoiceSection(ChoiceNodeData node)
        {
            var foldout = new Foldout { text = "Choices", value = true };

            foreach (var baseChoice in node.Choices)
            {
                if (baseChoice == null) continue;
                var choice = baseChoice; // capture for closures

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;

                var labelField = new TextField { value = (choice as StarterChoice)?.Label ?? string.Empty };
                labelField.style.flexGrow = 1;
                labelField.RegisterValueChangedCallback(e =>
                {
                    if (choice is StarterChoice tc)
                    {
                        tc.Label = e.newValue;
                        MarkGraphDirty();
                        // Update only this port's displayed label — no rebuild, so edges stay connected.
                        _graphView?.GetChoiceView(node.Id)?.UpdateChoiceLabel(choice.Id, e.newValue);
                    }
                });

                var conditionField = new ObjectField
                {
                    objectType   = typeof(BaseCondition),
                    allowSceneObjects = false,
                    value        = choice.Condition
                };
                conditionField.style.flexGrow = 1;
                conditionField.RegisterValueChangedCallback(e =>
                {
                    choice.Condition = e.newValue as BaseCondition;
                    MarkGraphDirty();
                });

                var removeBtn = new Button(() => RemoveChoice(node, choice)) { text = "×" };

                row.Add(labelField);
                row.Add(conditionField);
                row.Add(removeBtn);
                foldout.Add(row);
            }

            var addBtn = new Button(() => AddChoice(node)) { text = "Add Choice" };
            foldout.Add(addBtn);

            Add(foldout);
        }

        private void MarkGraphDirty()
        {
            if (_graph != null)
                EditorUtility.SetDirty(_graph);
        }

        private void RefreshIfBound(BaseNodeData node)
        {
            if (_boundNode == node)
                BindNode(node);
        }

        // ── Private panel builder ─────────────────────────────────────────────

        private void BuildParameterPanel()
        {
            var foldout = new Foldout { text = "Parameters", value = true };

            foreach (var param in _graph.Parameters)
            {
                var capturedKey = param.Key; // capture for the closure

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;

                var keyLabel     = new Label(param.Key) { style = { flexGrow = 1 } };
                var typeLabel    = new Label(param.Type.ToString()) { style = { width = 56 } };
                var defaultLabel = new Label($"= {param.DefaultValue}") { style = { flexGrow = 1 } };
                var removeBtn    = new Button(() => RemoveParameter(capturedKey)) { text = "×" };

                row.Add(keyLabel);
                row.Add(typeLabel);
                row.Add(defaultLabel);
                row.Add(removeBtn);
                foldout.Add(row);
            }

            var addRow = new VisualElement();
            addRow.style.flexDirection = FlexDirection.Row;
            var keyField     = new TextField("key") { style = { flexGrow = 1 } };
            var typeField    = new EnumField(ParameterType.Bool) { style = { width = 72 } };
            var defaultField = new TextField("default") { style = { flexGrow = 1 } };
            var addBtn = new Button(() =>
            {
                if (!string.IsNullOrWhiteSpace(keyField.value))
                    AddParameter(keyField.value.Trim(), (ParameterType)typeField.value, defaultField.value ?? string.Empty);
            }) { text = "Add" };
            addRow.Add(keyField);
            addRow.Add(typeField);
            addRow.Add(defaultField);
            addRow.Add(addBtn);
            foldout.Add(addRow);

            Add(foldout);
        }

        private void RebuildParameterPanel()
        {
            Clear();
            if (_graph != null)
                BuildParameterPanel();
        }

        // ── Node field helpers ────────────────────────────────────────────────

        private VisualElement BuildBoundLabelField(SerializedProperty nodeElement)
        {
            var labelProp = nodeElement.FindPropertyRelative("_label");
            if (labelProp == null) return new Label("(no _label property found)");
            var field = new PropertyField(labelProp, "Label");
            field.Bind(_serializedGraph);
            return field;
        }

        private static VisualElement BuildUnboundLabelField(StarterStatementNodeData node)
        {
            return new Label($"Label: {node.Label}");
        }
    }
}
