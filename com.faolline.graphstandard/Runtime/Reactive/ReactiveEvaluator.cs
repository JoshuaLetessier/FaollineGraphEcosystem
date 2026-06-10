using System;
using System.Collections.Generic;
using Faolline.GraphCore;

namespace Faolline.GraphStandard
{
    /// <summary>
    /// Cursor-less reactive engine over the graphcore substrate. It reads each edge as a PREREQUISITE
    /// (an edge <c>A→C</c> means "C requires A") and derives every node's <see cref="ReactiveNodeState"/>
    /// from graph topology plus a <em>completed-set</em> — a graphcore string-set collection on the shared
    /// <see cref="BaseContext"/>. A node is <see cref="ReactiveNodeState.Completed"/> when its id is in the
    /// set; <see cref="ReactiveNodeState.Available"/> when it has no prerequisites or all are Completed
    /// (AND); otherwise <see cref="ReactiveNodeState.Locked"/>.
    /// <para>
    /// <see cref="MarkCompleted"/> records completion in the collection (so it persists and history-restores
    /// via graphcore) and re-evaluates, cascading unlocks and raising <see cref="OnNodeAvailable"/> /
    /// <see cref="OnNodeCompleted"/>. There is no traversal: many nodes may be Available at once, and "back"
    /// is a re-pass (re-derive from a smaller set), never an undo of side-effects.
    /// </para>
    /// Construct, subscribe to the events, then call <see cref="Start"/> to receive the initial emission.
    /// </summary>
    public class ReactiveEvaluator
    {
        private readonly BaseGraph _graph;
        private readonly BaseContext _context;
        private readonly string _completedSetKey;
        private readonly Dictionary<string, List<string>> _prerequisites = new Dictionary<string, List<string>>();
        private readonly Dictionary<string, ReactiveNodeState> _states = new Dictionary<string, ReactiveNodeState>();
        // Per-node required count k (how many prerequisites must be Completed for Available). A node not
        // listed here defaults to its full prerequisite count N (i.e. AND). 0.2.0.
        private readonly Dictionary<string, int> _requiredCounts;

        /// <summary>Raised when a node enters <see cref="ReactiveNodeState.Available"/>; the node id is passed.</summary>
        public event Action<string> OnNodeAvailable;

        /// <summary>Raised when a node enters <see cref="ReactiveNodeState.Completed"/>; the node id is passed.</summary>
        public event Action<string> OnNodeCompleted;

        /// <summary>
        /// Raised when a node enters <see cref="ReactiveNodeState.Locked"/>; the node id is passed. The
        /// symmetric counterpart of <see cref="OnNodeAvailable"/> / <see cref="OnNodeCompleted"/> — fires on a
        /// backward transition during <see cref="Reevaluate"/> (e.g. a step-back drops the completed-set below
        /// a node's threshold) and once per initially-Locked node during <see cref="Start"/>. Lets a host
        /// react to re-locking without inferring it.
        /// </summary>
        public event Action<string> OnNodeLocked;

        /// <summary>
        /// Builds the prerequisite map from <paramref name="graph"/>'s edges and computes the initial node
        /// states (silently — call <see cref="Start"/> after subscribing to receive the initial events).
        /// <para>
        /// <paramref name="requiredCounts"/> optionally maps a node id to the number of its prerequisites
        /// that must be Completed for it to become Available (k-of-N): <c>k = N</c> is AND (the default for
        /// any unlisted node), <c>k = 1</c> is OR, <c>1 &lt; k &lt; N</c> is N-of-M, <c>k ≤ 0</c> is ungated,
        /// and <c>k &gt; N</c> never becomes Available from prerequisites. Pass <c>null</c> for all-AND.
        /// </para>
        /// </summary>
        public ReactiveEvaluator(BaseGraph graph, BaseContext context, string completedSetKey,
            IReadOnlyDictionary<string, int> requiredCounts = null)
        {
            _graph = graph;
            _context = context;
            _completedSetKey = completedSetKey;

            _requiredCounts = new Dictionary<string, int>();
            if (requiredCounts != null)
                foreach (var kvp in requiredCounts)
                    _requiredCounts[kvp.Key] = kvp.Value;

            if (graph == null || context == null || string.IsNullOrEmpty(completedSetKey))
            {
                UnityEngine.Debug.LogWarning(
                    "[GraphStandard] ReactiveEvaluator created with a null graph/context or empty completed-set key; it will be inert.");
                return;
            }

            // An edge From→To makes 'From' a prerequisite of 'To'.
            foreach (var edge in graph.Edges)
            {
                if (edge == null || string.IsNullOrEmpty(edge.ToNodeId) || string.IsNullOrEmpty(edge.FromNodeId))
                    continue;
                if (!_prerequisites.TryGetValue(edge.ToNodeId, out var list))
                {
                    list = new List<string>();
                    _prerequisites[edge.ToNodeId] = list;
                }
                if (!list.Contains(edge.FromNodeId))
                    list.Add(edge.FromNodeId);
            }

            foreach (var node in graph.Nodes)
                if (node != null && !string.IsNullOrEmpty(node.Id))
                    _states[node.Id] = DeriveState(node.Id);
        }

