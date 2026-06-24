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
    /// </summary>
    [Serializable]
    public class GraphLinkNodeData : BaseNodeData
    {
        /// <summary>Canonical type identifier for graph-link annotation nodes.</summary>
        public const string NodeTypeId = "graphcore/graph-link";

        [SerializeField, Tooltip("Documentary reference to another graph (never executed at runtime). Any graph type is valid.")]
        private BaseGraph _targetGraph;
        [SerializeField, Tooltip("Optional author note displayed alongside the reference in the editor.")]
        private string _note;

        /// <summary>The associated graph this annotation points at (any kind). May be <c>null</c> (unlinked).
        /// Never executed — this is a documentary reference only.</summary>
        public BaseGraph TargetGraph
        {
            get => _targetGraph;
            set => _targetGraph = value;
        }

        /// <summary>Optional author note/label shown alongside the reference.</summary>
        public string Note
        {
            get => _note;
            set => _note = value;
        }
    }
}
