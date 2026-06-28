using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Faolline.GraphCore.Editor
{
    public abstract partial class BaseGraphView
    {
        /// <summary>
        /// Called from the canvas context menu. Creates a group around all currently selected nodes.
        /// If no nodes are selected, creates an empty group at the mouse position.
        /// </summary>
        public void GroupSelection(Vector2 mousePosition)
        {
            if (_graph == null) return;

            var groupData = new GraphGroupData
            {
                Id       = System.Guid.NewGuid().ToString("D"),
                Title    = "Group",
                Position = mousePosition,
            };

            var selected = new List<BaseNodeView>();
            foreach (var item in selection)
                if (item is BaseNodeView nv && nv.NodeData != null) selected.Add(nv);

            if (selected.Count > 0)
            {
                var min = new Vector2(float.MaxValue, float.MaxValue);
                var max = new Vector2(float.MinValue, float.MinValue);
                foreach (var nv in selected)
                {
                    var r = nv.GetPosition();
                    min = Vector2.Min(min, r.position);
                    max = Vector2.Max(max, r.position + r.size);
                }
                const float padding = 20f;
                groupData.Position = min - Vector2.one * padding;
                groupData.Size     = (max - min) + Vector2.one * padding * 2;
                foreach (var nv in selected)
                    groupData.NodeIds.Add(nv.NodeData.Id);
            }

            _graph.AddGroup(groupData);

            var groupView = new BaseGroupView(groupData);
            groupView.DataChanged = () => { _isDirty = true; EditorUtility.SetDirty(_graph); };
            WireGroupCollapseCallback(groupView);
            AddElement(groupView);
            _groupViews[groupData.Id] = groupView;
            foreach (var nv in selected) groupView.AddElement(nv);

            _isDirty = true;
            EditorUtility.SetDirty(_graph);
        }

        /// <summary>Test/inspection hook: the live group views currently on the canvas.</summary>
        public IReadOnlyList<BaseGroupView> GroupViewsForTest => new List<BaseGroupView>(_groupViews.Values);

        /// <summary>Test/inspection hook: whether a node view is currently visible (not hidden by collapse).</summary>
        public bool IsNodeViewVisibleForTest(string nodeId)
            => _nodeViews.TryGetValue(nodeId, out var nv) && nv.style.display.value != DisplayStyle.None;

        private void WireGroupCollapseCallback(BaseGroupView groupView)
        {
            groupView.CollapseToggled = collapsed =>
            {
                SetGroupNodesVisible(groupView.GroupData, !collapsed);
                _isDirty = true;
                EditorUtility.SetDirty(_graph);
            };
        }

        private void SetGroupNodesVisible(GraphGroupData groupData, bool visible)
        {
            foreach (var nodeId in groupData.NodeIds)
                if (_nodeViews.TryGetValue(nodeId, out var nv))
                    nv.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Removes a group view and its data from the graph. Contained nodes are NOT deleted.</summary>
        private void RemoveGroup(BaseGroupView groupView)
        {
            if (_graph == null || groupView?.GroupData == null) return;
            _graph.RemoveGroup(groupView.GroupData);
            _groupViews.Remove(groupView.GroupData.Id);
            RemoveElement(groupView);
            _isDirty = true;
            EditorUtility.SetDirty(_graph);
        }
    }
}
