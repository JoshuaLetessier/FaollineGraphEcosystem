using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Detects <see cref="BaseGraph"/> assets sharing the same <see cref="BaseGraph.GraphId"/> and regenerates
    /// the duplicates' ids. The id is assigned once in <c>OnEnable</c> and meant to be stable — but duplicating
    /// an asset (Ctrl+D, or a file copy outside the editor) copies the serialized id, so two assets silently
    /// share it: SubGraph cycle detection compares <c>GraphId</c> (false positives), and save/localization keys
    /// become ambiguous. Runs automatically when graph assets are imported (thin
    /// <see cref="AssetPostprocessor"/>), and manually via <c>Faolline ▸ Graph ▸ Fix Duplicate GraphIds</c>.
    /// <para>
    /// When a duplicate set is found, the asset that was NOT just imported keeps its id (it existed first);
    /// among several imported ones, the first found keeps it. Every regeneration logs the asset path and both
    /// ids, so a rename-style workflow that WANTED to keep an id can be spotted and reverted.
    /// </para>
    /// </summary>
    public sealed class GraphIdDuplicateDetector : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            // Only pay the project scan when the import batch actually contains a graph asset.
            var importedGraphs = new HashSet<string>();
            foreach (var path in importedAssets)
                if (AssetDatabase.LoadAssetAtPath<BaseGraph>(path) != null)
                    importedGraphs.Add(path);
            if (importedGraphs.Count == 0) return;

            ScanAndFix(importedGraphs);
        }

        [MenuItem("Faolline/Graph/Fix Duplicate GraphIds")]
        private static void FixAllMenu()
        {
            int fixed_ = ScanAndFix(null);
            Debug.Log(fixed_ == 0
                ? "[GraphCore] No duplicate GraphIds found."
                : $"[GraphCore] Regenerated {fixed_} duplicate GraphId(s). See the warnings above for details.");
        }

        /// <summary>
        /// Scans every <see cref="BaseGraph"/> asset in the project, regenerates the id of each duplicate, and
        /// returns how many were regenerated. When <paramref name="preferRegenerate"/> is non-null, an asset
        /// whose path it contains loses the tie (the just-imported copy is the one that changes) — otherwise
        /// the first asset found keeps the id.
        /// </summary>
        public static int ScanAndFix(HashSet<string> preferRegenerate)
        {
            var byId = new Dictionary<string, List<string>>();
            foreach (var guid in AssetDatabase.FindAssets("t:BaseGraph"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var graph = AssetDatabase.LoadAssetAtPath<BaseGraph>(path);
                if (graph == null || string.IsNullOrEmpty(graph.GraphId)) continue;
                if (!byId.TryGetValue(graph.GraphId, out var list))
                    byId[graph.GraphId] = list = new List<string>();
                list.Add(path);
            }

            int fixedCount = 0;
            foreach (var kv in byId)
            {
                var paths = kv.Value;
                if (paths.Count < 2) continue;

                // The keeper: the first asset that was NOT part of the triggering import, else the first found.
                string keeper = null;
                if (preferRegenerate != null)
                    foreach (var p in paths)
                        if (!preferRegenerate.Contains(p)) { keeper = p; break; }
                keeper ??= paths[0];

                foreach (var path in paths)
                {
                    if (path == keeper) continue;
                    var graph = AssetDatabase.LoadAssetAtPath<BaseGraph>(path);
                    var newId = RegenerateId(graph);
                    fixedCount++;
                    Debug.LogWarning(
                        $"[GraphCore] Duplicate GraphId '{kv.Key}': '{path}' shared it with '{keeper}' — " +
                        $"regenerated to '{newId}'. GraphIds must be unique (SubGraph cycle detection and " +
                        $"save keys rely on them). If this asset was meant to REPLACE the other one, delete " +
                        $"the other asset and revert this file instead.");
                }
            }

            if (fixedCount > 0) AssetDatabase.SaveAssets();
            return fixedCount;
        }

        // _graphId is private and deliberately has no public setter — go through serialization, the same
        // channel the duplicate came from.
        private static string RegenerateId(BaseGraph graph)
        {
            var newId = System.Guid.NewGuid().ToString("D");
            var so = new SerializedObject(graph);
            so.FindProperty("_graphId").stringValue = newId;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(graph);
            return newId;
        }
    }
}
