using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Abstract base for embedded node inspector panels hosted inside a <see cref="BaseGraphEditorWindow"/>.
    /// Provides shared helpers to locate a node's <see cref="SerializedProperty"/> within the graph asset
    /// and to render the universal base-node fields (checkpoint, color, conditions, actions).
    /// Subclasses implement <see cref="BindNode"/> for domain-specific fields and must call
    /// <see cref="AddBaseNodeSection"/> to include the shared fields.
    /// Subclasses own all Undo.RecordObject / EditorUtility.SetDirty calls for their custom mutations.
    /// </summary>
    public abstract class BaseNodeInspectorView : VisualElement
    {
        protected BaseNodeInspectorView()
        {
            AddToClassList("graph-inspector-panel");
        }

        // ── Shared graph state ────────────────────────────────────────────────

        /// <summary>The loaded graph asset whose nodes/parameters this inspector edits.</summary>
        protected BaseGraph Graph { get; private set; }

        /// <summary>A <see cref="SerializedObject"/> over <see cref="Graph"/> for bound property fields.</summary>
        protected SerializedObject SerializedGraph { get; private set; }

        /// <summary>Provides the loaded graph asset for SerializedObject binding and the parameter panel.</summary>
        public virtual void SetGraph(BaseGraph graph)
        {
            Graph = graph;
            SerializedGraph = graph != null ? new SerializedObject(graph) : null;
        }

        /// <summary>Marks the loaded graph dirty (no-op when none is loaded).</summary>
        protected void MarkGraphDirty()
        {
            if (Graph != null) EditorUtility.SetDirty(Graph);
        }

        /// <summary>
        /// Rebuilds <see cref="SerializedGraph"/> when its target asset was destroyed (reimport / domain reload),
        /// then calls <c>Update()</c>. Call at the top of <see cref="BindNode"/> before reading node properties.
        /// </summary>
        protected void RefreshSerializedGraph()
        {
            if (SerializedGraph != null && SerializedGraph.targetObject == null)
                SerializedGraph = Graph != null ? new SerializedObject(Graph) : null;
            SerializedGraph?.Update();
        }

        // ── Shared parameter panel ────────────────────────────────────────────
        // Every graph has parameters, so the panel lives here once instead of being copied per lib. The default
        // value is edited with a field whose type matches the chosen ParameterType (no free-text parsing).

        /// <summary>Appends the graph-parameters foldout (list + a typed add row). No-op without a graph.</summary>
        protected void BuildParameterPanel()
        {
            if (Graph == null) return;

            var foldout = new Foldout { text = "Parameters", value = true };

            foreach (var param in Graph.Parameters)
            {
                if (param == null) continue;
                var capturedKey = param.Key;
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                row.Add(new Label(param.Key) { style = { flexGrow = 1 } });
                row.Add(new Label(param.Type.ToString()) { style = { width = 56 } });
                row.Add(new Label($"= {param.DefaultValue}") { style = { flexGrow = 1 } });
                row.Add(new Button(() => RemoveParameter(capturedKey)) { text = "×" });
                foldout.Add(row);
            }

            var addRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var keyField  = new TextField("key") { style = { flexGrow = 1 } };
            var typeField = new EnumField(ParameterType.Bool) { style = { width = 72 } };
            var defaultHost = new VisualElement { style = { flexGrow = 1 } };
            VisualElement defaultField = MakeDefaultField(ParameterType.Bool);
            defaultHost.Add(defaultField);
            typeField.RegisterValueChangedCallback(e =>
            {
                defaultHost.Clear();
                defaultField = MakeDefaultField((ParameterType)e.newValue);
                defaultHost.Add(defaultField);
            });
            var addBtn = new Button(() =>
            {
                var key = keyField.value?.Trim();
                if (string.IsNullOrWhiteSpace(key)) return;
                AddParameter(MakeParameter(key, (ParameterType)typeField.value, defaultField));
            }) { text = "Add" };
            addRow.Add(keyField);
            addRow.Add(typeField);
            addRow.Add(defaultHost);
            addRow.Add(addBtn);
            foldout.Add(addRow);

            Add(foldout);
        }

        /// <summary>Clears the panel and rebuilds the no-selection content (used after parameter add/remove).</summary>
        protected void RebuildParameterPanel()
        {
            Clear();
            BuildNoSelectionContent();
        }

        /// <summary>
        /// Builds the panel content shown when no node is selected. Default = the parameter panel. Override to add
        /// lib-specific no-selection sections around it (e.g. a dialogue speakers list), then call
        /// <see cref="BuildParameterPanel"/>. Subclasses call this from their <c>ClearInspector</c>.
        /// </summary>
        protected virtual void BuildNoSelectionContent()
        {
            BuildParameterPanel();
        }

        /// <summary>Adds a fully-formed parameter to the graph and rebuilds the panel.</summary>
        public void AddParameter(ParameterData param)
        {
            if (Graph == null || param == null) return;
            Graph.AddParameter(param);
            MarkGraphDirty();
            RebuildParameterPanel();
        }

        /// <summary>Adds a parameter from a (legacy) string default — kept for existing callers/tests.</summary>
        public void AddParameter(string key, ParameterType type, string defaultValue)
            => AddParameter(new ParameterData { Key = key, Type = type, DefaultValue = defaultValue ?? string.Empty });

        /// <summary>Adds a bool parameter (convenience).</summary>
        public void AddBoolParameter(string key, bool defaultValue) => AddParameter(ParameterData.Bool(key, defaultValue));

        /// <summary>Removes the first parameter with <paramref name="key"/>, regardless of type, and rebuilds.</summary>
        public void RemoveParameter(string key)
        {
            if (Graph == null) return;
            for (int i = Graph.Parameters.Count - 1; i >= 0; i--)
                if (Graph.Parameters[i] != null && Graph.Parameters[i].Key == key)
                {
                    Graph.RemoveParameter(Graph.Parameters[i]);
                    MarkGraphDirty();
                    break;
                }
            RebuildParameterPanel();
        }

        /// <summary>Removes the first bool parameter with <paramref name="key"/> (convenience).</summary>
        public void RemoveBoolParameter(string key)
        {
            if (Graph == null) return;
            for (int i = Graph.Parameters.Count - 1; i >= 0; i--)
                if (Graph.Parameters[i] != null && Graph.Parameters[i].Key == key && Graph.Parameters[i].Type == ParameterType.Bool)
                {
                    Graph.RemoveParameter(Graph.Parameters[i]);
                    MarkGraphDirty();
                    break;
                }
            RebuildParameterPanel();
        }

        // The default-value editor field, typed to the parameter type (Toggle / Integer / Float / Text).
        private static VisualElement MakeDefaultField(ParameterType type)
        {
            switch (type)
            {
                case ParameterType.Int:    return new IntegerField { value = 0 };
                case ParameterType.Float:  return new FloatField   { value = 0f };
                case ParameterType.String: return new TextField    { value = string.Empty };
                default:                   return new Toggle        { value = false };
            }
        }

        // Builds a typed ParameterData from the add-row's key + typed default field.
        private static ParameterData MakeParameter(string key, ParameterType type, VisualElement defaultField)
        {
            switch (type)
            {
                case ParameterType.Int:    return ParameterData.Int(key,    (defaultField as IntegerField)?.value ?? 0);
                case ParameterType.Float:  return ParameterData.Float(key,  (defaultField as FloatField)?.value   ?? 0f);
                case ParameterType.String: return ParameterData.String(key, (defaultField as TextField)?.value    ?? string.Empty);
                default:                   return ParameterData.Bool(key,   (defaultField as Toggle)?.value        ?? false);
            }
        }

        /// <summary>
        /// Populates the inspector panel with editable fields for <paramref name="node"/>.
        /// Always called with a non-null argument. Call <see cref="ClearInspector"/> internally
        /// before rebuilding to avoid field accumulation.
        /// </summary>
        public abstract void BindNode(BaseNodeData node);

        /// <summary>
        /// Clears all child elements from the inspector panel.
        /// Called when the canvas selection is empty or contains more than one node.
        /// Must leave the panel ready for a subsequent <see cref="BindNode"/> call.
        /// </summary>
        public abstract void ClearInspector();

        // ── Shared helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Walks the serialized <c>_nodes</c> array of <paramref name="so"/> and returns
        /// the <see cref="SerializedProperty"/> element whose <c>_id</c> matches <paramref name="nodeId"/>.
        /// Returns null when the graph is null, the property is not found, or no element matches.
        /// </summary>
        protected static SerializedProperty FindNodeProperty(SerializedObject so, string nodeId)
        {
            if (so == null) return null;
            var nodes = so.FindProperty("_nodes");
            if (nodes == null || !nodes.isArray) return null;

            for (int i = 0; i < nodes.arraySize; i++)
            {
                var element = nodes.GetArrayElementAtIndex(i);
                var idProp  = element.FindPropertyRelative("_id");
                if (idProp != null && idProp.stringValue == nodeId)
                    return element;
            }
            return null;
        }

        /// <summary>
        /// Appends a foldout containing <see cref="PropertyField"/>s for the universal
        /// <see cref="BaseNodeData"/> fields: Is Checkpoint, Has Color Override, Node Color,
        /// Entry Conditions, On Enter Actions, On Exit Actions.
        /// All fields are bound to <paramref name="so"/> via <c>Foldout.Bind</c>.
        /// Position is intentionally omitted — it is managed by the canvas.
        /// </summary>
        protected void AddBaseNodeSection(SerializedProperty nodeElement, SerializedObject so)
        {
            if (nodeElement == null || so == null) return;

            var foldout = new Foldout { text = "Node Properties", value = true };

            AddField(foldout, nodeElement, "_title");
            AddField(foldout, nodeElement, "_isCheckpoint");
            AddField(foldout, nodeElement, "_hasColorOverride");
            AddField(foldout, nodeElement, "_nodeColor");
            AddField(foldout, nodeElement, "_entryConditions");
            AddField(foldout, nodeElement, "_onEnterActions");
            AddField(foldout, nodeElement, "_onExitActions");

            // Bind all PropertyFields within the foldout in one call.
            foldout.Bind(so);

            // A change to any bound field here must refresh the node's canvas visuals.
            // Additionally, changing the color implies the override should be ON — enable it
            // automatically so a developer can recolor a node without first ticking Has Color Override
            // (the checkbox stays visible to turn the override back off).
            foldout.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
            {
                var changed = evt.changedProperty;
                if (changed != null && changed.propertyPath != null && changed.propertyPath.EndsWith("_nodeColor"))
                {
                    var hasOverride = nodeElement.FindPropertyRelative("_hasColorOverride");
                    if (hasOverride != null && !hasOverride.boolValue)
                    {
                        hasOverride.boolValue = true;
                        so.ApplyModifiedProperties();
                    }
                }
                OnNodeVisualsChanged();
            });

            Add(foldout);
        }

        /// <summary>
        /// Called when a bound field that affects a node's canvas visuals (its color override) changes.
        /// Override in a subclass to refresh the canvas (e.g. call the graph view's RefreshNodeColors).
        /// Default is a no-op.
        /// </summary>
        protected virtual void OnNodeVisualsChanged() { }

        private static void AddField(VisualElement parent, SerializedProperty nodeElement, string relativePath)
        {
            var prop = nodeElement.FindPropertyRelative(relativePath);
            if (prop != null)
                parent.Add(new PropertyField(prop));
        }
    }
}
