using System;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Invokes another <see cref="BaseGraph"/> as a nested sub-graph.
    /// This is the only cross-graph invocation mechanism permitted by the constitution —
    /// <see cref="TargetGraph"/> is always typed as <see cref="BaseGraph"/>, never as a
    /// lib-specific subtype.
    /// </summary>
    [Serializable]
    public class SubGraphNodeData : BaseNodeData
    {
        /// <summary>Canonical type identifier for sub-graph nodes.</summary>
        public const string NodeTypeId = "graphcore/subgraph";

        [SerializeField] private BaseGraph _targetGraph;
        [SerializeField] private bool      _inheritParentContext;

        /// <summary>
        /// The graph to invoke. <c>null</c> indicates an incomplete or unlinked node.
        /// </summary>
        public BaseGraph TargetGraph
        {
            get => _targetGraph;
            set => _targetGraph = value;
        }

        /// <summary>
        /// When <c>true</c>, the sub-graph execution receives the parent's context.
        /// When <c>false</c>, the runtime creates a fresh context for the sub-graph.
        /// </summary>
        public bool InheritParentContext
        {
            get => _inheritParentContext;
            set => _inheritParentContext = value;
        }
    }
}
