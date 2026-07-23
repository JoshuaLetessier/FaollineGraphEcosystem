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

        [SerializeField, Tooltip("The graph asset to invoke as a nested sub-graph. Null = incomplete node (will be skipped with a warning at runtime).")]
        private BaseGraph _targetGraph;
        [SerializeField, Tooltip("When enabled, the sub-graph shares the parent's context (reads and writes the same parameters). When disabled, a fresh context is created from the sub-graph's declared parameters.")]
        private bool      _inheritParentContext;
        [SerializeField, Tooltip("Opens a local context overlay: reads fall through to parent values, writes land in a transient scope discarded on exit. Takes precedence over Inherit Parent Context.")]
        private bool      _opensScope;

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
        /// <para>
        /// <b>Known limitation:</b> <see cref="BaseContext"/> supports only ONE open local context at a
        /// time — it is a flat overlay, not a stack. Reaching a second Opens Scope sub-graph while the
        /// first is still open (possible along any path that keeps riding the same context — Opens Scope
        /// or Inherit Parent Context all the way down) silently DISCARDS the outer scope's local values;
        /// only a runtime warning fires. <c>GraphValidator</c> flags this at authoring time
        /// (see its "Opens Scope" check). A real fix (turning the overlay into a proper scope stack) is
        /// deliberately deferred — see <c>TODO.md</c> — until a non-linear/parallel execution engine
        /// (e.g. a future Behavior Tree) actually needs it, so the stack shape gets designed against real
        /// requirements instead of guessed from this one narrow, currently-unused case.
        /// </para>
        /// </summary>
        public bool OpensScope
        {
            get => _opensScope;
            set => _opensScope = value;
        }
    }
}
