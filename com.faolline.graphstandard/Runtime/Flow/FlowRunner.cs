using System;
using System.Collections.Generic;
using Faolline.GraphCore;

namespace Faolline.GraphStandard
{
    /// <summary>
    /// Cursor-less, MULTI-ACTIVE execution engine over the graphcore substrate. Firing a node runs its
    /// enter-actions over the shared context, emits <see cref="OnNodeFired"/>, then FORKS — delivering a
    /// token along every outgoing edge whose condition passes (not a single selected edge). A node with
    /// multiple incoming edges JOINS: it fires once the number of distinct incoming edges that have
    /// delivered a token reaches its threshold (default = its incoming-edge count = AND-rendezvous;
    /// per-node configurable for k-of-N / OR). Propagation is a synchronous cascade resolving the reachable
    /// sub-flow in one <see cref="Fire"/>.
    /// <para>
    /// Re-firing is intentional: a non-one-shot node may fire again on a later propagation, and cycles are
    /// permitted but bounded by a fire-count safety cap (a <c>[GraphStandard]</c> warning is logged at the
    /// cap rather than looping forever). A per-node ONE-SHOT mark fires a node at most once until
    /// <see cref="Reset"/>. graphcore is untouched — thresholds and one-shot are FlowRunner configuration.
    /// </para>
    /// <para>
    /// The cascade is driven by an explicit work queue (not recursion), so a deep or wide flow cannot
    /// overflow the call stack before reaching the safety cap. Join bookkeeping uses a stable per-edge
    /// token assigned at construction (independent of <see cref="BaseEdgeData.Id"/>), so a graph built in
    /// code with empty edge ids still joins correctly.
    /// </para>
    /// </summary>
    public class FlowRunner
    {
        private readonly struct Link
        {
            public readonly BaseEdgeData Edge;
            public readonly string Token;
            public Link(BaseEdgeData edge, string token) { Edge = edge; Token = token; }
        }

        private readonly BaseGraph _graph;
        private readonly BaseContext _context;
        private readonly int _maxFires;
        private readonly HashSet<string> _oneShot = new HashSet<string>();
        private readonly Dictionary<string, int> _joinThresholds = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _incoming = new Dictionary<string, int>();
        private readonly Dictionary<string, List<Link>> _outgoing = new Dictionary<string, List<Link>>();
        private readonly Dictionary<string, BaseNodeData> _nodes = new Dictionary<string, BaseNodeData>();

        private readonly HashSet<string> _fired = new HashSet<string>();
        private readonly Dictionary<string, HashSet<string>> _arrived = new Dictionary<string, HashSet<string>>();
        private readonly Queue<string> _pending = new Queue<string>();
        private int _fireCount;
        private bool _capWarned;

        /// <summary>Raised when a node fires, after its enter-actions have run. The node id is passed.</summary>
        public event Action<string> OnNodeFired;

        /// <summary>
        /// Builds the flow over <paramref name="graph"/> + <paramref name="context"/>. Optionally configures
        /// <paramref name="oneShotNodeIds"/> (each fires at most once until <see cref="Reset"/>),
        /// <paramref name="joinThresholds"/> (node id → k-of-N; default per node is its incoming-edge count =
        /// AND), and <paramref name="maxFiresPerPropagation"/> (the cycle safety cap per <see cref="Fire"/>).
        /// </summary>
        public FlowRunner(BaseGraph graph, BaseContext context,
            IReadOnlyCollection<string> oneShotNodeIds = null,
            IReadOnlyDictionary<string, int> joinThresholds = null,
            int maxFiresPerPropagation = 10000)
        {
            _graph = graph;
            _context = context;
            _maxFires = maxFiresPerPropagation;

            if (oneShotNodeIds != null)
                foreach (var id in oneShotNodeIds)
                    if (!string.IsNullOrEmpty(id)) _oneShot.Add(id);
            if (joinThresholds != null)
                foreach (var kvp in joinThresholds)
                    _joinThresholds[kvp.Key] = kvp.Value;

            if (graph == null || context == null)
            {
                UnityEngine.Debug.LogWarning(
                    "[GraphStandard] FlowRunner created with a null graph/context; it will be inert.");
                return;
            }

            foreach (var node in graph.Nodes)
                if (node != null && !string.IsNullOrEmpty(node.Id))
                    _nodes[node.Id] = node;

            // Assign each edge a STABLE, unique join token at construction. The token is internal to the
            // join bookkeeping and deliberately independent of edge.Id: an author building a graph in code
            // may leave Id empty (the editor assigns GUIDs, but the data layer does not), and keying the
            // join on Id would collapse distinct incoming edges into one bucket — an AND-join would then
            // deadlock (or an OR-join fire too eagerly). A monotonic sequence guarantees uniqueness.
            int seq = 0;
            foreach (var edge in graph.Edges)
            {
                if (edge == null || string.IsNullOrEmpty(edge.FromNodeId) || string.IsNullOrEmpty(edge.ToNodeId))
                    continue;
                if (!_outgoing.TryGetValue(edge.FromNodeId, out var list))
                {
                    list = new List<Link>();
                    _outgoing[edge.FromNodeId] = list;
                }
                list.Add(new Link(edge, "#" + seq));
                _incoming[edge.ToNodeId] = (_incoming.TryGetValue(edge.ToNodeId, out var c) ? c : 0) + 1;
                seq++;
            }
#if UNITY_EDITOR
            EditorWireProbe();
#endif
        }