        /// <summary>
        /// Emits the initial events: <see cref="OnNodeAvailable"/> for every currently-Available node and
        /// <see cref="OnNodeCompleted"/> for every currently-Completed node (in graph order). Call once,
        /// after subscribing.
        /// </summary>
        public void Start()
        {
            if (_graph == null) return;
            foreach (var node in _graph.Nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.Id)) continue;
                EmitFor(node.Id, GetState(node.Id));
            }
        }

        /// <summary>
        /// Marks <paramref name="nodeId"/> complete: records it in the completed-set collection and
        /// re-evaluates (cascading unlocks). No-op when the node is already completed.
        /// </summary>
        public void MarkCompleted(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId) || string.IsNullOrEmpty(_completedSetKey) || _context == null)
                return;
            if (_context.CollectionContains(_completedSetKey, nodeId))
                return; // already completed — no duplicate, no events
            _context.AddToCollection(_completedSetKey, nodeId);
            Reevaluate();
        }

        /// <summary>
        /// Re-derives every node's state from the current completed-set and raises events for the
        /// transitions. Call after the host restores the context (step-back / un-complete) to a different
        /// completed-set. Derivation is idempotent: state depends only on the current set.
        /// </summary>
        public void Reevaluate()
        {
            if (_graph == null) return;
            var next = new Dictionary<string, ReactiveNodeState>();
            foreach (var node in _graph.Nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.Id)) continue;
                var state = DeriveState(node.Id);
                next[node.Id] = state;
                bool known = _states.TryGetValue(node.Id, out var prev);
                if (!known || prev != state)
                    EmitFor(node.Id, state);
            }
            _states.Clear();
            foreach (var kvp in next)
                _states[kvp.Key] = kvp.Value;
        }

        /// <summary>Current derived state of <paramref name="nodeId"/> (Locked for an unknown id).</summary>
        public ReactiveNodeState GetState(string nodeId)
            => (nodeId != null && _states.TryGetValue(nodeId, out var s)) ? s : ReactiveNodeState.Locked;

        /// <summary>The ids of all nodes currently <see cref="ReactiveNodeState.Available"/>.</summary>
        public IReadOnlyCollection<string> AvailableNodeIds => CollectByState(ReactiveNodeState.Available);

        /// <summary>The ids of all nodes currently <see cref="ReactiveNodeState.Completed"/>.</summary>
        public IReadOnlyCollection<string> CompletedNodeIds => CollectByState(ReactiveNodeState.Completed);

        // ── Internals ───────────────────────────────────────────────────────────

        private void EmitFor(string nodeId, ReactiveNodeState state)
        {
            if (state == ReactiveNodeState.Available) OnNodeAvailable?.Invoke(nodeId);
            else if (state == ReactiveNodeState.Completed) OnNodeCompleted?.Invoke(nodeId);
            else if (state == ReactiveNodeState.Locked) OnNodeLocked?.Invoke(nodeId);
        }

        private ReactiveNodeState DeriveState(string nodeId)
        {
            if (_context != null && _context.CollectionContains(_completedSetKey, nodeId))
                return ReactiveNodeState.Completed;

            int n = 0, completed = 0;
            if (_prerequisites.TryGetValue(nodeId, out var prereqs))
            {
                n = prereqs.Count;
                foreach (var prereq in prereqs)
                    if (_context != null && _context.CollectionContains(_completedSetKey, prereq))
                        completed++;
            }

            // Required count k: configured per node, else the full prerequisite count N (AND).
            int required = _requiredCounts.TryGetValue(nodeId, out var k) ? k : n;
            return completed >= required ? ReactiveNodeState.Available : ReactiveNodeState.Locked;
        }

        private List<string> CollectByState(ReactiveNodeState state)
        {
            var result = new List<string>();
            foreach (var kvp in _states)
                if (kvp.Value == state) result.Add(kvp.Key);
            return result;
        }
    }
}
