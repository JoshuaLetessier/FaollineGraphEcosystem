#if UNITY_EDITOR
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
    /// persists real on-disk assets — runtime instances (tests, <c>SignalName.Create</c>) are skipped, and the
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
    }
}
#endif
