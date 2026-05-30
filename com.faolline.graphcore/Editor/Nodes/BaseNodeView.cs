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
    /// </summary>
    public abstract class BaseNodeView : Node
    {
        private static readonly string UssName = "BaseNodeView";

        /// <summary>The data object this view represents. Null until Initialize is called.</summary>
        public BaseNodeData NodeData { get; protected set; }

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
            LoadStyleSheet();
            OnBuildView();
            ApplyNodeColor();
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
                body.style.backgroundColor = new StyleColor(color);
            else
                body.style.backgroundColor = new StyleColor(StyleKeyword.Null);
        }
    }
}
