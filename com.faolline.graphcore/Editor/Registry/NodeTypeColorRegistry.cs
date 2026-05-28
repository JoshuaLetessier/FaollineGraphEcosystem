using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Global registry mapping node type strings to display colors.
    /// Downstream libs register colors in [InitializeOnLoad] static constructors.
    /// </summary>
    public static class NodeTypeColorRegistry
    {
        private static readonly Dictionary<string, Color> _colors = new Dictionary<string, Color>();

        /// <summary>
        /// Registers a display color for the given nodeType.
        /// If already registered, the new color replaces the existing one.
        /// </summary>
        public static void Register(string nodeType, Color color)
        {
            _colors[nodeType] = color;
        }

        /// <summary>
        /// Attempts to retrieve the registered color for nodeType.
        /// Returns false when nodeType is not registered.
        /// </summary>
        public static bool TryGet(string nodeType, out Color color)
        {
            return _colors.TryGetValue(nodeType, out color);
        }

        /// <summary>Removes all registered colors. For use in tests only.</summary>
        public static void Clear()
        {
            _colors.Clear();
        }
    }
}
