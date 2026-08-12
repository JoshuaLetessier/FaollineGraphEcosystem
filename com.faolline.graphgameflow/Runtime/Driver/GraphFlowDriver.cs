using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Faolline.GraphCore;
using Faolline.GraphLogging;


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
    [HelpURL("https://github.com/JoshuaLetessier/FaollineGraphEcosystem/blob/master/com.faolline.graphgameflow/README.md")]
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

        // Cap on the auto-advance pump below — a cycle with no pause node (no await/wait/choice/end on the
        // loop) would otherwise auto-advance forever. Matches DialoguePlayer.MaxDrainSteps.
        private const int MaxAutoAdvanceSteps = 1000;

        private BaseRunner      _runner;
        private GameFlowContext _context;
        private ISceneLoader    _sceneLoader;
        private bool            _running;
        private bool            _autoAdvancePending;
        private float           _waitTotal;
        private float           _waitElapsed;

        // Tracks whether a top-level entry point (Boot/Tick/Advance/ChooseById/RaiseSignal) is currently
        // unwinding. BaseRunner fires OnEnded synchronously and inline (from deep inside ExitAndAdvance), so a
        // driver OnEnded subscriber that calls Boot() to reboot runs on the SAME call stack as whatever
        // Advance/Tick/RaiseSignal triggered it. Mutating _runner/_context/_running right there would corrupt
        // the still-unwinding outer call. Instead, a reentrant Boot() request is queued here and replayed once
        // the outermost dispatch finishes — see BeginDispatch/EndDispatch and every Boot overload.
        private int    _dispatchDepth;
        private Action _pendingBoot;

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
        /// <para>
        /// A DELIBERATE, narrow exception to this ecosystem's "no singletons, no service locator" rule (see
        /// <c>INTEGRATION.md</c>) — it exists only because a scene script dropped into a freshly-loaded
        /// scene (a physics trigger, a UI button) has no wiring path to the persistent driver at all, and
        /// forcing every such script through a DI container/gateway just to raise one signal is exactly the
        /// re-abstraction ceremony that document warns against. Wherever a reference CAN be threaded through
        /// — a component field, a constructor, a container registration, or an explicit target like
        /// <see cref="AsyncSceneLoader.SignalDriver"/> — prefer that over <c>Active</c>; reach for this only
        /// from code that is genuinely reference-less (see <see cref="ContextTrigger"/>'s own fallback use).
        /// </para>
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

        /// <summary>
        /// While true, <see cref="Tick"/> is a no-op — the flow's TIME stops (a parked timed wait holds its
        /// <see cref="WaitRemaining"/>), which is what a loading screen or pause menu needs. Deliberate calls
        /// stay live: <see cref="Advance"/>, <see cref="ChooseById"/> and <see cref="RaiseSignal(string)"/>
        /// still drive the flow, so a completion signal raised mid-pause resumes a parked await as usual.
        /// <see cref="AsyncSceneLoader"/> can manage this automatically while its queue is busy
        /// (<c>PauseDriverWhileLoading</c>).
        /// </summary>
        public bool Paused { get; set; }

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
        public void Boot()
        {
            if (_dispatchDepth > 0) { _pendingBoot = Boot; return; }
            BeginDispatch();
            try { BootInternal(null, null); }
            finally { EndDispatch(); }
        }

        /// <summary>
        /// Restores a flow from a <see cref="Faolline.GraphSave.GraphRunSnapshot"/>: applies the snapshot
        /// to the context, then starts the runner at the saved node. The snapshot's context values overwrite
        /// the graph's defaults — this is the "load game" path. Requires a <c>com.faolline.graphsave</c>
        /// dependency.
        /// </summary>
        public void Boot(GraphSave.GraphRunSnapshot snapshot, GameFlowContext context = null, NodeExecutorRegistry registry = null)
        {
            if (_dispatchDepth > 0) { _pendingBoot = () => Boot(snapshot, context, registry); return; }
            BeginDispatch();
            try
            {
                if (snapshot == null)
                {
                    Logging.Warning("GraphGameFlow", "[GraphGameFlow] GraphFlowDriver.Boot: null snapshot; ignored.");
                    return;
                }
                if (_running)
                {
                    Logging.Warning("GraphGameFlow", "[GraphGameFlow] GraphFlowDriver.Boot: already running; ignored.");
                    return;
                }
                if (_graph == null)
                {
                    Logging.Warning("GraphGameFlow", "[GraphGameFlow] GraphFlowDriver.Boot: no graph assigned; staying inert.");
                    return;
                }

                _context = context ?? new GameFlowContext { SceneLoader = SceneLoader };
                if (_context.SceneLoader == null) _context.SceneLoader = SceneLoader;

                snapshot.ApplyTo(_context, replaceCollections: true);

                _runner?.DetachEditorProbe();   // an earlier run's probe must not shadow the new one
                _runner = new BaseRunner();
                Subscribe();
                _running = true;
                _autoAdvancePending = false;

                var nodeId = string.IsNullOrEmpty(snapshot.CurrentNodeId) ? _graph.EntryNodeId : snapshot.CurrentNodeId;
                _runner.StartFrom(_graph, nodeId, _context, registry ?? new NodeExecutorRegistry());
                DrainAutoAdvance();
            }
            finally { EndDispatch(); }
        }

        /// <summary>
        /// Boots on a CALLER-SUPPLIED context and executor registry — prepare shared state (collections,
        /// parameters, services) and register custom node executors BEFORE the flow starts. A null
        /// <paramref name="context"/> falls back to a fresh graph-initialised one; a null
        /// <paramref name="registry"/> to an empty one. A supplied context is used as-is (it is NOT
        /// re-initialised from the graph, so seeded values survive); its <see cref="GameFlowContext.SceneLoader"/>
        /// is filled with the driver's only when it is null. The same boot guards apply.
        /// </summary>
        public void Boot(GameFlowContext context, NodeExecutorRegistry registry)
        {
            if (_dispatchDepth > 0) { _pendingBoot = () => Boot(context, registry); return; }
            BeginDispatch();
            try { BootInternal(context, registry); }
            finally { EndDispatch(); }
        }

        private void BootInternal(GameFlowContext context, NodeExecutorRegistry registry)
        {
            if (_running)
            {
                Logging.Warning("GraphGameFlow", "[GraphGameFlow] GraphFlowDriver.Boot: already running; ignored.");
                return;
            }
            if (_graph == null)
            {
                Logging.Warning("GraphGameFlow", "[GraphGameFlow] GraphFlowDriver.Boot: no graph assigned; staying inert.");
                return;
            }
            if (!HasValidStart(_graph))
            {
                Logging.Warning("GraphGameFlow", "[GraphGameFlow] GraphFlowDriver.Boot: graph has no valid start node (check EntryNodeId); staying inert.");
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

            _runner?.DetachEditorProbe();   // an earlier run's probe must not shadow the new one
            _runner = new BaseRunner();
            Subscribe();
            _running = true;
            _autoAdvancePending = false;
            _runner.Start(_graph, _context, registry ?? new NodeExecutorRegistry());
            DrainAutoAdvance();
        }

        /// <summary>Forwards <paramref name="deltaSeconds"/> of elapsed time to the runner. dt ≤ 0 is ignored; so is the whole call while <see cref="Paused"/>.</summary>
        public void Tick(float deltaSeconds)
        {
            if (!_running || Paused || deltaSeconds <= 0f) return;
            BeginDispatch();
            try
            {
                if (_runner.State == RunnerState.WaitingForTime) _waitElapsed += deltaSeconds;
                _runner.Tick(deltaSeconds);
                DrainAutoAdvance();
            }
            finally { EndDispatch(); }
        }

        /// <summary>Advances the flow (manual advance, or programmatic). No-op when not running.</summary>
        public void Advance()
        {
            if (!_running) return;
            BeginDispatch();
            try
            {
                _runner.Proceed();
                DrainAutoAdvance();
            }
            finally { EndDispatch(); }
        }

        /// <summary>
        /// Selects a choice branch by its id (or port name) on the running flow — the deliberate pick a
        /// <see cref="GraphCore.ChoiceNodeData"/> waits for (it is not auto-advanced even under
        /// <see cref="AutoAdvance"/>). No-op when not running. Mirrors <see cref="Advance"/>.
        /// </summary>
        public void ChooseById(string id)
        {
            if (!_running) return;
            BeginDispatch();
            try
            {
                _runner.ChooseById(id);
                DrainAutoAdvance();
            }
            finally { EndDispatch(); }
        }

        /// <summary>Raises a named signal into the running flow, resuming a matching await. No-op when not running.</summary>
        public void RaiseSignal(string name)
        {
            if (!_running) return;
            BeginDispatch();
            try
            {
                _runner.RaiseSignal(name);
                DrainAutoAdvance();
            }
            finally { EndDispatch(); }
        }

        /// <summary>As <see cref="RaiseSignal(string)"/>, carrying a scalar payload.</summary>
        public void RaiseSignal<T>(string name, T payload)
        {
            if (!_running) return;
            BeginDispatch();
            try
            {
                _runner.RaiseSignal<T>(name, payload);
                DrainAutoAdvance();
            }
            finally { EndDispatch(); }
        }

        /// <summary>
        /// Detaches the driver from the runner and stops it running, so no further runner callback reaches
        /// this driver. Called automatically by <c>OnDestroy</c>; also callable to halt a flow explicitly.
        /// Also unregisters the runner's editor live-cursor probe so the dead run stops painting the graph
        /// editor.
        /// </summary>
        public void Stop()
        {
            Unsubscribe();
            _runner?.DetachEditorProbe();
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

            // Loader-agnostic (Unity's own scene events, not the ISceneLoader in use) so the context's
            // scene registry stays accurate whether a scene loaded through UnitySceneLoader, AsyncSceneLoader,
            // AddressablesSceneLoader, or code entirely outside the flow. Tied to THIS subscribe/unsubscribe
            // pair (not to the context's lifetime — GameFlowContext has no dispose hook) so a non-persistent
            // driver never leaks a static-event subscription past its own OnDestroy.
            SceneManager.sceneLoaded   += HandleSceneLoaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            SeedLoadedScenes();
        }

        private void Unsubscribe()
        {
            SceneManager.sceneLoaded   -= HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;

            if (_runner == null) return;
            _runner.OnNodeEntered      -= HandleNodeEntered;
            _runner.OnNodeCompleted    -= HandleNodeCompleted;
            _runner.OnEnded            -= HandleEnded;
            _runner.OnStuck            -= HandleStuck;
            _runner.OnWaitingForSignal -= HandleWaitingForSignal;
            _runner.OnWaitingForTime   -= HandleWaitingForTime;
        }

        // Seeds the registry with whatever is already loaded at Boot() time, so IsSceneLoaded is accurate
        // immediately — not just for scene changes that happen after this driver started.
        private void SeedLoadedScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded) _context?.MarkSceneLoaded(scene.name);
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => _context?.MarkSceneLoaded(scene.name);
        private void HandleSceneUnloaded(Scene scene) => _context?.MarkSceneUnloaded(scene.name);

        private void HandleNodeEntered(BaseNodeData node) => OnNodeEntered?.Invoke(node);

        private void HandleNodeCompleted(BaseNodeData node)
        {
            OnNodeCompleted?.Invoke(node);
            // A choice requires a deliberate pick (ChooseById); never auto-resolve it by first-passing-edge.
            // Only a FLAG is set here — the actual Proceed() happens in DrainAutoAdvance's loop, called by
            // every public entry point after the runner call that might trigger this handler. Calling
            // Proceed() directly from inside this event handler would recurse (Proceed → EnterCurrentNode →
            // OnNodeCompleted → this handler → Proceed → …), and a cycle with no pause node on it would
            // recurse until the native call stack overflows — an uncatchable, unrecoverable editor/player
            // crash. The iterative pump below turns that into a bounded loop instead.
            if (_autoAdvance && !(node is ChoiceNodeData)) _autoAdvancePending = true;
        }

        // Iteratively drains auto-advance requests queued by HandleNodeCompleted. Call once after any
        // top-level runner call that can complete a node (Start/StartFrom/Proceed/ChooseById/RaiseSignal/
        // Tick). Flat call stack regardless of how many pass-through nodes chain in one pass; a genuine
        // cycle with no pause node stops at MaxAutoAdvanceSteps with a warning instead of a stack overflow.
        private void DrainAutoAdvance()
        {
            int guard = 0;
            while (_autoAdvancePending && _running && guard++ < MaxAutoAdvanceSteps)
            {
                _autoAdvancePending = false;
                _runner.Proceed();
            }
            if (_autoAdvancePending && guard >= MaxAutoAdvanceSteps)
            {
                _autoAdvancePending = false;
                Logging.Warning("GraphGameFlow", 
                    $"[GraphGameFlow] Auto-advance exceeded {MaxAutoAdvanceSteps} steps in one pass — likely " +
                    "a cycle with no pause node (no await-signal, timed wait, choice, or end anywhere on the " +
                    "loop). Stopping here instead of advancing forever. Add a pause point on the cycle, or " +
                    "disable AutoAdvance and drive this stretch of the flow manually.");
            }
        }

        private void HandleEnded(EndReason reason)
        {
            // Detach BEFORE the flow-ended fanout, not just in Stop()/OnDestroy: Subscribe() runs again on
            // every subsequent Boot(), and without this the driver's handlers (plus the static SceneManager
            // subscriptions) would pile up on the dead runner every time the flow ends and reboots, instead of
            // being replaced by the next run's subscription.
            Unsubscribe();
            _running = false;
            OnEnded?.Invoke(reason);
        }

        // Marks a top-level dispatch (Boot/Tick/Advance/ChooseById/RaiseSignal) as in flight. A reentrant
        // Boot() call made from inside a driver event (typically OnEnded, rebooting into the next flow) is
        // queued in _pendingBoot instead of running immediately, and replayed here once the outermost
        // dispatch has fully unwound — so it can never reassign _runner/_context/_running out from under a
        // call still using them.
        private void BeginDispatch() => _dispatchDepth++;

        private void EndDispatch()
        {
            _dispatchDepth--;
            if (_dispatchDepth == 0 && _pendingBoot != null)
            {
                var boot = _pendingBoot;
                _pendingBoot = null;
                boot();
            }
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
