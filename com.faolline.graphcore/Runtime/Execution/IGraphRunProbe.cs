#if UNITY_EDITOR
namespace Faolline.GraphCore
{
    /// <summary>
    /// A live graph execution the editor can visualize, the way the Animator window highlights running state —
    /// but as a full per-node state map, not just a single cursor. Engine-agnostic: a Linear runner, a reactive
    /// evaluator, or a flow runner each map their own notion of "where we are" onto <see cref="GraphRunNodeStatus"/>.
    /// Editor-only — no presence in player builds (compiled out with <see cref="GraphRunMonitor"/>).
    /// </summary>
    public interface IGraphRunProbe
    {
        /// <summary>
        /// The visual status of <paramref name="nodeId"/> within <paramref name="graph"/> right now —
        /// <see cref="GraphRunNodeStatus.None"/> when this probe is not running that graph or the node is
        /// irrelevant. The editor calls this for every node of the graph it displays, so it can paint the whole
        /// map (live cursor, visited trail, sub-graph parents, reactive Locked/Available/Completed, …).
        /// </summary>
        GraphRunNodeStatus StatusOf(BaseGraph graph, string nodeId);

        /// <summary>
        /// The single live-cursor node on <paramref name="graph"/> (the top-of-stack active node), used to focus
        /// the pulse animation. Null when the probe is not running that graph, or for cursor-less engines
        /// (reactive) that have no single active node.
        /// </summary>
        string ActiveNodeId(BaseGraph graph);
    }

    /// <summary>
    /// The visual status of a node in a live run. Linear runs use Running/Waiting/Active/Visited/Ended;
    /// reactive runs use Locked/Available/Completed; flow runs use Running (last fired) + Completed (fired).
    /// </summary>
    public enum GraphRunNodeStatus
    {
        /// <summary>Not part of the live run (paint nothing).</summary>
        None = 0,
        /// <summary>The live cursor — the node executing right now (pulses).</summary>
        Running,
        /// <summary>Parked on this node awaiting a signal or a timer.</summary>
        Waiting,
        /// <summary>An active ancestor: a sub-graph node whose sub-graph is currently running (solid, no pulse).</summary>
        Active,
        /// <summary>Already passed earlier in this run (the visited trail).</summary>
        Visited,
        /// <summary>The run ended on this node.</summary>
        Ended,
        /// <summary>Reactive: prerequisites unmet (greyed).</summary>
        Locked,
        /// <summary>Reactive: unlocked but not yet completed.</summary>
        Available,
        /// <summary>Reactive/Flow: completed / fired.</summary>
        Completed
    }
}
#endif
