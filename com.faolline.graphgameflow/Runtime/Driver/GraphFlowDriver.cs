using System;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphGameFlow
{
    /// <summary>
    /// The host bridge: a scene component that runs a graphcore graph inside a live Unity scene. It owns a
    /// shared <see cref="GameFlowContext"/>, boots and drives the Linear <see cref="BaseRunner"/>, forwards
    /// <c>Update</c>'s elapsed time into <see cref="BaseRunner.Tick"/>, lets scene code inject signals, and
    /// re-exposes the runner's lifecycle events as C# <see cref="Action"/> hooks.
    /// <para>
    /// All logic lives in the public methods (<see cref="Boot"/>/<see cref="Tick"/>/<see cref="Advance"/>/
    /// <see cref="RaiseSignal(string)"/>); the Unity hooks (<c>Start</c>/<c>Update</c>/<c>OnDestroy</c>) are
    /// thin wrappers, so the whole bridge is verifiable in EditMode without entering Play.
    /// </para>
    /// </summary>
    [HelpURL("https://github.com/JoshuaLetessier/FaollineGraphEcosystem/blob/master/Assets/FaollineGraphEcosystem/com.faolline.graphgameflow/README.md")]
    public sealed class GraphFlowDriver : MonoBehaviour
    {
        [Header("Graph")]
        [SerializeField, Tooltip("The graph asset to run. Assign in the inspector or set from code before Boot().")]
        private BaseGraph _graph;

        [Header("Behaviour")]
        [SerializeField, Tooltip("When enabled, the driver advances automatically as each node completes. Choices always require an explicit ChooseById call regardless.")]
        private bool      _autoAdvance = true;
        [SerializeField, Tooltip("When enabled (default), the driver boots automatically in Start(). Disable to boot manually from code via Boot().")]
        private bool      _bootOnStart = true;
        [SerializeField, Tooltip("When enabled, this driver survives scene loads (DontDestroyOnLoad) so a single flow can span scenes. Duplicate per-scene copies self-destruct.")]
        private bool      _persistAcrossScenes = false;
        [SerializeField, Tooltip("When enabled, the driver uses Time.unscaledDeltaTime instead of Time.deltaTime. Enable for flows that must keep running when Time.timeScale is 0 (pause menus, cutscene overlays).")]
        private bool      _useUnscaledTime = false;

        private BaseRunner      _runner;
        private GameFlowContext _context;
        private ISceneLoader    _sceneLoader;
        private bool            _running;
        private float           _waitTotal;
        private float           _waitElapsed;

        /// <summary>The flow to run (assignable in the inspector or by code before <see cref="Boot"/>).</summary>
        public BaseGraph Graph { get => _graph; set => _graph = value; }

        /// <summary>When true, the driver advances automatically as each node completes.</summary>
        public bool AutoAdvance { get => _autoAdvance; set => _autoAdvance = value; }

        /// <summary>When true (default), the Unity <c>Start</c> hook boots the driver automatically on Play.</summary>
        public bool BootOnStart { get => _bootOnStart; set => _bootOnStart = value; }

        /// <summary>
        /// When true, the driver survives scene loads (<c>DontDestroyOnLoad</c>), so a single driver can run a
        /// graph that spans scenes (e.g. <c>LoadScene(Single)</c> transitions). Read at <c>Awake</c>: set it in
        /// the inspector, or on an inactive GameObject before it activates. A duplicate persistent driver (a
        /// per-scene copy) destroys itself, leaving the first one running. Default false.
        /// </summary>
        public bool PersistAcrossScenes { get => _persistAcrossScenes; set => _persistAcrossScenes = value; }

        /// <summary>When true, uses <c>Time.unscaledDeltaTime</c> so the flow keeps running at <c>timeScale=0</c>.</summary>
        public bool UseUnscaledTime { get => _useUnscaledTime; set => _useUnscaledTime = value; }

        /// <summary>
        /// The current persistent driver (the one that booted with <see cref="PersistAcrossScenes"/>), or
        /// null. Lets scene scripts reach the cross-scene driver without writing their own singleton.
        /// </summary>
        public static GraphFlowDriver Active { get; private set; }

        /// <summary>The scene loader used by scene actions. Defaults to a <see cref="UnitySceneLoader"/>.</summary>
        public ISceneLoader SceneLoader
        {
            get => _sceneLoader ?? (_sceneLoader = new UnitySceneLoader());
            set => _sceneLoader = value;
        }

        /// <summary>The shared context for the run (null before <see cref="Boot"/>).</summary>
        public GameFlowContext Context => _context;

        /// <summary>The graphcore runner the driver drives (null before <see cref="Boot"/>).</summary>
        public BaseRunner Runner => _runner;

        /// <summary>True between a successful <see cref="Boot"/> and the flow ending.</summary>
        public bool IsRunning => _running;

        /// <summary>True while running and parked on an await-signal node.</summary>
        public bool IsWaitingForSignal
            => _running && _runner != null && _runner.State == RunnerState.WaitingForSignal;

        /// <summary>
        /// The signal name the flow is currently awaiting while <see cref="IsWaitingForSignal"/>; otherwise
        /// the empty string. Lets a scene that subscribed late (after the wait fired during a scene load)
        /// recover the parked state without reaching into the runner.
        /// </summary>
        public string CurrentAwaitSignal
            => IsWaitingForSignal ? (_runner.CurrentNode?.AwaitSignalName ?? "") : "";

        /// <summary>True while running and parked on a timed node.</summary>
        public bool IsWaitingForTime
            => _running && _runner != null && _runner.State == RunnerState.WaitingForTime;

        /// <summary>
        /// Seconds left on the current timed wait while <see cref="IsWaitingForTime"/> (never negative);
        /// otherwise 0. Symmetric with <see cref="CurrentAwaitSignal"/> — lets a late-loading scene drive a
        /// synced countdown. Computed driver-side from the wait duration minus the host-fed ticks.
        /// </summary>
        public float WaitRemaining
            => IsWaitingForTime ? Mathf.Max(0f, _waitTotal - _waitElapsed) : 0f;

        /// <summary>The current timed node's total duration while <see cref="IsWaitingForTime"/>; else 0.</summary>
        public float WaitTotal => IsWaitingForTime ? _waitTotal : 0f;

        /// <summary>Raised when a node is entered.</summary>
        public event Action<BaseNodeData> OnNodeEntered;

        /// <summary>Raised when a node completes (the runner is ready to advance).</summary>
        public event Action<BaseNodeData> OnNodeCompleted;

        /// <summary>Raised when the flow reaches an end.</summary>
        public event Action<EndReason> OnEnded;

        /// <summary>Raised when the flow can no longer advance (a failed entry condition / no edge).</summary>
        public event Action OnStuck;

        /// <summary>Raised when the flow parks awaiting a signal. Args: the node + the awaited name.</summary>
        public event Action<BaseNodeData, string> OnWaitingForSignal;

        /// <summary>Raised when the flow enters a timed node. Args: the node + the wait duration (seconds).</summary>
        public event Action<BaseNodeData, float> OnWaitingForTime;

        // ── Unity hooks (thin wrappers) ─────────────────────────────────────────

        private void Awake()
        {
            if (!_persistAcrossScenes) return;
            if (Active != null && Active != this)
            {
                // A duplicate per-scene copy: the original persistent driver keeps running the flow.
                Destroy(gameObject);
                return;
            }
            Active = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start() { if (_bootOnStart) Boot(); }
        private void Update() { if (_running) Tick(_useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime); }
        private void OnDestroy() { Stop(); if (Active == this) Active = null; }

        // ── Host bridge surface ─────────────────────────────────────────────────

        /// <summary>
        /// Boots the runner over <see cref="Graph"/> and a fresh shared context (its scene loader set, then
        /// initialised from the graph's declared parameters) with an empty executor registry. Logs a
        /// <c>[GraphGameFlow]</c> warning and stays inert if there is no graph / no valid start node, or if
        /// already running.
        /// </summary>
        public void Boot() => BootInternal(null, null);

        /// <summary>
        /// Restores a flow from a <see cref="Faolline.GraphSave.GraphRunSnapshot"/>: applies the snapshot
        /// to the context, then starts the runner at the saved node. The snapshot's context values overwrite
        /// the graph's defaults — this is the "load game" path. Requires a <c>com.faolline.graphsave</c>
        /// dependency.
        /// </summary>
        public void Boot(GraphSave.GraphRunSnapshot snapshot, GameFlowContext context = null, NodeExecutorRegistry registry = null)
        {
            if (snapshot == null)
            {
                Debug.LogWarning("[GraphGameFlow] GraphFlowDriver.Boot: null snapshot; ignored.");
                return;
            }
            if (_running)
            {
                Debug.LogWarning("[GraphGameFlow] GraphFlowDriver.Boot: already running; ignored.");
                return;
            }
            if (_graph == null)
            {
                Debug.LogWarning("[GraphGameFlow] GraphFlowDriver.Boot: no graph assigned; staying inert.");
                return;
            }

            _context = context ?? new GameFlowContext { SceneLoader = SceneLoader };
            if (_context.SceneLoader == null) _context.SceneLoader = SceneLoader;

            snapshot.ApplyTo(_context, replaceCollections: true);

            _runner = new BaseRunner();
            Subscribe();
            _running = true;

            var nodeId = string.IsNullOrEmpty(snapshot.CurrentNodeId) ? _graph.EntryNodeId : snapshot.CurrentNodeId;
            _runner.StartFrom(_graph, nodeId, _context, registry ?? new NodeExecutorRegistry());
        }

        /// <summary>
        /// Boots on a CALLER-SUPPLIED context and executor registry — prepare shared state (collections,
        /// parameters, services) and register custom node executors BEFORE the flow starts. A null
        /// <paramref name="context"/> falls back to a fresh graph-initialised one; a null
        /// <paramref name="registry"/> to an empty one. A supplied context is used as-is (it is NOT
        /// re-initialised from the graph, so seeded values survive); its <see cref="GameFlowContext.SceneLoader"/>
        /// is filled with the driver's only when it is null. The same boot guards apply.
        /// </summary>
        public void Boot(GameFlowContext context, NodeExecutorRegistry registry) => BootInternal(context, registry);

        private void BootInternal(GameFlowContext context, NodeExecutorRegistry registry)
        {
            if (_running)
            {
                Debug.LogWarning("[GraphGameFlow] GraphFlowDriver.Boot: already running; ignored.");
                return;
            }
            if (_graph == null)
            {
                Debug.LogWarning("[GraphGameFlow] GraphFlowDriver.Boot: no graph assigned; staying inert.");
                return;
            }
            if (!HasValidStart(_graph))
            {
                Debug.LogWarning("[GraphGameFlow] GraphFlowDriver.Boot: graph has no valid start node (check EntryNodeId); staying inert.");
                return;
            }

            if (context != null)
            {
                // Caller owns this context (they seeded it): use it as-is, and only fill the scene loader when
                // absent so LoadSceneAction works. Do NOT InitFromGraph — that would overwrite seeded params.
                _context = context;
                if (_context.SceneLoader == null) _context.SceneLoader = SceneLoader;
            }
            else
            {
                _context = new GameFlowContext { SceneLoader = SceneLoader };
                _context.InitFromGraph(_graph);
            }

            _runner = new BaseRunner();
            Subscribe();
            _running = true;
            _runner.Start(_graph, _context, registry ?? new NodeExecutorRegistry());
        }

        /// <summary>Forwards <paramref name="deltaSeconds"/> of elapsed time to the runner. dt ≤ 0 is ignored.</summary>
        public void Tick(float deltaSeconds)
        {
            if (!_running || deltaSeconds <= 0f) return;
            if (_runner.State == RunnerState.WaitingForTime) _waitElapsed += deltaSeconds;
            _runner.Tick(deltaSeconds);
        }

        /// <summary>Advances the flow (manual advance, or programmatic). No-op when not running.</summary>
        public void Advance()
        {
            if (!_running) return;
            _runner.Proceed();
        }

        /// <summary>
        /// Selects a choice branch by its id (or port name) on the running flow — the deliberate pick a
        /// <see cref="GraphCore.ChoiceNodeData"/> waits for (it is not auto-advanced even under
        /// <see cref="AutoAdvance"/>). No-op when not running. Mirrors <see cref="Advance"/>.
        /// </summary>
        public void ChooseById(string id)
        {
            if (!_running) return;
            _runner.ChooseById(id);
        }

        /// <summary>Raises a named signal into the running flow, resuming a matching await. No-op when not running.</summary>
        public void RaiseSignal(string name)
        {
            if (!_running) return;
            _runner.RaiseSignal(name);
        }

        /// <summary>As <see cref="RaiseSignal(string)"/>, carrying a scalar payload.</summary>
        public void RaiseSignal<T>(string name, T payload)
        {
            if (!_running) return;
            _runner.RaiseSignal<T>(name, payload);
        }

        /// <summary>
        /// Detaches the driver from the runner and stops it running, so no further runner callback reaches
        /// this driver. Called automatically by <c>OnDestroy</c>; also callable to halt a flow explicitly.
        /// </summary>
        public void Stop()
        {
            Unsubscribe();
            _running = false;
        }

        // ── Internals ───────────────────────────────────────────────────────────

        private void Subscribe()
        {
            _runner.OnNodeEntered      += HandleNodeEntered;
            _runner.OnNodeCompleted    += HandleNodeCompleted;
            _runner.OnEnded            += HandleEnded;
            _runner.OnStuck            += HandleStuck;
            _runner.OnWaitingForSignal += HandleWaitingForSignal;
            _runner.OnWaitingForTime   += HandleWaitingForTime;
        }

        private void Unsubscribe()
        {
            if (_runner == null) return;
            _runner.OnNodeEntered      -= HandleNodeEntered;
            _runner.OnNodeCompleted    -= HandleNodeCompleted;
            _runner.OnEnded            -= HandleEnded;
            _runner.OnStuck            -= HandleStuck;
            _runner.OnWaitingForSignal -= HandleWaitingForSignal;
            _runner.OnWaitingForTime   -= HandleWaitingForTime;
        }

        private void HandleNodeEntered(BaseNodeData node) => OnNodeEntered?.Invoke(node);

        private void HandleNodeCompleted(BaseNodeData node)
        {
            OnNodeCompleted?.Invoke(node);
            // A choice requires a deliberate pick (ChooseById); never auto-resolve it by first-passing-edge.
            if (_autoAdvance && !(node is ChoiceNodeData)) _runner.Proceed();
        }

        private void HandleEnded(EndReason reason)
        {
            _running = false;
            OnEnded?.Invoke(reason);
        }

        private void HandleStuck() => OnStuck?.Invoke();

        private void HandleWaitingForSignal(BaseNodeData node, string signal) => OnWaitingForSignal?.Invoke(node, signal);

        private void HandleWaitingForTime(BaseNodeData node, float seconds)
        {
            _waitTotal   = seconds;
            _waitElapsed = 0f;
            OnWaitingForTime?.Invoke(node, seconds);
        }

        private static bool HasValidStart(BaseGraph graph)
        {
            if (string.IsNullOrEmpty(graph.EntryNodeId)) return false;
            foreach (var node in graph.Nodes)
                if (node != null && node.Id == graph.EntryNodeId) return true;
            return false;
        }
    }
}
