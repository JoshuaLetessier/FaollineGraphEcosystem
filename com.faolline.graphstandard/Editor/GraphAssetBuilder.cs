using UnityEditor;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphStandard.Editor
{
    /// <summary>
    /// Persists an in-memory graph (e.g. one built with <see cref="GraphBuilder{TGraph}"/>) as an asset, with
    /// its attached actions and conditions stored as SUB-ASSETS so the asset is self-contained and portable.
    /// Only objects that are not already persisted assets are added (a shared/asset condition is not
    /// double-added).
    /// </summary>
    public static class GraphAssetBuilder
    {
        /// <summary>Writes <paramref name="graph"/> to <paramref name="path"/> with its actions/conditions as
        /// sub-assets; returns the saved graph.</summary>
        public static BaseGraph Save(BaseGraph graph, string path)
        {
            if (graph == null)
            {
                Debug.LogError("[GraphStandard] GraphAssetBuilder.Save: null graph; ignored.");
                return null;
            }
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[GraphStandard] GraphAssetBuilder.Save: empty path; ignored.");
                return graph;
            }

            AssetDatabase.CreateAsset(graph, path);

            foreach (var node in graph.Nodes)
            {
                if (node == null) continue;
                foreach (var action in node.OnEnterActions) AddSubAsset(graph, action);
                foreach (var action in node.OnExitActions)  AddSubAsset(graph, action);
                foreach (var condition in node.EntryConditions) AddSubAsset(graph, condition);
                if (node is ChoiceNodeData choice)
                    foreach (var ch in choice.Choices)
                        if (ch != null) AddSubAsset(graph, ch.Condition);
            }

            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
            return graph;
        }

        private static void AddSubAsset(Object owner, Object sub)
        {
            if (sub != null && !AssetDatabase.Contains(sub))
                AssetDatabase.AddObjectToAsset(sub, owner);
        }
    }
}
