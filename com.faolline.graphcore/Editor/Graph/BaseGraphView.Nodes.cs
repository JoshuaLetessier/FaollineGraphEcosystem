using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Faolline.GraphLogging;

namespace Faolline.GraphCore.Editor
{
    public abstract partial class BaseGraphView
    {
        /// <summary>
        /// Adds <paramref name="nodeData"/> to the loaded graph and the canvas.
        /// Assigns a GUID to <paramref name="nodeData"/> if <see cref="BaseNodeData.Id"/> is empty.
        /// Sets the canvas position to <paramref name="position"/>.
        /// No-op if no graph is currently loaded.
        /// </summary>
        protected void AddNodeToCanvas(BaseNodeData nodeData, Vector2 position)
        {
            if (_graph == null) return;

            if (nodeData != null && nodeData.NodeType == StartNodeData.NodeTypeId && HasStartNode())
            {
                Logging.Warning("GraphCore", "[GraphCore] This graph already has a Start node — only one is allowed (the single entry point).");
                return;
            }

            if (string.IsNullOrEmpty(nodeData.Id))
                nodeData.Id = System.Guid.NewGuid().ToString("D");

            nodeData.Position = position;

            _graph.AddNode(nodeData);

            var view = ResolveNodeView(nodeData);
            if (view == null) return;

            view.SetPosition(new Rect(position, Vector2.zero));
            view.TitleChanged += OnNodeTitleChanged;
            AddElement(view);
            _nodeViews[nodeData.Id] = view;
            RerouteEdgesWhenMoved(view);
            _isDirty = true;
            OnNodeCreated(nodeData);
        }

        /// <summary>True when the loaded graph already holds a Start node (only one is allowed — the entry point).</summary>
        protected bool HasStartNode()
        {
            if (_graph?.Nodes == null) return false;
            foreach (var n in _graph.Nodes)
                if (n != null && n.NodeType == StartNodeData.NodeTypeId) return true;
            return false;
        }

        /// <summary>
        /// Appends the shared "Add Start Node" context-menu action, disabled when the graph already has a Start
        /// node (only one is allowed — the single entry point). Libs call this from <c>BuildContextualMenu</c>
        /// instead of appending their own Start action, so the one-Start rule is enforced uniformly.
        /// </summary>
        protected void AppendAddStartAction(ContextualMenuPopulateEvent evt, Vector2 mousePos)
        {
            evt.menu.AppendAction("Add Start Node",
                _ => AddNodeToCanvas(new StartNodeData { NodeType = StartNodeData.NodeTypeId }, mousePos),
                _ => HasStartNode() ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
        }

        private void OnNodeTitleChanged()
        {
            _isDirty = true;
            if (_graph != null) EditorUtility.SetDirty(_graph);
        }
    }
}
