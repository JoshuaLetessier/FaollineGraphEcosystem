namespace Faolline.GraphCore
{
    /// <summary>
    /// The first mismatch <see cref="GraphRunPath.TryResolve(BaseGraph, System.Collections.Generic.IReadOnlyList{GraphRunFrame}, out GraphRunPathError)"/>
    /// found between an execution path and the graphs it walks. Nothing is ever restored from a path that produced
    /// one; <see cref="DeepestValidFrameIndex"/> lets the caller choose its own fallback.
    /// </summary>
    public sealed class GraphRunPathError
    {
        /// <summary>Creates an error description.</summary>
        public GraphRunPathError(GraphRunPathErrorReason reason, int frameIndex, int deepestValidFrameIndex, string message)
        {
            Reason                 = reason;
            FrameIndex             = frameIndex;
            DeepestValidFrameIndex = deepestValidFrameIndex;
            Message                = message;
        }

        /// <summary>What does not match.</summary>
        public GraphRunPathErrorReason Reason { get; }

        /// <summary>Index of the level where the check failed (0 = root); -1 when the path itself is empty.</summary>
        public int FrameIndex { get; }

        /// <summary>
        /// Index of the deepest level such that the path cut after it would itself pass the check; -1 when no
        /// level does. Resuming there is the caller's decision: for an enclosing level it re-enters its
        /// <see cref="SubGraphNodeData"/>, which replays that sub-graph from its start.
        /// </summary>
        public int DeepestValidFrameIndex { get; }

        /// <summary>One English sentence naming the reason, the level and the ids involved.</summary>
        public string Message { get; }

        /// <inheritdoc/>
        public override string ToString() => Message;
    }
}
