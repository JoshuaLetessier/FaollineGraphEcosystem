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

        /// <summary>
        /// The node currently active in the execution stack. Returns null when
        /// <see cref="State"/> is <see cref="RunnerState.Idle"/> or the stack is empty.
        /// </summary>
        public BaseNodeData CurrentNode
        {
            get
            {
                if (_graphStack.Count == 0) return null;
                var frame = _graphStack.Peek();
                return FindNode(frame.Graph, frame.CurrentNodeId);
            }
        }

        /// <summary>
        /// The graph the active node lives in — the top execution frame's graph, which is a sub-graph while the
        /// run has descended into one (so a tool can match the live node against the right graph asset). Null
        /// when the stack is empty.
        /// </summary>
        public BaseGraph CurrentGraph => _graphStack.Count == 0 ? null : _graphStack.Peek().Graph;

#if UNITY_EDITOR
        // ── Editor live-run cursor probe ─────────────────────────────────────────
        // Self-registers with GraphRunMonitor while playing so the graph editor window can highlight the active
        // node (Animator-style) for ANY host that drives a BaseRunner (gameflow, dialogue, custom). Editor- and
        // Play-only; compiled out of player builds. Reuses the runner's own lifecycle events to notify, so the
        // execution methods are untouched.
        private bool _probeWired;
        private RunnerProbe _runProbe;

        private sealed class RunnerProbe : IGraphRunProbe
        {
            private readonly BaseRunner _r;
            public RunnerProbe(BaseRunner r) => _r = r;

            // The top-of-stack cursor on `graph` (only when that frame IS the top frame), else null.
            public string ActiveNodeId(BaseGraph graph)
            {
                if (_r._graphStack.Count == 0) return null;
                var top = _r._graphStack.Peek();
                return top.Graph == graph ? top.CurrentNodeId : null;
            }

            public GraphRunNodeStatus StatusOf(BaseGraph graph, string nodeId)
            {
                if (graph == null || string.IsNullOrEmpty(nodeId) || _r._graphStack.Count == 0)
                    return GraphRunNodeStatus.None;

                // Walk the frame stack: the TOP frame's current node is the live cursor; an ANCESTOR frame's
                // current node is a sub-graph parent (still executing, one level down) → Active.
                bool isTop = true;
                foreach (var frame in _r._graphStack)   // Stack enumerates top → bottom
                {
                    if (frame.Graph == graph && frame.CurrentNodeId == nodeId)
                    {
                        if (!isTop) return GraphRunNodeStatus.Active;   // parent of a running sub-graph
                        switch (_r._state)
                        {
                            case RunnerState.WaitingForSignal:
                            case RunnerState.WaitingForTime: return GraphRunNodeStatus.Waiting;
                            case RunnerState.Ended:          return GraphRunNodeStatus.Ended;
                            default:                         return GraphRunNodeStatus.Running;
                        }
                    }
                    isTop = false;
                }

                // Otherwise it's part of the visited trail if it appears in history for this graph (the snapshot's
                // top frame is the graph the node was left in).
                var history = _r._history;
                for (int i = 0; i < history.Count; i++)
                {
                    var entry = history[i];
                    if (entry == null || entry.NodeId != nodeId || entry.GraphStackSnapshot == null ||
                        entry.GraphStackSnapshot.Count == 0)
                        continue;
                    if (entry.GraphStackSnapshot.Peek().Graph == graph)
                        return GraphRunNodeStatus.Visited;
                }

                return GraphRunNodeStatus.None;
            }
        }

        private void EditorWireProbe()
        {
            if (!UnityEngine.Application.isPlaying) return;   // the live cursor only matters in Play

            if (!_probeWired)
            {
                _probeWired = true;
                _runProbe = new RunnerProbe(this);
                // Notify on every transition that moves/recolors the cursor, via the existing events.
                OnNodeEntered      += _      => GraphRunMonitor.NotifyChanged();
                OnEnded            += _      => GraphRunMonitor.NotifyChanged();
                OnStuck            += ()     => GraphRunMonitor.NotifyChanged();
                OnWaitingForSignal += (a, b) => GraphRunMonitor.NotifyChanged();
                OnWaitingForTime   += (a, b) => GraphRunMonitor.NotifyChanged();
            }

            GraphRunMonitor.Register(_runProbe);
            GraphRunContextRegistry.Register(_runProbe, _context);
            GraphRunMonitor.NotifyChanged();
        }

        private void EditorUnwireProbe()
        {
            if (_runProbe != null)
            {
                GraphRunMonitor.Unregister(_runProbe);
                GraphRunContextRegistry.Unregister(_runProbe);
            }
        }
