using System;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Thrown by <see cref="BaseRunner"/> when a sub-graph would create a cycle
    /// in the execution stack (i.e., the target graph is already being executed).
    /// Per Constitution Principle VI, cycle detection is mandatory at runtime.
    /// </summary>
    public sealed class GraphCycleException : Exception
    {
        /// <summary>The <c>GraphId</c> of the graph that caused the cycle.</summary>
        public string CyclicGraphId { get; }

        /// <summary>
        /// Initialises a new <see cref="GraphCycleException"/> for the given graph id.
        /// </summary>
        public GraphCycleException(string graphId)
            : base($"[GraphCore] Cycle detected: graph '{graphId}' is already in the execution stack.")
        {
            CyclicGraphId = graphId;
        }
    }
}
