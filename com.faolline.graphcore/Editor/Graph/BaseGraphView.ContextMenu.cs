using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Faolline.GraphCore.Editor
{
    public abstract partial class BaseGraphView
    {
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);

            if (evt.target is GraphView || evt.target is VisualElement ve && ve.ClassListContains("graphView"))
            {
                bool hasNodeSelection = false;
                foreach (var item in selection)
                    if (item is BaseNodeView) { hasNodeSelection = true; break; }

                var label = hasNodeSelection ? "Group Selection" : "Add Group";
                evt.menu.AppendAction(label, _ => GroupSelection(contentViewContainer.WorldToLocal(evt.mousePosition)));

                var graphLinkPos = contentViewContainer.WorldToLocal(evt.mousePosition);
                evt.menu.AppendAction("Add GraphLink (reference)", _ =>
                    AddNodeToCanvas(new GraphLinkNodeData { NodeType = GraphLinkNodeData.NodeTypeId }, graphLinkPos));

                if (hasNodeSelection)
                {
                    evt.menu.AppendAction("Save Selection as Template", _ => SaveSelectionAsTemplate());
                }

                var insertPos = contentViewContainer.WorldToLocal(evt.mousePosition);
                var templates = FindAllTemplates();
                if (templates.Count > 0)
                {
                    foreach (var tpl in templates)
                    {
                        var captured = tpl;
                        evt.menu.AppendAction($"Templates/{captured.name}", _ => InsertTemplate(captured, insertPos));
                    }
                }
            }
        }

        /// <summary>
        /// Auto-arranges the graph into a tidy left-to-right layered layout (longest-path layering + crossing
        /// reduction). Clears manual edge bend points (a fresh layout makes them meaningless), rebuilds the
        /// canvas, and frames the result. The graph is marked dirty; the new positions persist on the next Save.
        /// </summary>
        public void ArrangeGraph()
        {
            if (_graph == null) return;

            var sizes = new Dictionary<string, Vector2>();
            foreach (var kv in _nodeViews)
            {
                var s = kv.Value.layout.size;
                if (!float.IsNaN(s.x) && !float.IsNaN(s.y) && s.x > 1f && s.y > 1f) sizes[kv.Key] = s;
            }

            var positions = GraphAutoLayout.Arrange(_graph.Nodes, _graph.Edges, _graph.EntryNodeId, nodeSizes: sizes);
            foreach (var node in _graph.Nodes)
                if (node != null && positions.TryGetValue(node.Id, out var p)) node.Position = p;

            var routes = GraphAutoLayout.RouteLongEdges(positions, _graph.Edges, nodeSizes: sizes);
            foreach (var edge in _graph.Edges)
            {
                if (edge == null) continue;
                edge.Waypoints.Clear();
                if (routes.TryGetValue(edge.Id, out var wps)) edge.Waypoints.AddRange(wps);
            }

            _isDirty = true;
            EditorUtility.SetDirty(_graph);
            LoadGraph(_graph);
            FrameAll();
        }
    }
}
