using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;
using Faolline.GraphTest;

namespace Faolline.GraphTest.Editor
{
    /// <summary>
    /// Inspector panel for the GraphTest verification package.
    /// When a node is selected: renders Label field (for TestStatementNodeData) and base-node section.
    /// When no node is selected: renders the bool parameter panel for the loaded graph.
    /// </summary>
    public class TestNodeInspectorView : BaseNodeInspectorView
    {
        private SerializedObject _serializedGraph;
        private BaseGraph _graph;
        private TestGraphView _graphView;
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
        public void SetGraphView(TestGraphView graphView)
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

            if (node is TestStatementNodeData)
            {
                var labelField = nodeElement != null
                    ? BuildBoundLabelField(nodeElement)
                    : BuildUnboundLabelField((TestStatementNodeData)node);
                Add(labelField);
            }

            if (node is ChoiceNodeData choiceNode)
                BuildChoiceSection(choiceNode);

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

        /// <summary>Adds a bool parameter with the given key and default value to the loaded graph.</summary>
        public void AddBoolParameter(string key, bool defaultValue)
        {
            if (_graph == null) return;
            _graph.AddParameter(new ParameterData
            {
                Key          = key,
                Type         = ParameterType.Bool,
                DefaultValue = defaultValue.ToString()
            });
            EditorUtility.SetDirty(_graph);
            RebuildParameterPanel();
        }

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

        // ── Choice node helpers ───────────────────────────────────────────────

        /// <summary>
        /// Appends a new <see cref="TestChoice"/> (fresh GUID, default label) to <paramref name="node"/>,
        /// rebuilds the canvas output ports, marks the graph dirty, and refreshes the inspector.
        /// </summary>
        public void AddChoice(ChoiceNodeData node)
        {
            if (node == null) return;

            var choice = new TestChoice
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

                var labelField = new TextField { value = (choice as TestChoice)?.Label ?? string.Empty };
                labelField.style.flexGrow = 1;
                labelField.RegisterValueChangedCallback(e =>
                {
                    if (choice is TestChoice tc)
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
            var foldout = new Foldout { text = "Bool Parameters", value = true };

            foreach (var param in _graph.Parameters)
            {
                if (param.Type != ParameterType.Bool) continue;

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;

                var keyLabel = new Label(param.Key) { style = { flexGrow = 1 } };
                var defaultLabel = new Label($"default: {param.DefaultValue}") { style = { flexGrow = 1 } };
                var removeBtn = new Button(() =>
                {
                    RemoveBoolParameter(param.Key);
                }) { text = "×" };

                row.Add(keyLabel);
                row.Add(defaultLabel);
                row.Add(removeBtn);
                foldout.Add(row);
            }

            var addRow = new VisualElement();
            addRow.style.flexDirection = FlexDirection.Row;
            var keyField = new TextField("key") { style = { flexGrow = 1 } };
            var addBtn = new Button(() =>
            {
                if (!string.IsNullOrWhiteSpace(keyField.value))
                    AddBoolParameter(keyField.value.Trim(), false);
            }) { text = "Add Bool" };
            addRow.Add(keyField);
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

        private static VisualElement BuildUnboundLabelField(TestStatementNodeData node)
        {
            return new Label($"Label: {node.Label}");
        }
    }
}
