using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;
using Faolline.StarterGraph;
using Faolline.GraphLogging;

namespace Faolline.StarterGraph.Editor
{
    /// <summary>
    /// Editor window for the StarterGraph verification package.
    /// Opens via <c>Faolline/Open StarterGraph Editor</c> or by double-clicking a <see cref="StarterGraph"/> asset.
    /// Maintains a persistent runner session enabling GoBack and GoBackToCheckpoint after a Run.
    /// </summary>
    public class StarterGraphEditorWindow : BaseGraphEditorWindow
    {
        // ── Session state ─────────────────────────────────────────────────────

        private BaseRunner _activeRunner;
        private BaseContext _activeContext;
        private bool _hasActiveSession;

        // Choice pause/resume state
        private bool _waitingForChoice;
        private ChoiceNodeData _waitingChoiceNode;

        // Run-loop state, kept as fields so the loop can resume after a Choose.
        private bool _stuck;
        private EndReason _endReason = EndReason.Completed;
        private int _steps;

        private const int MaxSteps = 1000;

        /// <summary>True when a runner session has been started and can be navigated with GoBack.</summary>
        public bool HasActiveSession => _hasActiveSession;

        /// <summary>True while execution is paused at a Choice node awaiting a selection.</summary>
        public bool IsWaitingForChoice => _waitingForChoice;

        /// <summary>The Choice node currently awaiting a selection, or null when not waiting.</summary>
        public ChoiceNodeData WaitingChoiceNode => _waitingChoiceNode;

        /// <summary>
        /// The choices selectable at the current pause point: those with no condition or whose
        /// condition passes against the live context. Empty when not waiting for a choice.
        /// </summary>
        public IReadOnlyList<BaseChoice> AvailableChoices => GetAvailableChoices(_waitingChoiceNode);

        // ── Menu / asset opening ──────────────────────────────────────────────

        [MenuItem("Faolline/Open StarterGraph Editor")]
        public static void Open()
        {
            GetWindow<StarterGraphEditorWindow>("StarterGraph Editor");
        }

        [OnOpenAsset]
        private static bool OnOpenAsset(int instanceId, int line)
        {
            var asset = EditorUtility.InstanceIDToObject(instanceId) as StarterGraph;
            if (asset == null) return false;

            // Focus an existing window already showing this asset; otherwise open a NEW window,
            // so multiple graphs (e.g. a parent and its sub-graph) can be edited side by side.
            foreach (var existing in Resources.FindObjectsOfTypeAll<StarterGraphEditorWindow>())
            {
                if (existing.LoadedGraph == asset)
                {
                    existing.Focus();
                    return true;
                }
            }

            var window = CreateWindow<StarterGraphEditorWindow>();
            window.titleContent = new GUIContent(asset.name);
            window.LoadGraph(asset);
            return true;
        }

        // ── Factory methods ───────────────────────────────────────────────────

        protected override BaseGraphView CreateGraphView()
            => new StarterGraphView();

        protected override BaseNodeInspectorView CreateNodeInspectorView()
        {
            _testInspector = new StarterNodeInspectorView();
            return _testInspector;
        }

        private StarterNodeInspectorView _testInspector;

        protected override void OnGraphLoaded(BaseGraph graph)
        {
            _testInspector?.SetGraph(graph);
            _testInspector?.SetGraphView(GraphView as StarterGraphView);
        }

        // ── Toolbar ───────────────────────────────────────────────────────────

        protected override void PopulateToolbar(Toolbar toolbar)
        {
            toolbar.Add(new ToolbarButton(RunGraph) { text = "Run" });
            toolbar.Add(new ToolbarButton(ShowChooseMenu) { text = "Choose" });
            toolbar.Add(new ToolbarButton(Continue) { text = "▶ Continue" });
            toolbar.Add(new ToolbarButton(GoBack) { text = "← GoBack" });
            toolbar.Add(new ToolbarButton(GoBackToCheckpoint) { text = "⏮ Checkpoint" });
        }

