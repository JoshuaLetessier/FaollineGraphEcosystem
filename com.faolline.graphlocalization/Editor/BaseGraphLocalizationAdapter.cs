using UnityEditor;
using UnityEngine;

namespace Faolline.GraphLocalization.Editor
{
    /// <summary>
    /// Generic base for per-lib localization adapters. Handles the <see cref="AssetDatabase"/> scan,
    /// metadata bookkeeping, and log — subclasses provide only key extraction.
    /// <typeparam name="TGraph">The ScriptableObject graph type to scan (e.g. DialogueGraph, QuestGraph).</typeparam>
    /// </summary>
    public abstract class BaseGraphLocalizationAdapter<TGraph> : IGraphLocalizationAdapter
        where TGraph : ScriptableObject
    {
        public abstract string LibName { get; }

        public void ScanAndIndex(LocalizationDatabase database)
        {
            int totalKeys = 0;

            var guids = AssetDatabase.FindAssets($"t:{typeof(TGraph).Name}");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var graph = AssetDatabase.LoadAssetAtPath<TGraph>(path);
                if (graph == null) continue;

                var entry = database.GetOrCreateGraphEntry(guid, graph.name);
                totalKeys += ExtractGraphKeys(graph, entry);
            }

            int globalKeys = ExtractGlobalKeys(database);
            totalKeys += globalKeys;

            database.TotalGraphsScanned = guids.Length;
            database.TotalKeysFound = totalKeys;

            Debug.Log($"[{LibName}] {guids.Length} graphs, {totalKeys} keys.");
        }

        /// <summary>
        /// Extract localization keys from a single graph asset and add them to <paramref name="entry"/>.
        /// Return the number of keys added.
        /// </summary>
        protected abstract int ExtractGraphKeys(TGraph graph, LocalizationGraphEntry entry);

        /// <summary>
        /// Extract global keys not tied to a specific graph (e.g. Speaker display names).
        /// Override to add lib-specific global keys. Default returns 0.
        /// </summary>
        protected virtual int ExtractGlobalKeys(LocalizationDatabase database) => 0;
    }
}
