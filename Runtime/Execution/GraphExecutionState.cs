using System.Collections.Generic;

namespace Faolline.GraphCore
{
    /// <summary>
    /// One level of graph execution state on <see cref="BaseRunner"/>'s graph stack.
    /// Stores the active graph, the id of the node currently being processed, the
    /// outgoing edges available from that node, and the context associated with this
    /// stack level (relevant for isolated sub-graph contexts).
    /// </summary>
    public class GraphExecutionState
    {
        /// <summary>The graph being executed at this stack level.</summary>
        public BaseGraph Graph { get; set; }

        /// <summary>Id of the node currently being processed in this frame.</summary>
        public string CurrentNodeId { get; set; }

        /// <summary>Outgoing edges from the current node, computed on entry.</summary>
        public List<BaseEdgeData> AvailableEdges { get; set; } = new List<BaseEdgeData>();

        /// <summary>
        /// The execution context associated with this stack level. Equals the parent
        /// context when <c>InheritParentContext</c> is <c>true</c>; a fresh context otherwise.
        /// </summary>
        public BaseContext FrameContext { get; set; }

        /// <summary>
        /// Returns a shallow clone: same <see cref="Graph"/> reference, same
        /// <see cref="CurrentNodeId"/> and <see cref="FrameContext"/>, but a new copy of
        /// <see cref="AvailableEdges"/>. Used when snapshotting the graph stack for history.
        /// </summary>
        public GraphExecutionState ShallowClone() => new GraphExecutionState
        {
            Graph          = Graph,
            CurrentNodeId  = CurrentNodeId,
            AvailableEdges = new List<BaseEdgeData>(AvailableEdges),
            FrameContext   = FrameContext
        };
    }
}
