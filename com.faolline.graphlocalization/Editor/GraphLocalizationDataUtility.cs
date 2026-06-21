using UnityEditor;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphLocalization.Editor
{
    /// <summary>
    /// Finds or creates the <see cref="GraphLocalizationData"/> companion asset for a graph.
    /// The companion sits beside the graph asset (same folder, suffixed with _Localization).
    /// </summary>
    public static class GraphLocalizationDataUtility
    {
        public static GraphLocalizationData GetOrCreate(BaseGraph graph)
        {
            if (graph == null) return null;
            var existing = Find(graph);
            if (existing != null) return existing;

            var graphPath = AssetDatabase.GetAssetPath(graph);
            if (string.IsNullOrEmpty(graphPath)) return null;

            var folder = System.IO.Path.GetDirectoryName(graphPath)?.Replace('\\', '/');
            var name = graph.name + "_Localization";
            var path = $"{folder}/{name}.asset";

            var data = ScriptableObject.CreateInstance<GraphLocalizationData>();
            data.GraphGuid = graph.GraphId;
            AssetDatabase.CreateAsset(data, path);
            AssetDatabase.SaveAssets();
            return data;
        }

        public static GraphLocalizationData Find(BaseGraph graph)
        {
            if (graph == null) return null;
            return FindByGuid(graph.GraphId);
        }

        public static GraphLocalizationData FindByGuid(string graphGuid)
        {
            if (string.IsNullOrEmpty(graphGuid)) return null;
            var guids = AssetDatabase.FindAssets("t:GraphLocalizationData");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<GraphLocalizationData>(path);
                if (data != null && data.GraphGuid == graphGuid) return data;
            }
            return null;
        }
    }
}
