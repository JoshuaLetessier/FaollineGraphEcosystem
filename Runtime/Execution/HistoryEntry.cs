using System.Collections.Generic;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Immutable snapshot of <see cref="BaseRunner"/> state at one node transition.
    /// Captured after <c>OnExitActions</c> and before advancing, so that
    /// <see cref="BaseRunner.GoBack"/> can restore the runner to exactly this point.
    /// </summary>
    public class HistoryEntry
    {
        /// <summary>Id of the node at the time of snapshot.</summary>
        public string NodeId { get; set; }

        /// <summary>
        /// Snapshot of the full graph execution stack at this point.
        /// Each <see cref="GraphExecutionState"/> is a shallow clone.
        /// </summary>
        public Stack<GraphExecutionState> GraphStackSnapshot { get; set; }

        /// <summary>
        /// Deep clone of the execution context at this point (values only, no subscribers).
        /// </summary>
        public BaseContext ContextSnapshot { get; set; }
    }
}
