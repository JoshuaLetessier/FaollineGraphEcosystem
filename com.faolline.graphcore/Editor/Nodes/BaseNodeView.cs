using System;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Abstract base for the visual representation of a <see cref="BaseNodeData"/>.
    /// Implement <see cref="OnBuildView"/> to populate the node content area.
    /// Override <see cref="HasColorOverride"/> and <see cref="ColorOverride"/> to apply a
    /// custom node background color.
    /// The title bar is editable inline (double-click) and persisted to
    /// <see cref="BaseNodeData.Title"/>; an empty title falls back to the subclass's type label.
    /// </summary>
    public abstract class BaseNodeView : Node
    {
        private static readonly string UssName = "BaseNodeView";

        /// <summary>The data object this view represents. Null until Initialize is called.</summary>
        public BaseNodeData NodeData { get; protected set; }

        /// <summary>
        /// Raised after the title is edited inline. Hosts (the graph view) subscribe to mark the
        /// graph dirty so the change persists.
        /// </summary>
        public event Action TitleChanged;

        // The type label set by the subclass constructor (e.g. "Line", "Statement"); used when
        // BaseNodeData.Title is empty. Captured at Initialize time.
        private string _defaultTitle;
        private Label _titleLabel;
        private TextField _titleEditor;

        /// <summary>
        /// When <c>true</c>, <see cref="ColorOverride"/> is used as the node background color.
        /// Default: <c>false</c>.
        /// </summary>
        protected virtual bool HasColorOverride => false;

        /// <summary>
        /// The background color applied when <see cref="HasColorOverride"/> is <c>true</c>.
        /// Default: <c>Color.gray</c>.
        /// </summary>
        protected virtual Color ColorOverride => Color.gray;

        /// <summary>
        /// Resolves the node background color using the four-step chain:
        /// inspector instance override → code override → lib type color → graphcore default grey.
        /// </summary>
        public Color ResolveColor()
        {
            if (NodeData != null && NodeData.HasColorOverride)
                return NodeData.NodeColor;

            if (HasColorOverride)
                return ColorOverride;

            if (NodeData != null && NodeData.NodeType != null && NodeTypeColorRegistry.TryGet(NodeData.NodeType, out var registeredColor))
                return registeredColor;

            return GraphCoreDefaults.NodeGrey;
        }

        /// <summary>
        /// Called during construction after the base node chrome is built.
        /// Add custom UI elements (labels, fields, ports) here.
        /// </summary>
        protected abstract void OnBuildView();

        /// <summary>
        /// Initializes the view with the given node data. Call from subclass constructors.
        /// </summary>
        protected void Initialize(BaseNodeData nodeData)
        {
            NodeData = nodeData;
            _defaultTitle = title; // type label set by the subclass ctor before Initialize
            LoadStyleSheet();
            OnBuildView();
            ApplyNodeColor();
            SetupEditableTitle();
            ApplyTitleFromData();
        }

        // ── Inline title editing ────────────────────────────────────────────────

        /// <summary>Shows <see cref="BaseNodeData.Title"/> on the title bar, or the type label when empty.</summary>
        private void ApplyTitleFromData()
        {
            title = (NodeData != null && !string.IsNullOrEmpty(NodeData.Title)) ? NodeData.Title : _defaultTitle;
        }

        private void SetupEditableTitle()
        {
            _titleLabel = this.Q<Label>("title-label");
            if (_titleLabel == null) return;

            _titleEditor = new TextField { isDelayed = true };
            _titleEditor.style.display = DisplayStyle.None;
            _titleEditor.style.flexGrow = 1;
            _titleLabel.parent.Insert(_titleLabel.parent.IndexOf(_titleLabel), _titleEditor);

            _titleLabel.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0 && evt.clickCount == 2)
                {
                    BeginTitleEdit();
                    evt.StopImmediatePropagation();
                }
            });

            _titleEditor.RegisterCallback<FocusOutEvent>(_ => EndTitleEdit());
            _titleEditor.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    EndTitleEdit();
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    CloseTitleEditor();
                    evt.StopPropagation();
                }
            });
        }

        private void BeginTitleEdit()
        {
            if (_titleEditor == null) return;
            _titleEditor.SetValueWithoutNotify(NodeData?.Title ?? string.Empty);
            _titleLabel.style.display = DisplayStyle.None;
            _titleEditor.style.display = DisplayStyle.Flex;
            _titleEditor.Focus();
            _titleEditor.SelectAll();
        }

        private void EndTitleEdit()
        {
            if (_titleEditor == null || _titleEditor.style.display == DisplayStyle.None) return;

            var newTitle = (_titleEditor.value ?? string.Empty).Trim();
            CloseTitleEditor();

            var current = NodeData?.Title ?? string.Empty;
            if (NodeData != null && newTitle != current)
            {
                NodeData.Title = newTitle;
                ApplyTitleFromData();
                TitleChanged?.Invoke();
            }
        }

        private void CloseTitleEditor()
        {
            if (_titleEditor == null) return;
            _titleEditor.style.display = DisplayStyle.None;
            if (_titleLabel != null) _titleLabel.style.display = DisplayStyle.Flex;
        }

        private void LoadStyleSheet()
        {
            var guids = AssetDatabase.FindAssets($"{UssName} t:StyleSheet");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith($"{UssName}.uss"))
                {
                    var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                    if (styleSheet != null)
                    {
                        styleSheets.Add(styleSheet);
                        break;
                    }
                }
            }
        }

        public void RefreshColor()
        {
            ApplyNodeColor();
        }

        /// <summary>
        /// Returns true and the resolved color when this node has an explicit color (instance
        /// override → code override → registered lib type color). Returns false when only the
        /// graphcore default would apply — so the canvas keeps its USS default instead of being
        /// tinted grey. This is the "is there a real color to show" half of <see cref="ResolveColor"/>.
        /// </summary>
        public bool TryResolveColorOverride(out Color color)
        {
            if (NodeData != null && NodeData.HasColorOverride) { color = NodeData.NodeColor; return true; }
            if (HasColorOverride)                              { color = ColorOverride;       return true; }
            if (NodeData != null && NodeData.NodeType != null && NodeTypeColorRegistry.TryGet(NodeData.NodeType, out color)) return true;
            color = default;
            return false;
        }

        private void ApplyNodeColor()
        {
            // Tint the node BODY (#contents), not the title bar. Only when an explicit color is set —
            // otherwise reset to the USS default so un-colored nodes keep their normal look.
            // Dynamic colors require this minimal C# bridge; all static styling stays in USS.
            var body = this.Q("contents") ?? mainContainer;
            if (body == null) return;

            if (TryResolveColorOverride(out var color))
            {
                // A fully-transparent tint is invisible — the default Color is (0,0,0,0) and the
                // color picker often leaves alpha at 0. Treat alpha 0 as "opaque" so a chosen color shows.
                if (color.a <= 0f) color.a = 1f;
                body.style.backgroundColor = new StyleColor(color);
            }
            else
            {
                body.style.backgroundColor = new StyleColor(StyleKeyword.Null);
            }
        }
    }
}