#endif

        // ── Internal fields ────────────────────────────────────────────────────

        private readonly Stack<GraphExecutionState> _graphStack =
            new Stack<GraphExecutionState>();

        private readonly List<HistoryEntry> _history = new List<HistoryEntry>();

        private BaseContext _context;
        private NodeExecutorRegistry _registry;
        private BaseGraph _rootGraph;
        private float _waitRemaining;
        private List<string> _subscribedSignalNames;
        private Action<SignalArgs> _contextSignalBridge;

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

        /// <summary>
        /// Raised when an awaiting node (non-empty <see cref="BaseNodeData.AwaitSignalName"/>) is entered.
        /// The runner is now in <see cref="RunnerState.WaitingForSignal"/>; call
        /// <see cref="RaiseSignal(string)"/> with the matching name to advance. Args: the node + awaited name.
        /// </summary>
        public event Action<BaseNodeData, string> OnWaitingForSignal;

        /// <summary>
        /// Raised when a node with a positive <see cref="BaseNodeData.WaitDuration"/> is entered. The runner
        /// is now <see cref="RunnerState.WaitingForTime"/>; feed elapsed time via <see cref="Tick"/> to
        /// advance. Args: the node + the duration in seconds.
        /// </summary>
        public event Action<BaseNodeData, float> OnWaitingForTime;

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
            if (graph == null)
                throw new ArgumentNullException(nameof(graph), "[GraphCore] Cannot start: graph is null.");
            if (context == null)
                throw new ArgumentNullException(nameof(context), "[GraphCore] Cannot start: context is null.");
            if (string.IsNullOrEmpty(graph.EntryNodeId))
                throw new InvalidOperationException(
                    "[GraphCore] Cannot start: graph.EntryNodeId is not set.");

            _context   = context;
            _registry  = registry;
            _rootGraph = graph;
            _graphStack.Clear();
            _history.Clear();
            ClearIndexes();

            var rootFrame = new GraphExecutionState
            {
                Graph         = graph,
                CurrentNodeId = graph.EntryNodeId,
                FrameContext  = context
            };
            _graphStack.Push(rootFrame);
            _state = RunnerState.NodeReady;
#if UNITY_EDITOR
            EditorWireProbe();
#endif
            EnterCurrentNode();
        }

        /// <summary>
        /// Starts execution at <paramref name="nodeId"/> instead of the graph's EntryNodeId.
        /// Used when restoring a saved session — skips to a known checkpoint without re-running
        /// the graph from the top. Enter-conditions and enter-actions of the restored node still run.
        /// </summary>
        public void StartFrom(BaseGraph graph, string nodeId, BaseContext context, NodeExecutorRegistry registry)
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph), "[GraphCore] Cannot start: graph is null.");
            if (string.IsNullOrEmpty(nodeId))
                throw new ArgumentException("[GraphCore] Cannot start: nodeId is null or empty.", nameof(nodeId));
            if (context == null)
                throw new ArgumentNullException(nameof(context), "[GraphCore] Cannot start: context is null.");

            _context   = context;
            _registry  = registry;
            _rootGraph = graph;
            _graphStack.Clear();
            _history.Clear();
            ClearIndexes();

            var frame = new GraphExecutionState
            {
                Graph         = graph,
                CurrentNodeId = nodeId,
                FrameContext  = context
            };
            _graphStack.Push(frame);
            _state = RunnerState.NodeReady;
#if UNITY_EDITOR
            EditorWireProbe();
