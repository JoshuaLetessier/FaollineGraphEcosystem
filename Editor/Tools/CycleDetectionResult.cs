using System.Collections.Generic;

namespace Faolline.GraphCore.Editor
{
    /// <summary>Immutable result of a <see cref="CycleDetector.Check"/> call.</summary>
    public readonly struct CycleDetectionResult
    {
        /// <summary>true if the proposed connection would form a cycle.</summary>
        public bool HasCycle { get; }

        /// <summary>
        /// Sequence of GraphId values that form the cycle in DFS traversal order.
        /// Empty when HasCycle is false.
        /// </summary>
        public IReadOnlyList<string> CyclePath { get; }

        public CycleDetectionResult(bool hasCycle, IReadOnlyList<string> cyclePath)
        {
            HasCycle = hasCycle;
            CyclePath = cyclePath ?? new List<string>(0);
        }
    }
}
