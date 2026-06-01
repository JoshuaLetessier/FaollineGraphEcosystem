using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Visual representation of a <see cref="GraphGroupData"/> on the canvas.
    /// Extends Unity's <see cref="Group"/> with: persistent data binding, color tinting,
    /// and collapse/expand (content area toggles visibility; header always visible).
    /// Right-click the group header for rename/color/collapse options.
    /// </summary>
    public sealed class BaseGroupView : Group
    {
        public GraphGroupData GroupData { get; }

        /// <summary>Raised when the group is renamed, collapsed, or its color changes — host marks graph dirty.</summary>
        public System.Action DataChanged;

        private Button _collapseButton;
        private VisualElement _contentArea;

        public BaseGroupView(GraphGroupData data)
        {
            GroupData = data;
            title = data.Title;

            SetPosition(new Rect(data.Position, data.Size));
            ApplyColor(data.Color);

            AddCollapseButton();
            ApplyCollapsedState(data.IsCollapsed, animate: false);

            // Sync title changes back to data
            var titleLabel = this.Q<Label>("titleLabel")
                ?? this.Q<Label>(className: "group-title-label")
                ?? this.Q<Label>();
            if (titleLabel != null)
            {
                titleLabel.RegisterCallback<FocusOutEvent>(_ =>
                {
                    if (data.Title != title)
                    {
                        data.Title = title;
                        DataChanged?.Invoke();
                    }
                });
            }
        }

        // ── Collapse ──────────────────────────────────────────────────────────────

        private void AddCollapseButton()
        {
            _collapseButton = new Button(ToggleCollapse) { text = GroupData.IsCollapsed ? "▶" : "▼" };
            _collapseButton.AddToClassList("group-collapse-btn");
            _collapseButton.style.position = Position.Absolute;
            _collapseButton.style.right = 4;
            _collapseButton.style.top = 4;
            _collapseButton.style.width = 20;
            _collapseButton.style.height = 20;
            _collapseButton.style.fontSize = 10;
            _collapseButton.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0.3f));
            _collapseButton.style.borderBottomLeftRadius = 3;
            _collapseButton.style.borderBottomRightRadius = 3;
            _collapseButton.style.borderTopLeftRadius = 3;
            _collapseButton.style.borderTopRightRadius = 3;
            Add(_collapseButton);
        }

        private void ToggleCollapse()
        {
            GroupData.IsCollapsed = !GroupData.IsCollapsed;
            ApplyCollapsedState(GroupData.IsCollapsed, animate: true);
            DataChanged?.Invoke();
        }

        private void ApplyCollapsedState(bool collapsed, bool animate)
        {
            _contentArea ??= this.Q("contentContainer") ?? this.Q("contents");

            if (_collapseButton != null)
                _collapseButton.text = collapsed ? "▶" : "▼";

            if (_contentArea != null)
                _contentArea.style.display = collapsed ? DisplayStyle.None : DisplayStyle.Flex;
        }

        // ── Color ─────────────────────────────────────────────────────────────────

        private void ApplyColor(Color color)
        {
            if (color.a <= 0f) color.a = 0.4f;
            style.backgroundColor = new StyleColor(color);
        }

        /// <summary>Changes the group background color and persists it.</summary>
        public void SetColor(Color color)
        {
            GroupData.Color = color;
            ApplyColor(color);
            DataChanged?.Invoke();
        }

        // ── Contextual menu ───────────────────────────────────────────────────────

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction(GroupData.IsCollapsed ? "Expand" : "Collapse",
                _ => ToggleCollapse());

            evt.menu.AppendSeparator();
            base.BuildContextualMenu(evt);
        }
    }
}
