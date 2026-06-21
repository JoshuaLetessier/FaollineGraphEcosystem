using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Static registry where downstream libs inject inspector UI sections into the graph editor
    /// without graphcore knowing their types. Follows the same pattern as
    /// <see cref="GraphEditorWindowRegistry"/>: register via <c>[InitializeOnLoad]</c>.
    /// </summary>
    public static class InspectorExtensionRegistry
    {
        public delegate void NodeSectionDelegate(
            BaseNodeData node, VisualElement parent, BaseGraph graph, Action markDirty);

        public delegate void GraphSectionDelegate(
            BaseGraph graph, VisualElement parent, Action markDirty);

        private static readonly List<NodeSectionDelegate> _nodeSections = new();
        private static readonly List<GraphSectionDelegate> _graphSections = new();

        public static IReadOnlyList<NodeSectionDelegate> NodeSections => _nodeSections;
        public static IReadOnlyList<GraphSectionDelegate> GraphSections => _graphSections;

        public static void RegisterNodeSection(NodeSectionDelegate callback)
        {
            if (callback != null && !_nodeSections.Contains(callback))
                _nodeSections.Add(callback);
        }

        public static void RegisterGraphSection(GraphSectionDelegate callback)
        {
            if (callback != null && !_graphSections.Contains(callback))
                _graphSections.Add(callback);
        }

        public static void Clear()
        {
            _nodeSections.Clear();
            _graphSections.Clear();
        }
    }
}
