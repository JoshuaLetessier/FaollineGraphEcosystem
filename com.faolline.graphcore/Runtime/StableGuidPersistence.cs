#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Editor-only helper that PERSISTS a stable-GUID identity assigned in <c>OnEnable</c>. An
    /// <see cref="IStableGuidIdentity"/> type assigns its GUID in memory the first time it loads with an empty
    /// id — but assigning a serialized field in code does NOT mark the asset dirty, so Unity never writes it
    /// back. An asset created through the Create menu is fine (its <c>OnEnable</c> runs before the asset is
    /// serialized to disk), but an asset that reaches an empty id ANOTHER way — a pre-existing asset from
    /// before the id field existed, or one whose old id field deserialized empty — would re-derive a DIFFERENT
    /// random GUID every session. That is invisible within one run (all references point at the same live
    /// instance) but desyncs anything crossing a session boundary: a generated <c>GraphSignals</c> constant,
    /// and, worse, a save file whose <c>RaisedSignals</c> reloaded in a session that never explicitly saved
    /// the asset would see an already-fired signal silently "un-fire".
    /// <para>
    /// <see cref="ScheduleSave"/> is called from <c>OnEnable</c> right after a fresh assignment. It defers to
    /// <see cref="EditorApplication.delayCall"/> (writing during <c>OnEnable</c>/import is unsafe), and only
    /// persists real on-disk assets — runtime instances (tests, <c>SignalDef.Create</c>) are skipped, and the
    /// whole file compiles out of player builds.
    /// </para>
    /// </summary>
    public static class StableGuidPersistence
    {
        /// <summary>Queues a save of <paramref name="asset"/> on the next editor tick (edit mode only, persistent assets only).</summary>
        public static void ScheduleSave(ScriptableObject asset)
        {
            if (asset == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.delayCall += () => SaveIfPersistentAsset(asset);
        }

        /// <summary>Marks <paramref name="asset"/> dirty and writes it to disk when it is a persistent asset; no-op otherwise.</summary>
        public static void SaveIfPersistentAsset(ScriptableObject asset)
        {
            if (asset == null || !EditorUtility.IsPersistent(asset)) return;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }

        /// <summary>
        /// SYNCHRONOUSLY flushes every <see cref="IStableGuidIdentity"/> asset in the project to disk,
        /// persisting any GUID that <c>OnEnable</c> assigned in memory but never wrote. Returns the number of
        /// assets touched. Unlike <see cref="ScheduleSave"/> (which defers to the next editor tick), this
        /// runs immediately — so it works under an automated <c>-batchmode -executeMethod … -quit</c> run,
        /// where the process exits before any deferred tick fires (exactly the pipeline that exposes the
        /// unpersisted-GUID bug on a migrated project). Run it once after migrating a pre-existing project, or
        /// as a CI step before generating constants / building saves:
        /// <c>-executeMethod Faolline.GraphCore.StableGuidPersistence.PersistAll</c>. A brand-new project that
        /// only ever creates assets through the Create menu never needs it (those persist their GUID at
        /// creation).
        /// <para>
        /// Discovers types via <see cref="TypeCache"/> (no per-type code) and de-duplicates by asset path, so
        /// a graph and its embedded signal sub-assets are saved once through the parent.
        /// </para>
        /// </summary>
        public static int PersistAll()
        {
            var paths = new HashSet<string>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<ScriptableObject>())
            {
                if (type.IsAbstract || !typeof(IStableGuidIdentity).IsAssignableFrom(type)) continue;
                foreach (var guid in AssetDatabase.FindAssets($"t:{type.Name}"))
                    paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }

            int count = 0;
            foreach (var path in paths)
            {
                if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) is IStableGuidIdentity id
                    && !string.IsNullOrEmpty(id.StableId))
                {
                    EditorUtility.SetDirty((Object)id);
                    count++;
                }
            }
            if (count > 0) AssetDatabase.SaveAssets();
            return count;
        }

        [MenuItem("Faolline/Graph/Persist Stable Ids")]
        private static void PersistAllMenu()
            => Debug.Log($"[GraphCore] Persisted {PersistAll()} stable-GUID asset(s) to disk.");
    }
}
#endif
