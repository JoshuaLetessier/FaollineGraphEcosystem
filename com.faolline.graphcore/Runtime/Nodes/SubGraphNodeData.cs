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
        [SerializeField] private bool      _opensScope;

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

        /// <summary>
        /// When <c>true</c>, entering this sub-graph opens a <em>local context</em> on the parent
        /// context (a third behaviour alongside inherit / fresh-blank): the sub-graph reads through to
        /// the parent/global values but its own writes land in a transient local context that is
        /// discarded when the sub-graph ends. Takes precedence over <see cref="InheritParentContext"/>
        /// — a scoped sub-graph always rides the parent context with a local overlay. Default
        /// <c>false</c>, so pre-existing sub-graph nodes keep their original behaviour.
        /// </summary>
        public bool OpensScope
        {
            get => _opensScope;
            set => _opensScope = value;
        }
    }
}
