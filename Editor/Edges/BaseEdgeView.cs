using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Abstract base for the visual representation of a <see cref="BaseEdgeData"/> connection.
    /// Override <see cref="HasColorOverride"/> and <see cref="ColorOverride"/> to apply a
    /// custom edge color. Color resolution follows the same three-step chain as
    /// <see cref="BaseNodeView"/>.
    /// </summary>
    public abstract class BaseEdgeView : Edge
    {
        private static readonly string UssName = "BaseEdgeView";

        /// <summary>The data object this view represents. Null until Initialize is called.</summary>
        public BaseEdgeData EdgeData { get; internal set; }

        /// <summary>
        /// When <c>true</c>, <see cref="ColorOverride"/> is used as the edge color.
        /// Default: <c>false</c>.
        /// </summary>
        protected virtual bool HasColorOverride => false;

        /// <summary>
        /// The color applied when <see cref="HasColorOverride"/> is <c>true</c>.
        /// Default: <c>Color.gray</c>.
        /// </summary>
        protected virtual Color ColorOverride => Color.gray;

        /// <summary>
        /// Resolves the edge color using the three-step chain:
        /// override → lib type color → graphcore default grey.
        /// </summary>
        public Color ResolveColor()
        {
            if (HasColorOverride)
                return ColorOverride;

            if (EdgeData != null && NodeTypeColorRegistry.TryGet(EdgeData.Id, out var registeredColor))
                return registeredColor;

            return GraphCoreDefaults.NodeGrey;
        }

        /// <summary>
        /// Initializes the view with the given edge data. Call from subclass constructors.
        /// </summary>
        protected void Initialize(BaseEdgeData edgeData)
        {
            EdgeData = edgeData;
            LoadStyleSheet();
            ApplyEdgeColor();
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

        private void ApplyEdgeColor()
        {
            // Apply resolved edge color. Dynamic registry-based colors require this minimal
            // C# bridge; all layout/typography/spacing styling is in USS.
            edgeControl.inputColor = ResolveColor();
            edgeControl.outputColor = ResolveColor();
        }
    }
}
