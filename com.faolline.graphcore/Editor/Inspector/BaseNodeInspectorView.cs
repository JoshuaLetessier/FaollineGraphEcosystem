using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
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
    public abstract class BaseNodeInspectorView : ScrollView
    {
        protected BaseNodeInspectorView()
        {
            AddToClassList("graph-inspector-panel");
            // Scroll vertically rather than letting flex-shrink compress the sections into each other when the
            // node has more fields than the panel is tall. Add/Clear target the scroll content container.
            mode = ScrollViewMode.Vertical;
            horizontalScrollerVisibility = ScrollerVisibility.Hidden;
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
                case ParameterType.Int:     return new IntegerField { value = 0 };
                case ParameterType.Float:   return new FloatField   { value = 0f };
                case ParameterType.String:  return new TextField    { value = string.Empty };
                case ParameterType.Vector2: return new Vector2Field { value = Vector2.zero };
                case ParameterType.Vector3: return new Vector3Field { value = Vector3.zero };
                case ParameterType.Color:   return new ColorField   { value = Color.white };
                default:                    return new Toggle       { value = false };
            }
        }

        // Builds a typed ParameterData from the add-row's key + typed default field.
        private static ParameterData MakeParameter(string key, ParameterType type, VisualElement defaultField)
        {
            switch (type)
            {
                case ParameterType.Int:     return ParameterData.Int(key,     (defaultField as IntegerField)?.value ?? 0);
                case ParameterType.Float:   return ParameterData.Float(key,   (defaultField as FloatField)?.value   ?? 0f);
                case ParameterType.String:  return ParameterData.String(key,  (defaultField as TextField)?.value    ?? string.Empty);
                case ParameterType.Vector2: return ParameterData.Vector2(key, (defaultField as Vector2Field)?.value ?? Vector2.zero);
                case ParameterType.Vector3: return ParameterData.Vector3(key, (defaultField as Vector3Field)?.value ?? Vector3.zero);
                case ParameterType.Color:   return ParameterData.Color(key,   (defaultField as ColorField)?.value   ?? Color.white);
                default:                    return ParameterData.Bool(key,    (defaultField as Toggle)?.value        ?? false);
            }
        }

        // ── Bound node + universal node sections ──────────────────────────────

        /// <summary>The node currently shown, or null. Subclasses set this in <see cref="BindNode"/>.</summary>
        protected BaseNodeData BoundNode { get; set; }

        /// <summary>Re-binds the inspector if <paramref name="node"/> is the one currently shown (after an edit).</summary>
        protected void RefreshIfBound(BaseNodeData node)
        {
            if (BoundNode == node) BindNode(node);
        }

        /// <summary>Log prefix for inspector warnings (e.g. cycle refusals). Override per lib for its own tag.</summary>
        protected virtual string LogContext => "GraphCore";

        /// <summary>The foldout title for a sub-graph node (override to e.g. "SubDialogue").</summary>
        protected virtual string SubGraphSectionTitle => "SubGraph";

        /// <summary>
        /// Appends the sections for the universal graphcore node types (End reason, SubGraph target + inherit).
        /// Call from a subclass's <see cref="BindNode"/>; no-op for other node types.
        /// </summary>
        protected void BuildUniversalNodeSections(BaseNodeData node)
        {
            if (node is EndNodeData endNode)        BuildEndReasonSection(endNode);
            if (node is SubGraphNodeData subNode)   BuildSubGraphSection(subNode);
            if (node is GraphLinkNodeData linkNode) BuildGraphLinkSection(linkNode);
        }

        /// <summary>Sets the node's end reason, marks the graph dirty, refreshes if bound.</summary>
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

        // The GraphLink section: pick the referenced graph + an optional note. No cycle check — a GraphLink is a
        // documentary reference, never executed, so referencing any graph (even the host) is harmless.
        private void BuildGraphLinkSection(GraphLinkNodeData node)
        {
            var foldout = new Foldout { text = "GraphLink (reference)", value = true };

            var targetField = new ObjectField("Target Graph")
            {
                objectType = typeof(BaseGraph), allowSceneObjects = false, value = node.TargetGraph
            };
            targetField.RegisterValueChangedCallback(e =>
            {
                node.TargetGraph = e.newValue as BaseGraph;
                MarkGraphDirty();
                RefreshIfBound(node);   // the canvas label re-derives from the target on the next reload (Save / ↻)
            });
            foldout.Add(targetField);

            var noteField = new TextField("Note") { value = node.Note ?? string.Empty };
            noteField.RegisterValueChangedCallback(e => { node.Note = e.newValue; MarkGraphDirty(); });
            foldout.Add(noteField);

            Add(foldout);
        }

        /// <summary>Assigns the sub-graph target, refusing inter-graph cycles (returns false on refusal).</summary>
        public bool SetSubGraphTarget(SubGraphNodeData node, BaseGraph target)
        {
            if (node == null) return false;
            if (target != null && Graph != null)
            {
                var result = CycleDetector.Check(Graph, target);
                if (result.HasCycle)
                {
                    var path = result.CyclePath != null ? string.Join(" → ", result.CyclePath) : "?";
                    Debug.LogWarning($"[{LogContext}] Cycle refused: {path}");
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
            var foldout = new Foldout { text = SubGraphSectionTitle, value = true };

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
                    Debug.LogWarning($"[{LogContext}] Cycle refused: {path}");
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
