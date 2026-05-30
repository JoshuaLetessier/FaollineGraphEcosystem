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

            AddField(foldout, nodeElement, "_isCheckpoint");
            AddField(foldout, nodeElement, "_hasColorOverride");
            AddField(foldout, nodeElement, "_nodeColor");
            AddField(foldout, nodeElement, "_entryConditions");
            AddField(foldout, nodeElement, "_onEnterActions");
            AddField(foldout, nodeElement, "_onExitActions");

            // Bind all PropertyFields within the foldout in one call.
            foldout.Bind(so);
            Add(foldout);
        }

        private static void AddField(VisualElement parent, SerializedProperty nodeElement, string relativePath)
        {
            var prop = nodeElement.FindPropertyRelative(relativePath);
            if (prop != null)
                parent.Add(new PropertyField(prop));
        }
    }
}
