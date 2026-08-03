using System;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// A NON-executing, documentary reference from its host graph to another <see cref="BaseGraph"/> — used to
    /// make composition visible in the editor (e.g. "this zone flow is associated with these quests"). Unlike
    /// <see cref="SubGraphNodeData"/> (which is RUN/traversed), a GraphLink is never executed and never touches
    /// its <see cref="TargetGraph"/> at runtime: it is pure authoring metadata. If it is ever wired onto the
    /// execution path, the runner passes straight through it like a comment (no pause, no actions, no executor).
    /// <see cref="TargetGraph"/> is always typed as <see cref="BaseGraph"/>, never a lib-specific subtype.
    /// <para>
    /// The reference is GUID-backed (<see cref="TargetGraphGuid"/>), not a hard serialized <see cref="BaseGraph"/>
    /// field — Unity treats a hard reference as a build/asset-bundle dependency, which used to force the target
    /// (and everything <em>it</em> references) into the same inclusion group as the host graph for zero runtime
    /// value, since this node is never touched at runtime. <see cref="TargetGraph"/> keeps its exact public
    /// signature; only its backing storage changed.
    /// </para>
    /// </summary>
    [Serializable]
    public class GraphLinkNodeData : BaseNodeData
    {
        /// <summary>Canonical type identifier for graph-link annotation nodes.</summary>
        public const string NodeTypeId = "graphcore/graph-link";

        [SerializeField, Tooltip("Documentary reference to another graph (never executed at runtime), stored as a GUID so it never forces the target into the build.")]
        private string _targetGraphGuid;
        [SerializeField, Tooltip("Optional author note displayed alongside the reference in the editor.")]
        private string _note;

        // Session-only fallback for a target that isn't a saved project asset (e.g. a test fixture or a
        // freshly-created, not-yet-saved graph) — AssetDatabase has no GUID for such an object, so the GUID
        // field stays empty and this cache is what TargetGraph reflects back until the process ends. Never
        // serialized: on the next domain reload/session an unsaved target is expected to be gone anyway.
        [NonSerialized] private BaseGraph _targetGraphSessionCache;

        /// <summary>Raw GUID backing <see cref="TargetGraph"/>. Empty/null means unlinked. Available in
        /// Runtime too (unlike <see cref="TargetGraph"/>) so authoring-time validation can detect a target
        /// that no longer resolves without needing <c>UnityEditor</c> itself.</summary>
        public string TargetGraphGuid
        {
            get => _targetGraphGuid;
            set { _targetGraphGuid = value; _targetGraphSessionCache = null; }
        }

        /// <summary>The associated graph this annotation points at (any kind). May be <c>null</c> (unlinked).
        /// Never executed — this is a documentary reference only. Editor-only: nothing at runtime ever
        /// dereferences it (see class remarks), so it is compiled out of players entirely.</summary>
#if UNITY_EDITOR
        public BaseGraph TargetGraph
        {
            get
            {
                if (_targetGraphSessionCache != null) return _targetGraphSessionCache;
                if (string.IsNullOrEmpty(_targetGraphGuid)) return null;
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(_targetGraphGuid);
                return string.IsNullOrEmpty(path) ? null : UnityEditor.AssetDatabase.LoadAssetAtPath<BaseGraph>(path);
            }
            set
            {
                var path = value != null ? UnityEditor.AssetDatabase.GetAssetPath(value) : null;
                if (!string.IsNullOrEmpty(path))
                {
                    _targetGraphGuid = UnityEditor.AssetDatabase.AssetPathToGUID(path);
                    _targetGraphSessionCache = null;
                }
                else
                {
                    _targetGraphGuid = null;
                    _targetGraphSessionCache = value;
                }
            }
        }
#endif

        /// <summary>Optional author note/label shown alongside the reference.</summary>
        public string Note
        {
            get => _note;
            set => _note = value;
        }
    }
}