        /// <summary>
        /// External trigger: fires <paramref name="nodeId"/> directly (bypassing its join threshold) and
        /// cascades the propagation. Resets the per-propagation fire counter.
        /// </summary>
        public void Fire(string nodeId)
        {
            _fireCount = 0;
            _capWarned = false;
            _pending.Clear();
            _pending.Enqueue(nodeId);
            while (_pending.Count > 0)
                FireNode(_pending.Dequeue());
        }

        /// <summary>Clears all fired and token state — re-arms one-shots for a fresh pass.</summary>
        public void Reset()
        {
            _fired.Clear();
            _arrived.Clear();
        }

        /// <summary>Whether <paramref name="nodeId"/> has fired since the last <see cref="Reset"/>.</summary>
        public bool HasFired(string nodeId) => nodeId != null && _fired.Contains(nodeId);

        /// <summary>A snapshot of the node ids fired since the last <see cref="Reset"/>.</summary>
        public IReadOnlyCollection<string> FiredNodeIds => new List<string>(_fired);

#if UNITY_EDITOR
        // ── Editor live-run probe ────────────────────────────────────────────────
        // Self-registers with GraphRunMonitor while playing so the graph editor window shows the fired set
        // (Completed) and the most-recent fire (Running) of this multi-active flow.
        private bool _probeWired;
        private string _lastFired;
        private FlowProbe _runProbe;

        private sealed class FlowProbe : IGraphRunProbe
        {
            private readonly FlowRunner _f;
            public FlowProbe(FlowRunner f) => _f = f;

            public string ActiveNodeId(BaseGraph graph) => graph == _f._graph ? _f._lastFired : null;

            public GraphRunNodeStatus StatusOf(BaseGraph graph, string nodeId)
            {
                if (graph != _f._graph || string.IsNullOrEmpty(nodeId)) return GraphRunNodeStatus.None;
                if (nodeId == _f._lastFired) return GraphRunNodeStatus.Running;      // most-recent fire — pulses
                return _f._fired.Contains(nodeId) ? GraphRunNodeStatus.Completed : GraphRunNodeStatus.None;
            }
        }

        private void EditorWireProbe()
        {
            if (!UnityEngine.Application.isPlaying) return;   // the live map only matters in Play
            if (!_probeWired)
            {
                _probeWired = true;
                _runProbe = new FlowProbe(this);
                OnNodeFired += id => { _lastFired = id; GraphRunMonitor.NotifyChanged(); };
            }
            GraphRunMonitor.Register(_runProbe);
            GraphRunMonitor.NotifyChanged();
        }
#endif

        /// <summary>
        /// Unregisters this runner's editor live-state probe from <see cref="GraphRunMonitor"/>. A host that
        /// discards the runner (teardown, replacing it with a new one over the same graph) MUST call this —
        /// the graph editor takes the first probe answering for a graph, so a dead runner's probe would
        /// shadow the live one. No-op outside the editor; compiled empty in player builds.
        /// </summary>
        public void DetachEditorProbe()
        {
#if UNITY_EDITOR
            if (_runProbe != null) GraphRunMonitor.Unregister(_runProbe);
#endif
        }

        // ── Internals ───────────────────────────────────────────────────────────

        private void FireNode(string id)
        {
            if (string.IsNullOrEmpty(id) || !_nodes.TryGetValue(id, out var node)) return;
            if (_oneShot.Contains(id) && _fired.Contains(id)) return;            // one-shot guard
            if (++_fireCount > _maxFires)                                        // cycle safety cap
            {
                if (!_capWarned)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[GraphStandard] FlowRunner exceeded {_maxFires} fires in one propagation (possible cycle); stopping.");
                    _capWarned = true;
                }
                return;
            }

            // Do the work, announce, then consume this node's accumulated tokens.
            foreach (var action in node.OnEnterActions)
                action?.Execute(_context);
            _fired.Add(id);
            OnNodeFired?.Invoke(id);
            if (_arrived.TryGetValue(id, out var myTokens))
                myTokens.Clear();

            // Fork: deliver a token along every condition-passing outgoing edge; enqueue a target the moment
            // its join threshold is met, consuming its rendezvous so a deferred fire is enqueued only once.
            if (!_outgoing.TryGetValue(id, out var links)) return;
            foreach (var link in links)
            {
                var edge = link.Edge;
                if (edge.Condition != null && !edge.Condition.Evaluate(_context)) continue;
                var target = edge.ToNodeId;
                if (!_arrived.TryGetValue(target, out var set))
                {
                    set = new HashSet<string>();
                    _arrived[target] = set;
                }
                set.Add(link.Token);
                if (set.Count >= Threshold(target))
                {
                    set.Clear();
                    _pending.Enqueue(target);
                }
            }
        }

        private int Threshold(string nodeId)
            => _joinThresholds.TryGetValue(nodeId, out var k)
                ? k
                : (_incoming.TryGetValue(nodeId, out var n) ? n : 0);
    }
}
