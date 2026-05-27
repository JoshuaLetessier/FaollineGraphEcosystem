using System;
using System.Collections.Generic;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Headless state machine that drives graph traversal. Requires no <c>MonoBehaviour</c>
    /// or Unity lifecycle — all events use <c>C# Action&lt;T&gt;</c>.
    /// <para>
    /// Lifecycle: call <see cref="Start"/> once, then respond to <see cref="OnNodeCompleted"/>
    /// by calling <see cref="Proceed"/> (linear/auto) or <see cref="ChooseById"/> (choices).
    /// </para>
    /// Nested sub-graphs are handled transparently via an internal graph stack.
    /// History snapshots enable <see cref="GoBack"/> and <see cref="GoBackToCheckpoint"/>.
    /// </summary>
    public class BaseRunner
    {
        // ── State ──────────────────────────────────────────────────────────────

        private RunnerState _state = RunnerState.Idle;

        /// <summary>Current execution state.</summary>
        public RunnerState State => _state;

        // ── Internal fields ────────────────────────────────────────────────────

        private readonly Stack<GraphExecutionState> _graphStack =
            new Stack<GraphExecutionState>();

        private readonly List<HistoryEntry> _history = new List<HistoryEntry>();

        private BaseContext _context;
        private NodeExecutorRegistry _registry;
        private BaseGraph _rootGraph;

        // ── Events ─────────────────────────────────────────────────────────────

        /// <summary>Raised when a node is entered (after entry-conditions pass and executor runs).</summary>
        public event Action<BaseNodeData> OnNodeEntered;

        /// <summary>
        /// Raised after the executor runs on the current node. The runner pauses here;
        /// the caller MUST call <see cref="Proceed"/> or <see cref="ChooseById"/> to advance.
        /// </summary>
        public event Action<BaseNodeData> OnNodeCompleted;

        /// <summary>Raised when an <see cref="EndNodeData"/> is reached at the root graph level.</summary>
        public event Action<EndReason> OnEnded;

        /// <summary>
        /// Raised when no valid outgoing edge can be found, or when an entry condition fails.
        /// The runner stays in <see cref="RunnerState.NodeReady"/>; the caller must decide
        /// how to recover (e.g., via <see cref="GoBack"/>).
        /// </summary>
        public event Action OnStuck;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        /// <summary>
        /// Initialises execution at <paramref name="graph"/>'s <c>EntryNodeId</c> and
        /// immediately enters the entry node (entry-conditions, enter-actions, executor, OnNodeCompleted).
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <c>graph.EntryNodeId</c> is null or empty.
        /// </exception>
        /// <exception cref="GraphCycleException">
        /// Thrown when a cycle is detected immediately on the root graph (self-cycle edge case).
        /// </exception>
        public void Start(BaseGraph graph, BaseContext context, NodeExecutorRegistry registry)
        {
            if (string.IsNullOrEmpty(graph.EntryNodeId))
                throw new InvalidOperationException(
                    "[GraphCore] Cannot start: graph.EntryNodeId is not set.");

            _context   = context;
            _registry  = registry;
            _rootGraph = graph;
            _graphStack.Clear();
            _history.Clear();

            var rootFrame = new GraphExecutionState
            {
                Graph         = graph,
                CurrentNodeId = graph.EntryNodeId,
                FrameContext  = context
            };
            _graphStack.Push(rootFrame);
            _state = RunnerState.NodeReady;
            EnterCurrentNode();
        }

        /// <summary>
        /// Advances execution: runs exit-actions on the current node, evaluates outgoing
        /// edges, snapshots history, then enters the next node. No-op when <c>State == Ended</c>.
        /// </summary>
        public void Proceed()
        {
            if (_state != RunnerState.NodeReady) return;
            ExitAndAdvance();
        }

        /// <summary>
        /// Selects the outgoing edge or choice whose <c>Id</c> (or <c>PortName</c>) matches
        /// <paramref name="id"/>, bypassing condition evaluation. Advances to that node.
        /// No-op when <c>State == Ended</c>.
        /// </summary>
        public void ChooseById(string id)
        {
            if (_state != RunnerState.NodeReady) return;
            ExitAndAdvance(forcedId: id);
        }

        /// <summary>
        /// Restores the most recent history snapshot. If the history stack is empty, this
        /// is a no-op. Calls <see cref="INodeExecutor.Undo"/> on the current node before restoring.
        /// </summary>
        public void GoBack()
        {
            if (_history.Count == 0) return;
            RestoreEntry(_history.Count - 1);
        }

        /// <summary>
        /// Restores the most recent history snapshot where the node had
        /// <c>IsCheckpoint == true</c>. No-op when no such snapshot exists.
        /// </summary>
        public void GoBackToCheckpoint()
        {
            for (int i = _history.Count - 1; i >= 0; i--)
            {
                var entry = _history[i];
                var node  = FindNodeInEntry(entry);
                if (node != null && node.IsCheckpoint)
                {
                    RestoreEntry(i);
                    return;
                }
            }
        }

        // ── Internal: node entry ───────────────────────────────────────────────

        private void EnterCurrentNode()
        {
            var frame = _graphStack.Peek();
            var node  = FindNode(frame.Graph, frame.CurrentNodeId);
            if (node == null)
            {
                UnityEngine.Debug.LogError(
                    $"[GraphCore] Node '{frame.CurrentNodeId}' not found in graph '{frame.Graph.GraphId}'.");
                return;
            }

            // 1. EntryConditions
            foreach (var condition in node.EntryConditions)
            {
                if (!condition.Evaluate(_context))
                {
                    OnStuck?.Invoke();
                    return;
                }
            }

            // 2. OnEnterActions
            foreach (var action in node.OnEnterActions)
                action.Execute(_context);

            // 3. Executor
            _registry?.GetExecutor(node.NodeType)?.Execute(node, _context);

            // 4. Raise events — runner pauses here until Proceed/ChooseById
            OnNodeEntered?.Invoke(node);
            OnNodeCompleted?.Invoke(node);
        }

        // ── Internal: node exit & advance ─────────────────────────────────────

        private void ExitAndAdvance(string forcedId = null)
        {
            var frame = _graphStack.Peek();
            var node  = FindNode(frame.Graph, frame.CurrentNodeId);
            if (node == null) return;

            // 5. OnExitActions
            foreach (var action in node.OnExitActions)
                action.Execute(_context);

            // Collect outgoing edges
            var outEdges = GetOutgoingEdges(frame.Graph, frame.CurrentNodeId);
            frame.AvailableEdges = outEdges;

            // 7. Snapshot (after exit, before advance)
            AppendSnapshot(frame.CurrentNodeId);

            // 8. Advance based on node type
            if (node is SubGraphNodeData subNode)
            {
                EnterSubGraph(subNode);
                return;
            }

            if (node is EndNodeData endNode)
            {
                HandleEndNode(endNode);
                return;
            }

            // 6. Regular node — select next edge
            BaseEdgeData selected = SelectEdge(outEdges, forcedId);
            if (selected == null)
            {
                if (outEdges.Count == 0)
                {
                    // Terminal non-end node — treat as completed
                    _state = RunnerState.Ended;
                    OnEnded?.Invoke(EndReason.Completed);
                }
                else
                {
                    OnStuck?.Invoke();
                }
                return;
            }

            frame.CurrentNodeId = selected.ToNodeId;
            EnterCurrentNode();
        }

        // ── Internal: SubGraph ─────────────────────────────────────────────────

        private void EnterSubGraph(SubGraphNodeData subNode)
        {
            var targetGraph = subNode.TargetGraph;
            if (targetGraph == null)
            {
                UnityEngine.Debug.LogError("[GraphCore] SubGraphNodeData.TargetGraph is null.");
                OnStuck?.Invoke();
                return;
            }

            // Cycle detection
            foreach (var frame in _graphStack)
            {
                if (frame.Graph.GraphId == targetGraph.GraphId)
                    throw new GraphCycleException(targetGraph.GraphId);
            }

            if (string.IsNullOrEmpty(targetGraph.EntryNodeId))
            {
                UnityEngine.Debug.LogError(
                    $"[GraphCore] SubGraph '{targetGraph.GraphId}' has no EntryNodeId.");
                OnStuck?.Invoke();
                return;
            }

            // Determine sub-graph context
            BaseContext subCtx;
            if (subNode.InheritParentContext)
            {
                subCtx = _context;
            }
            else
            {
                subCtx = new BaseContext();
                subCtx.InitFromGraph(targetGraph);
            }

            _context = subCtx;

            var subFrame = new GraphExecutionState
            {
                Graph         = targetGraph,
                CurrentNodeId = targetGraph.EntryNodeId,
                FrameContext  = subCtx
            };
            _graphStack.Push(subFrame);
            _state = RunnerState.NodeReady;
            EnterCurrentNode();
        }

        private void HandleEndNode(EndNodeData endNode)
        {
            if (_graphStack.Count > 1)
            {
                // Pop sub-graph, resume parent
                _graphStack.Pop();
                var parentFrame = _graphStack.Peek();
                _context = parentFrame.FrameContext;

                // Advance from the SubGraphNode in the parent
                var parentEdges = GetOutgoingEdges(parentFrame.Graph, parentFrame.CurrentNodeId);
                parentFrame.AvailableEdges = parentEdges;
                AppendSnapshot(parentFrame.CurrentNodeId);

                var selected = SelectEdge(parentEdges);
                if (selected == null)
                {
                    if (parentEdges.Count == 0)
                    {
                        _state = RunnerState.Ended;
                        OnEnded?.Invoke(EndReason.Completed);
                    }
                    else
                    {
                        OnStuck?.Invoke();
                    }
                    return;
                }

                parentFrame.CurrentNodeId = selected.ToNodeId;
                _state = RunnerState.NodeReady;
                EnterCurrentNode();
            }
            else
            {
                _state = RunnerState.Ended;
                OnEnded?.Invoke(endNode.EndReason);
            }
        }

        // ── Internal: history ──────────────────────────────────────────────────

        private void AppendSnapshot(string nodeId)
        {
            var stackSnapshot = CloneGraphStack();
            var entry = new HistoryEntry
            {
                NodeId             = nodeId,
                GraphStackSnapshot = stackSnapshot,
                ContextSnapshot    = _context.DeepClone()
            };

            _history.Add(entry);

            // Cap history using the root graph's HistoryDepth (0 = unlimited)
            var depth = _rootGraph != null ? _rootGraph.HistoryDepth : 0;
            if (depth > 0 && _history.Count > depth)
                _history.RemoveAt(0);
        }

        private void RestoreEntry(int index)
        {
            var entry = _history[index];

            // Call Undo on current executor before restoring
            var currentFrame = _graphStack.Peek();
            var currentNode  = FindNode(currentFrame.Graph, currentFrame.CurrentNodeId);
            if (currentNode != null)
                _registry?.GetExecutor(currentNode.NodeType)?.Undo(currentNode, _context);

            // Truncate history up to and including this entry
            _history.RemoveRange(index, _history.Count - index);

            // Restore graph stack
            _graphStack.Clear();
            // entry.GraphStackSnapshot is ordered top-first (same as Stack enumeration)
            // Re-push in reverse so that the original top ends up on top
            var frames = new List<GraphExecutionState>(entry.GraphStackSnapshot);
            for (int i = frames.Count - 1; i >= 0; i--)
                _graphStack.Push(frames[i]);

            // Restore context values (copy values from snapshot into the live context objects)
            RestoreContextValues(entry.ContextSnapshot);

            _state = RunnerState.NodeReady;
            // Re-enter the restored node
            EnterCurrentNode();
        }

        private void RestoreContextValues(BaseContext snapshot)
        {
            // Copy snapshot values INTO the live context object so external references
            // held by the caller (the original ctx passed to Start) remain valid.
            // Subscribers on the live context are preserved; only values are overwritten.
            var topFrame = _graphStack.Peek();
            topFrame.FrameContext.CopyValuesFrom(snapshot);
            _context = topFrame.FrameContext;
        }

        // ── Internal: helpers ─────────────────────────────────────────────────

        private static BaseNodeData FindNode(BaseGraph graph, string nodeId)
        {
            foreach (var node in graph.Nodes)
                if (node.Id == nodeId) return node;
            return null;
        }

        private static List<BaseEdgeData> GetOutgoingEdges(BaseGraph graph, string fromNodeId)
        {
            var result = new List<BaseEdgeData>();
            foreach (var edge in graph.Edges)
                if (edge.FromNodeId == fromNodeId) result.Add(edge);
            return result;
        }

        private BaseEdgeData SelectEdge(List<BaseEdgeData> edges, string forcedId = null)
        {
            if (forcedId != null)
            {
                foreach (var edge in edges)
                    if (edge.Id == forcedId || edge.PortName == forcedId) return edge;
                return null;
            }

            foreach (var edge in edges)
            {
                if (edge.Condition == null || edge.Condition.Evaluate(_context))
                    return edge;
            }
            return null;
        }

        private Stack<GraphExecutionState> CloneGraphStack()
        {
            // Stack<T> enumeration is top-first; rebuild in same order
            var frames = new List<GraphExecutionState>(_graphStack);
            var clone  = new Stack<GraphExecutionState>();
            for (int i = frames.Count - 1; i >= 0; i--)
                clone.Push(frames[i].ShallowClone());
            return clone;
        }

        private BaseNodeData FindNodeInEntry(HistoryEntry entry)
        {
            // The entry's GraphStackSnapshot top frame contains the graph
            if (entry.GraphStackSnapshot.Count == 0) return null;
            foreach (var frame in entry.GraphStackSnapshot)
            {
                // Top frame (first enumerated) is the relevant one
                return FindNode(frame.Graph, entry.NodeId);
            }
            return null;
        }
    }
}