        /// <summary>
        /// Toolbar Choose handler. When paused at a choice, opens a dropdown of the available
        /// (condition-passing) choices; selecting one calls <see cref="Choose"/>. Otherwise logs a no-op.
        /// </summary>
        private void ShowChooseMenu()
        {
            if (!_waitingForChoice)
            {
                Logging.Info("StarterGraph", "[StarterGraph] No active choice — click Run first.");
                return;
            }

            var menu = new GenericMenu();
            foreach (var choice in AvailableChoices)
            {
                string label = (choice is StarterChoice tc && !string.IsNullOrEmpty(tc.Label)) ? tc.Label : choice.Id;
                string id = choice.Id;
                menu.AddItem(new GUIContent(label), false, () => Choose(id));
            }
            menu.ShowAsContext();
        }

        private void RunGraph() => ExecuteGraph(LoadedGraph);

        // ── Public navigation API ─────────────────────────────────────────────

        /// <summary>Steps back one entry in the runner history and logs the restored node.</summary>
        public void GoBack()
        {
            if (!_hasActiveSession)
            {
                Logging.Info("StarterGraph", "[StarterGraph] No active session — click Run first.");
                return;
            }

            // Stepping back invalidates any pending choice (FR-012) and resets the forward
            // run-loop budget so a subsequent Continue can advance cleanly from the restored node.
            _waitingForChoice = false;
            _waitingChoiceNode = null;
            _stuck = false;
            _steps = 0;

            _activeRunner.GoBack();
            var node = _activeRunner.CurrentNode;
            Logging.Info("StarterGraph", node != null
                ? $"[StarterGraph] GoBack → {node.NodeType}"
                : "[StarterGraph] GoBack — nothing to go back to.");
        }

        /// <summary>Restores to the nearest checkpoint node in history and logs the result.</summary>
        public void GoBackToCheckpoint()
        {
            if (!_hasActiveSession)
            {
                Logging.Info("StarterGraph", "[StarterGraph] No active session — click Run first.");
                return;
            }

            // Restoring a checkpoint invalidates any pending choice and resets the forward
            // run-loop budget so a subsequent Continue can advance cleanly from the restored node.
            _waitingForChoice = false;
            _waitingChoiceNode = null;
            _stuck = false;
            _steps = 0;

            _activeRunner.GoBackToCheckpoint();
            var node = _activeRunner.CurrentNode;
            Logging.Info("StarterGraph", node != null
                ? $"[StarterGraph] GoBack to checkpoint → {node.NodeType}"
                : "[StarterGraph] GoBackToCheckpoint — no checkpoint in history.");
        }

        // ── Execution ─────────────────────────────────────────────────────────

        /// <summary>
        /// Executes <paramref name="graph"/> synchronously, logging each visited node.
        /// Stores the runner session for GoBack/GoBackToCheckpoint use after completion.
        /// </summary>
        public void ExecuteGraph(BaseGraph graph)
        {
            if (graph == null)
            {
                Logging.Error("StarterGraph", "[StarterGraph] No graph loaded. Open a StarterGraph asset first.");
                return;
            }

            if (string.IsNullOrEmpty(graph.EntryNodeId))
            {
                Logging.Error("StarterGraph", "[StarterGraph] Graph has no entry node set. Add a Start node and save before running.");
                return;
            }

            // Always reset session before a new run
            _hasActiveSession = false;
            _waitingForChoice = false;
            _waitingChoiceNode = null;
            _stuck = false;
            _endReason = EndReason.Completed;
            _steps = 0;

            _activeRunner = new BaseRunner();
            _activeContext = new StarterContext();
            _activeContext.InitFromGraph(graph);

            _activeRunner.OnNodeEntered += node =>
            {
                string label = (node is StarterStatementNodeData stmt && !string.IsNullOrEmpty(stmt.Label))
                    ? $" \"{stmt.Label}\""
                    : string.Empty;
                Logging.Info("StarterGraph", $"[StarterGraph] Node: {node.NodeType}{label}");
            };

            _activeRunner.OnEnded += reason => _endReason = reason;
            _activeRunner.OnStuck += () => _stuck = true;

            try
            {
                _activeRunner.Start(graph, _activeContext, new NodeExecutorRegistry());
                _hasActiveSession = true;
            }
            catch (GraphCycleException ex)
            {
                Logging.Error("StarterGraph", $"[StarterGraph] Cycle detected in graph: {ex.CyclicGraphId}. Execution aborted.");
                return;
            }
            catch (System.InvalidOperationException ex)
            {
                Logging.Error("StarterGraph", $"[StarterGraph] Cannot run graph: {ex.Message}");
                return;
            }

            DrainLoop();
        }

