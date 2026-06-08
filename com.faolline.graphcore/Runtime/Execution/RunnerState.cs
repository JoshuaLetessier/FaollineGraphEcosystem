namespace Faolline.GraphCore
{
    /// <summary>State machine states for <see cref="BaseRunner"/>.</summary>
    public enum RunnerState
    {
        /// <summary><see cref="BaseRunner.Start"/> has not been called yet.</summary>
        Idle = 0,

        /// <summary>
        /// The current node has been entered (conditions checked, enter-actions executed,
        /// executor called). Waiting for <see cref="BaseRunner.Proceed"/> or
        /// <see cref="BaseRunner.ChooseById"/>.
        /// </summary>
        NodeReady = 1,

        /// <summary>
        /// A <see cref="SubGraphNodeData"/> has been entered; the sub-graph is now
        /// executing. The parent frame is suspended on the stack.
        /// </summary>
        Paused = 2,

        /// <summary>An <see cref="EndNodeData"/> has been reached and processed.</summary>
        Ended = 3,

        /// <summary>
        /// The current node declared <see cref="BaseNodeData.AwaitSignalName"/>; the runner entered it
        /// and is holding until that signal is raised. <see cref="BaseRunner.Proceed"/> and
        /// <see cref="BaseRunner.ChooseById"/> are no-ops in this state — only a matching
        /// <see cref="BaseRunner.RaiseSignal(string)"/> advances execution.
        /// </summary>
        WaitingForSignal = 4
    }
}
