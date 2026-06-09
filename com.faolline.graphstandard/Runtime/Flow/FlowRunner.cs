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
    /// </summary>
    public class FlowRunner
    {
        private readonly BaseContext _context;
        private readonly int _maxFires;
        private readonly HashSet<string> _oneShot = new HashSet<string>();
        private readonly Dictionary<string, int> _joinThresholds = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _incoming = new Dictionary<string, int>();
        private readonly Dictionary<string, List<BaseEdgeData>> _outgoing = new Dictionary<string, List<BaseEdgeData>>();
        private readonly Dictionary<string, BaseNodeData> _nodes = new Dictionary<string, BaseNodeData>();

        private readonly HashSet<string> _fired = new HashSet<string>();
        private readonly Dictionary<string, HashSet<string>> _arrived = new Dictionary<string, HashSet<string>>();
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

            foreach (var edge in graph.Edges)
            {
                if (edge == null || string.IsNullOrEmpty(edge.FromNodeId) || string.IsNullOrEmpty(edge.ToNodeId))
                    continue;
                if (!_outgoing.TryGetValue(edge.FromNodeId, out var list))
                {
                    list = new List<BaseEdgeData>();
                    _outgoing[edge.FromNodeId] = list;
                }
                list.Add(edge);
                _incoming[edge.ToNodeId] = (_incoming.TryGetValue(edge.ToNodeId, out var c) ? c : 0) + 1;
            }
        }

        /// <summary>
        /// External trigger: fires <paramref name="nodeId"/> directly (bypassing its join threshold) and
        /// cascades the propagation. Resets the per-propagation fire counter.
        /// </summary>
        public void Fire(string nodeId)
        {
            _fireCount = 0;
            _capWarned = false;
            FireNode(nodeId);
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

            // Fork: deliver a token along every condition-passing outgoing edge.
            if (!_outgoing.TryGetValue(id, out var edges)) return;
            foreach (var edge in edges)
            {
                if (edge.Condition != null && !edge.Condition.Evaluate(_context)) continue;
                var target = edge.ToNodeId;
                if (!_arrived.TryGetValue(target, out var set))
                {
                    set = new HashSet<string>();
                    _arrived[target] = set;
                }
                set.Add(edge.Id);
                if (set.Count >= Threshold(target))
                    FireNode(target);
            }
        }

        private int Threshold(string nodeId)
            => _joinThresholds.TryGetValue(nodeId, out var k)
                ? k
                : (_incoming.TryGetValue(nodeId, out var n) ? n : 0);
    }
}