#endif
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

        // ── Signals ────────────────────────────────────────────────────────────

        /// <summary>
        /// Raises a signal (no payload) into the active context, then — if the current node is awaiting
        /// exactly this name — advances execution as <see cref="Proceed"/> would. Delivery to subscribers
        /// happens even when nothing is waiting. A null/empty name logs a <c>[GraphCore]</c> warning and is ignored.
        /// </summary>
        public void RaiseSignal(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                UnityEngine.Debug.LogWarning("[GraphCore] RaiseSignal called with a null or empty name; ignored.");
                return;
            }
            _context?.RaiseSignal(name);
            ResumeIfAwaiting(name);
        }

        /// <summary>
        /// As <see cref="RaiseSignal(string)"/>, carrying a scalar payload (<c>bool</c>/<c>int</c>/
        /// <c>float</c>/<c>string</c>) readable by graph logic via <see cref="BaseContext.TryGetLastSignal"/>.
        /// </summary>
        public void RaiseSignal<T>(string name, T payload)
        {
            if (string.IsNullOrEmpty(name))
            {
                UnityEngine.Debug.LogWarning("[GraphCore] RaiseSignal called with a null or empty name; ignored.");
                return;
            }
            _context?.RaiseSignal<T>(name, payload);
            ResumeIfAwaiting(name);
        }

        private void ResumeIfAwaiting(string name)
        {
            if (_state != RunnerState.WaitingForSignal) return;
            var node = CurrentNode;
            // Resume only when the raised name is one this node awaits (logical OR over AwaitSignalNames) AND the
            // node's resume-gate passes. A match with a failing gate is ignored — the node stays parked and
            // re-armable (the actor may raise again once ready).
            if (node != null && Contains(node.AwaitSignalNames, name) && ResumeConditionsPass(node))
            {
                UnsubscribeContextSignal();
                ExitAndAdvance();
            }
        }

        private static bool Contains(IReadOnlyList<string> names, string name)
        {
            for (int i = 0; i < names.Count; i++)
                if (names[i] == name) return true;
            return false;
        }

        private bool AnyRaised(IReadOnlyList<string> names)
        {
            for (int i = 0; i < names.Count; i++)
                if (_context.HasSignalBeenRaised(names[i])) return true;
            return false;
        }

        private void SubscribeContextSignals(IReadOnlyList<string> signalNames)
        {
            UnsubscribeContextSignal();
            _subscribedSignalNames = new List<string>(signalNames);
            _contextSignalBridge = args => ResumeIfAwaiting(args.Name);
            foreach (var n in _subscribedSignalNames)
                _context?.OnSignal(n, _contextSignalBridge);
        }

        private void UnsubscribeContextSignal()
        {
            if (_subscribedSignalNames != null && _contextSignalBridge != null)
            {
                foreach (var n in _subscribedSignalNames)
                    _context?.OffSignal(n, _contextSignalBridge);
                _subscribedSignalNames = null;
                _contextSignalBridge = null;
            }
        }

        private bool ResumeConditionsPass(BaseNodeData node)
        {
            foreach (var condition in node.ResumeConditions)
            {
                if (condition == null)
                {
                    UnityEngine.Debug.LogWarning($"[GraphCore] Null resume condition skipped on node '{node.Id}'.");
                    continue;
                }
                if (!condition.Evaluate(_context)) return false;
            }
            return true;
        }

        // ── Time ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Feeds <paramref name="deltaSeconds"/> of elapsed time. When the runner is holding on a node's
        /// <see cref="BaseNodeData.WaitDuration"/> (<see cref="RunnerState.WaitingForTime"/>), the remaining
        /// time is reduced; once it reaches zero the runner advances as <see cref="Proceed"/> would. A
        /// non-positive <paramref name="deltaSeconds"/>, or a call while not time-waiting, is a no-op — so
        /// pause is simply not ticking and slow-motion is a scaled dt. The runner owns no clock.
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (_state != RunnerState.WaitingForTime) return;
            if (deltaSeconds <= 0f) return;
            _waitRemaining -= deltaSeconds;
            if (_waitRemaining <= 0f)
                ExitAndAdvance();
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

            // GraphLink is a NON-executing documentary reference. It normally sits OFF the path; if it is wired
            // onto the path, pass straight through it like a comment — no conditions, actions, executor, pause,
            // or access to its TargetGraph — so a run is unaffected by its presence.
            if (node is GraphLinkNodeData)
            {
                ExitAndAdvance();
                return;
            }

            // 1. EntryConditions
            foreach (var condition in node.EntryConditions)
            {
                if (condition == null)
                {
                    UnityEngine.Debug.LogWarning($"[GraphCore] Null condition entry skipped on node '{node.Id}'.");
                    continue;
                }
                if (!condition.Evaluate(_context))
                {
                    OnStuck?.Invoke();
                    return;
                }
            }

            // 2. OnEnterActions
            foreach (var action in node.OnEnterActions)
            {
                if (action == null) { UnityEngine.Debug.LogWarning($"[GraphCore] Null action entry skipped on node '{node.Id}'."); continue; }
                action.Execute(_context);
            }

            // 3. Executor
            _registry?.GetExecutor(node.NodeType)?.Execute(node, _context);

            // 4. Raise events
            OnNodeEntered?.Invoke(node);

            // Await-signal: hold here until ANY awaited signal is raised (logical OR over AwaitSignalNames) —
            // either via BaseRunner.RaiseSignal or directly on the context (e.g. a dialogue end callback).
            var awaitNames = node.AwaitSignalNames;
            if (awaitNames.Count > 0)
            {
                // Opt-in: a signal that already fired ahead of the cursor (recorded in the context's
                // raised-signal history) resumes the node immediately instead of parking forever — provided
                // the ResumeConditions gate also passes. Off by default (live-only park).
                bool alreadySatisfied = node.ResumeIfSignalAlreadyRaised
                    && _context != null
                    && AnyRaised(awaitNames)
                    && ResumeConditionsPass(node);

                if (!alreadySatisfied)
                {
                    _state = RunnerState.WaitingForSignal;
                    SubscribeContextSignals(awaitNames);
                    OnWaitingForSignal?.Invoke(node, awaitNames[0]);
                    return;
                }
                // else: fall through to normal node-ready completion (no park).
            }

            // Time wait: hold here until enough host-fed time has elapsed (Tick).
            if (node.WaitDuration > 0f)
            {
                _state = RunnerState.WaitingForTime;
                _waitRemaining = node.WaitDuration;
                OnWaitingForTime?.Invoke(node, node.WaitDuration);
                return;
            }

            // Runner pauses here until Proceed/ChooseById.
            _state = RunnerState.NodeReady;
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
            {
                if (action == null) { UnityEngine.Debug.LogWarning($"[GraphCore] Null action entry skipped on node '{node.Id}'."); continue; }
                action.Execute(_context);
            }

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

            // Determine sub-graph context. OpensScope takes precedence over InheritParentContext:
            // a scoped sub-graph rides the parent context with a fresh local overlay.
            BaseContext subCtx;
            bool openedScope = false;
            if (subNode.OpensScope)
            {
                subCtx = _context;
                _context.BeginLocalContext(targetGraph);
                openedScope = true;
            }
            else if (subNode.InheritParentContext)
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
                Graph              = targetGraph,
                CurrentNodeId      = targetGraph.EntryNodeId,
                FrameContext       = subCtx,
                OpenedLocalContext = openedScope
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
                var endingFrame = _graphStack.Pop();
                if (endingFrame.OpenedLocalContext)
                    endingFrame.FrameContext.EndLocalContext();
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

        /// <remarks>
        /// Deep-clones the full context per call. With <see cref="BaseGraph.HistoryDepth"/> = 0 (unlimited),
        /// memory grows linearly with traversal length.
        /// </remarks>
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

        private readonly Dictionary<BaseGraph, Dictionary<string, BaseNodeData>> _nodeIndex =
            new Dictionary<BaseGraph, Dictionary<string, BaseNodeData>>();
        private readonly Dictionary<BaseGraph, Dictionary<string, List<BaseEdgeData>>> _adjacency =
            new Dictionary<BaseGraph, Dictionary<string, List<BaseEdgeData>>>();

        private void ClearIndexes()
        {
            _nodeIndex.Clear();
            _adjacency.Clear();
        }

        private BaseNodeData FindNode(BaseGraph graph, string nodeId)
        {
            if (!_nodeIndex.TryGetValue(graph, out var index))
            {
                index = new Dictionary<string, BaseNodeData>();
                foreach (var node in graph.Nodes)
                    if (node != null) index[node.Id] = node;
                _nodeIndex[graph] = index;
            }
            return index.TryGetValue(nodeId, out var found) ? found : null;
        }

        private List<BaseEdgeData> GetOutgoingEdges(BaseGraph graph, string fromNodeId)
        {
            if (!_adjacency.TryGetValue(graph, out var adj))
            {
                adj = new Dictionary<string, List<BaseEdgeData>>();
                foreach (var edge in graph.Edges)
                {
                    if (edge == null) continue;
                    if (!adj.TryGetValue(edge.FromNodeId, out var list))
                    {
                        list = new List<BaseEdgeData>();
                        adj[edge.FromNodeId] = list;
                    }
                    list.Add(edge);
                }
                _adjacency[graph] = adj;
            }
            return adj.TryGetValue(fromNodeId, out var edges) ? edges : new List<BaseEdgeData>();
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
