namespace Faolline.GraphStandard
{
    /// <summary>
    /// The reactive state of a node under the <see cref="ReactiveEvaluator"/>, derived from graph
    /// topology and the completed-set.
    /// </summary>
    public enum ReactiveNodeState
    {
        /// <summary>At least one prerequisite is not yet Completed.</summary>
        Locked = 0,

        /// <summary>No prerequisites, or all prerequisites are Completed — and the node is not itself Completed.</summary>
        Available = 1,

        /// <summary>The node's id is present in the completed-set.</summary>
        Completed = 2
    }
}