        /// <summary>
        /// Selects the choice with <paramref name="choiceId"/> on the paused Choice node and resumes
        /// execution. No-op (with a console message) when not currently paused at a choice.
        /// </summary>
        public void Choose(string choiceId)
        {
            if (!_waitingForChoice)
            {
                Logging.Info("StarterGraph", "[StarterGraph] No active choice — click Run first.");
                return;
            }

            _waitingForChoice = false;
            _waitingChoiceNode = null;
            _activeRunner.ChooseById(choiceId);
            DrainLoop();
        }

        /// <summary>
        /// Resumes the forward execution loop from the runner's current node — used after a GoBack or
        /// checkpoint restore to re-advance (e.g. back to a Choice node to pick a different branch).
        /// No-op (with a message) when there is no session, when paused at a choice (use Choose), or
        /// when execution has already ended.
        /// </summary>
        public void Continue()
        {
            if (!_hasActiveSession)
            {
                Logging.Info("StarterGraph", "[StarterGraph] No active session — click Run first.");
                return;
            }
            if (_waitingForChoice)
            {
                Logging.Info("StarterGraph", "[StarterGraph] Paused at a choice — use Choose, not Continue.");
                return;
            }
            if (_activeRunner.State != RunnerState.NodeReady)
            {
                Logging.Info("StarterGraph", "[StarterGraph] Nothing to continue — execution has ended. Use GoBack first.");
                return;
            }

            DrainLoop();
        }

        /// <summary>
        /// Advances the runner until it reaches a Choice node (pause), gets stuck, ends, or hits the
        /// step cap. At a Choice node with no available choices, halts as stuck instead of pausing.
        /// </summary>
        private void DrainLoop()
        {
            while (_activeRunner.State == RunnerState.NodeReady && !_stuck && _steps < MaxSteps)
            {
                if (_activeRunner.CurrentNode is ChoiceNodeData choiceNode)
                {
                    var available = GetAvailableChoices(choiceNode);
                    if (available.Count == 0)
                    {
                        Logging.Warning("StarterGraph", "[StarterGraph] Execution stopped: runner is stuck (no choice passed its condition or no choices defined).");
                        return;
                    }

                    _waitingForChoice = true;
                    _waitingChoiceNode = choiceNode;
                    Logging.Info("StarterGraph", $"[StarterGraph] Waiting for choice at node: {choiceNode.Id}");
                    return;
                }

                _activeRunner.Proceed();
                _steps++;
            }

            if (_stuck)
                Logging.Warning("StarterGraph", "[StarterGraph] Execution stopped: runner is stuck (no valid outgoing edge or entry condition failed). Make sure all nodes are connected.");
            else if (_steps >= MaxSteps)
                Logging.Error("StarterGraph", $"[StarterGraph] Execution aborted after {MaxSteps} steps — possible infinite loop.");
            else
                Logging.Info("StarterGraph", $"[StarterGraph] Graph ended: {_endReason}");
        }

        /// <summary>
        /// Returns the choices on <paramref name="node"/> that are available: those with a null
        /// condition or whose condition passes against the live context. Empty when null.
        /// </summary>
        private List<BaseChoice> GetAvailableChoices(ChoiceNodeData node)
        {
            var result = new List<BaseChoice>();
            if (node == null) return result;

            foreach (var choice in node.Choices)
            {
                if (choice == null) continue;
                if (choice.Condition == null || choice.Condition.Evaluate(_activeContext))
                    result.Add(choice);
            }
            return result;
        }
    }
}
