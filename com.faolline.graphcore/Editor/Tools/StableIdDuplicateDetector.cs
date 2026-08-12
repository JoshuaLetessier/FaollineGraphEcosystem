using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Faolline.GraphLogging;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Detects, for EVERY <c>ScriptableObject</c> type implementing <see cref="IStableGuidIdentity"/>
    /// (<see cref="BaseGraph"/>, <see cref="CollectionEntry"/>, <see cref="CollectionDef"/>, and any future
    /// type — discovered via <see cref="TypeCache"/>, no per-type code needed here), assets that share the
    /// same stable id, and regenerates the duplicates' ids. The id is assigned once (typically in
    /// <c>OnEnable</c>, only when empty) and meant to be stable — but duplicating an asset (Ctrl+D, or a file
    /// copy outside the editor) copies the serialized id, so two assets silently share it: for a
    /// <see cref="BaseGraph"/> that means false-positive SubGraph cycle detection and ambiguous save keys;
    /// for a <see cref="CollectionEntry"/>/<see cref="CollectionDef"/>, two "different" items/buckets that
    /// silently collide in a context collection. Duplicates are scoped PER CONCRETE TYPE — a
    /// <see cref="BaseGraph"/> and a <see cref="CollectionEntry"/> coincidentally sharing a GUID string is
    /// not a collision (they are never compared to each other).
    /// <para>
    /// Runs automatically when a relevant asset is imported (thin <see cref="AssetPostprocessor"/>), and
    /// manually via <c>Faolline ▸ Graph ▸ Fix Duplicate Stable Ids</c>.
    /// </para>
    /// <para>
    /// When a duplicate set is found, the asset that was NOT just imported keeps its id (it existed first);
    /// among several imported ones, the first found keeps it. Every regeneration logs the asset path and both
    /// ids, so a rename-style workflow that WANTED to keep an id can be spotted and reverted.
    /// </para>
    /// </summary>
    public sealed class StableIdDuplicateDetector : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            // Only pay the project scan when the import batch actually contains a stable-id asset.
            var importedRelevant = new HashSet<string>();
            foreach (var path in importedAssets)
                if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) is IStableGuidIdentity)
                    importedRelevant.Add(path);
            if (importedRelevant.Count == 0) return;

            ScanAndFix(importedRelevant);
        }

        [MenuItem("Faolline/Graph/Fix Duplicate Stable Ids")]
        private static void FixAllMenu()
        {
            int fixedCount = ScanAndFix(null);
            Logging.Info("GraphCore.Editor", fixedCount == 0
                ? "[GraphCore] No duplicate stable ids found."
                : $"[GraphCore] Regenerated {fixedCount} duplicate stable id(s). See the warnings above for details.");
        }

        /// <summary>
        /// Scans every asset of every <see cref="IStableGuidIdentity"/>-implementing type in the project,
        /// regenerates the id of each duplicate (grouped separately per concrete type), and returns how many
        /// were regenerated. When <paramref name="preferRegenerate"/> is non-null, an asset whose path it
        /// contains loses the tie (the just-imported copy is the one that changes) — otherwise the first
        /// asset found keeps the id.
        /// </summary>
        public static int ScanAndFix(HashSet<string> preferRegenerate)
        {
            int fixedCount = 0;
            foreach (var type in TypeCache.GetTypesDerivedFrom<ScriptableObject>())
            {
                if (type.IsAbstract || !typeof(IStableGuidIdentity).IsAssignableFrom(type)) continue;
                fixedCount += ScanAndFixType(type, preferRegenerate);
            }
            if (fixedCount > 0) AssetDatabase.SaveAssets();
            return fixedCount;
        }

        private static int ScanAndFixType(Type type, HashSet<string> preferRegenerate)
        {
            var byId = new Dictionary<string, List<string>>();
            foreach (var guid in AssetDatabase.FindAssets($"t:{type.Name}"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var identity = AssetDatabase.LoadAssetAtPath(path, type) as IStableGuidIdentity;
                if (identity == null || string.IsNullOrEmpty(identity.StableId)) continue;
                if (!byId.TryGetValue(identity.StableId, out var list))
                    byId[identity.StableId] = list = new List<string>();
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
                    var asset = AssetDatabase.LoadAssetAtPath(path, type) as ScriptableObject;
                    var newId = RegenerateId(asset, ((IStableGuidIdentity)asset).StableIdFieldName);
                    fixedCount++;
                    Logging.Warning("GraphCore", $"[GraphCore] Duplicate {type.Name} id '{kv.Key}': '{path}' shared it with '{keeper}' — " +
                        $"regenerated to '{newId}'. Stable ids must be unique within a type (cycle detection, " +
                        $"context-collection keys, and save data rely on them). If this asset was meant to " +
                        $"REPLACE the other one, delete the other asset and revert this file instead.");
                }
            }
            return fixedCount;
        }

        // The id field is private and deliberately has no public setter — go through serialization, the
        // same channel the duplicate came from.
        private static string RegenerateId(ScriptableObject asset, string fieldName)
        {
            var newId = Guid.NewGuid().ToString("D");
            var so = new SerializedObject(asset);
            so.FindProperty(fieldName).stringValue = newId;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return newId;
        }
    }
}
