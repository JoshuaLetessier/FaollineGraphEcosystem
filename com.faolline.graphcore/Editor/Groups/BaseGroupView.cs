using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Visual representation of a <see cref="GraphGroupData"/> on the canvas.
    /// Extends Unity's <see cref="Group"/> with persistent data binding, color tinting, and
    /// collapse/expand. Because GraphView nodes are children of the canvas (not of the Group),
    /// collapse is handled by the host graph view via <see cref="CollapseToggled"/>.
    /// </summary>
    public sealed class BaseGroupView : Group
    {
        public GraphGroupData GroupData { get; }

        /// <summary>Raised when title/color changes — host marks graph dirty.</summary>
        public System.Action DataChanged;

        /// <summary>
        /// Raised when the collapsed state changes. Parameter: isCollapsed.
        /// The host graph view subscribes to hide/show the contained node views.
        /// </summary>
        public System.Action<bool> CollapseToggled;

        private const float HeaderHeight = 26f;
        private Button _collapseButton;

        public BaseGroupView(GraphGroupData data)
        {
            GroupData = data;
            title = data.Title;

            SetPosition(new Rect(data.Position, data.Size));
            ApplyColor(data.Color);
            AddCollapseButton();

            // Contextual menu via manipulator (Group.BuildContextualMenu is not virtual here)
            this.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.InsertAction(0,
                    GroupData.IsCollapsed ? "Expand" : "Collapse",
                    _ => ToggleCollapse());
                evt.menu.InsertAction(1, "Remove Selected From Group",
                    _ => RemoveSelectedNodes(),
                    HasSelectedContainedNodes() ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                evt.menu.InsertSeparator("/", 2);
            }));

            // Sync title back to data on blur
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                var titleField = this.Q<TextField>();
                if (titleField != null)
                    titleField.RegisterCallback<FocusOutEvent>(_ =>
                    {
                        if (data.Title != title) { data.Title = title; DataChanged?.Invoke(); }
                    });
            });
        }

        // ── Collapse ──────────────────────────────────────────────────────────────

        private void AddCollapseButton()
        {
            _collapseButton = new Button(ToggleCollapse)
            {
                text = GroupData.IsCollapsed ? "▶" : "▼"
            };
            _collapseButton.style.position = Position.Absolute;
            _collapseButton.style.right = 6;
            _collapseButton.style.top = 4;
            _collapseButton.style.width = 18;
            _collapseButton.style.height = 18;
            _collapseButton.style.fontSize = 9;
            _collapseButton.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.4f));
            _collapseButton.style.borderBottomLeftRadius =
            _collapseButton.style.borderBottomRightRadius =
            _collapseButton.style.borderTopLeftRadius =
            _collapseButton.style.borderTopRightRadius = 3;
            Add(_collapseButton);
        }

        /// <summary>Flips the collapsed state, updates the visual, and notifies the host.</summary>
        public void ToggleCollapse()
        {
            GroupData.IsCollapsed = !GroupData.IsCollapsed;
            ApplyCollapsedVisual(GroupData.IsCollapsed);
            CollapseToggled?.Invoke(GroupData.IsCollapsed);
            DataChanged?.Invoke();
        }

        /// <summary>Applies the visual state of the button and the group height.</summary>
        public void ApplyCollapsedVisual(bool collapsed)
        {
            if (_collapseButton != null) _collapseButton.text = collapsed ? "▶" : "▼";

            // Resize to header only when collapsed; restore full size when expanded.
            var rect = GetPosition();
            if (collapsed)
                SetPosition(new Rect(rect.x, rect.y, rect.width, HeaderHeight));
            else
                SetPosition(new Rect(rect.x, rect.y, GroupData.Size.x, GroupData.Size.y));
        }

        // ── Membership ──────────────────────────────────────────────────────────────

        /// <summary>Removes a single node from this group (keeps the node on the canvas) and persists.</summary>
        public void RemoveContainedNode(BaseNodeView nodeView)
        {
            if (nodeView == null) return;
            if (ContainsElement(nodeView)) RemoveElement(nodeView);
            if (nodeView.NodeData != null) GroupData.NodeIds.Remove(nodeView.NodeData.Id);
            DataChanged?.Invoke();
        }

        private bool HasSelectedContainedNodes()
        {
            var gv = GetFirstAncestorOfType<GraphView>();
            if (gv == null) return false;
            foreach (var sel in gv.selection)
                if (sel is BaseNodeView nv && ContainsElement(nv)) return true;
            return false;
        }

        private void RemoveSelectedNodes()
        {
            var gv = GetFirstAncestorOfType<GraphView>();
            if (gv == null) return;
            var toRemove = new List<BaseNodeView>();
            foreach (var sel in gv.selection)
                if (sel is BaseNodeView nv && ContainsElement(nv)) toRemove.Add(nv);
            foreach (var nv in toRemove) RemoveContainedNode(nv);
        }

        // ── Color ─────────────────────────────────────────────────────────────────

        private void ApplyColor(Color color)
        {
            if (color.a <= 0f) color.a = 0.4f;
            style.backgroundColor = new StyleColor(color);
        }

        public void SetColor(Color color)
        {
            GroupData.Color = color;
            ApplyColor(color);
            DataChanged?.Invoke();
        }
    }
}
