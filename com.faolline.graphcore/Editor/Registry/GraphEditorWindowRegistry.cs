using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Opt-in map from a concrete <see cref="BaseGraph"/> type to the editor that opens it. Populated by each
    /// downstream lib's editor (via an <c>[InitializeOnLoadMethod]</c>) so GraphCore keeps ZERO knowledge of any
    /// specific lib — the same pattern as <c>NodeTypeColorRegistry</c>. Used to navigate from a
    /// <see cref="GraphLinkNodeData"/> annotation to its target graph.
    /// </summary>
    public static class GraphEditorWindowRegistry
    {
        private static readonly Dictionary<Type, Action<BaseGraph>> Openers = new Dictionary<Type, Action<BaseGraph>>();

        /// <summary>Registers the action that opens a graph of <paramref name="graphType"/> in its window. The
        /// last registration for a type wins. Null args are ignored with a <c>[GraphCore]</c> warning.</summary>
        public static void Register(Type graphType, Action<BaseGraph> opener)
        {
            if (graphType == null || opener == null)
            {
                Debug.LogWarning("[GraphCore] GraphEditorWindowRegistry.Register: null graphType/opener ignored.");
                return;
            }
            Openers[graphType] = opener;
        }

        /// <summary>Finds an opener registered for <paramref name="graphType"/> or any of its base graph types.</summary>
        public static bool TryGetOpener(Type graphType, out Action<BaseGraph> opener)
        {
            opener = null;
            for (var t = graphType; t != null && typeof(BaseGraph).IsAssignableFrom(t); t = t.BaseType)
                if (Openers.TryGetValue(t, out opener)) return true;
            return false;
        }

        /// <summary>
        /// Opens <paramref name="graph"/> in its registered editor. Falls back to selecting/pinging the asset
        /// (with a <c>[GraphCore]</c> diagnostic) when the graph is null or no editor is registered for its type.
        /// Never throws.
        /// </summary>
        public static void Open(BaseGraph graph)
        {
            if (graph != null && TryGetOpener(graph.GetType(), out var opener))
            {
                opener(graph);
                return;
            }

            if (graph != null)
            {
                Selection.activeObject = graph;
                EditorGUIUtility.PingObject(graph);
                Debug.LogWarning($"[GraphCore] No editor registered for graph type '{graph.GetType().Name}'; " +
                                 "selected the asset instead. Register one via GraphEditorWindowRegistry.Register.");
            }
            else
            {
                Debug.LogWarning("[GraphCore] GraphEditorWindowRegistry.Open: target graph is null (nothing to open).");
            }
        }

        /// <summary>Test hook — clears all registrations.</summary>
        public static void Clear() => Openers.Clear();
    }
}
