using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using Faolline.GraphCore;
using Faolline.GraphGameFlow;
using Faolline.GraphLogging;


namespace Faolline.GraphGameFlow.Addressables
{
    /// <summary>
    /// Drop-in <see cref="ISceneLoader"/>/<see cref="ISceneUnloader"/> that loads through
    /// <c>UnityEngine.AddressableAssets.Addressables</c> instead of a Build Settings entry — a scene reached
    /// by <see cref="LoadSceneAction"/>/<see cref="UnloadSceneAction"/> only needs an Addressable key
    /// (address, label, or GUID), never a Build Settings registration. It mirrors
    /// <see cref="AsyncSceneLoader"/>'s whole contract (queue, progress events, activation gate, completion
    /// signals, driver pause) so it is a drop-in swap wherever an async loader is wanted — the two are not
    /// related by inheritance because <c>AsyncOperation</c> (SceneManager) and
    /// <c>AsyncOperationHandle&lt;SceneInstance&gt;</c> (Addressables) are unrelated engine types.
    /// <para>
    /// <b>The scene name passed to <see cref="LoadScene"/>/<see cref="UnloadScene"/> is the Addressable
    /// key</b> (typically the address you gave the entry) — it does not have to match the Scene asset's
    /// file name, and the scene does NOT need a Build Settings entry. <see cref="LoadSceneActionEditor"/>'s
    /// "not in Build Settings" warning is therefore a false positive for this loader; ignore it.
    /// </para>
    /// <para>
    /// Unloading an Addressables scene needs the load's own <see cref="AsyncOperationHandle{TObject}"/>
    /// (not just its name) — internally this loader keeps a key→handle map for every scene it loaded, so
    /// <see cref="UnloadScene"/> only works on a scene THIS loader instance loaded (an unrecognised key logs
    /// a graceful <c>[GraphGameFlow]</c> error, exactly like the other loaders on a bad request).
    /// </para>
    /// <para>
    /// A load/unload that fails (bad key, a content build gap, unloading the last scene…) does NOT raise its
    /// completion signal — a node awaiting only that signal would park forever. Set
    /// <see cref="LoadFailedSignal"/>/<see cref="UnloadFailedSignal"/> and add them as a SECOND name on the
    /// same await-signal node (<c>AwaitSignalNames</c> waits on any one of several, logical OR) so a failure
    /// resumes the flow too. The failure signal's string payload is <c>"{key}: {reason}"</c> — naming both
    /// what failed and why in one glance, on top of the <c>[GraphGameFlow]</c> error already logged.
    /// </para>
    /// <para>
    /// <see cref="StuckOperationWarningAfter"/> logs a loud warning (and raises
    /// <see cref="OperationTakingTooLong"/>) if a single load/unload has been in flight unusually long — a
    /// hung request (one that never resolves to success or failure at all) is otherwise silent beyond the
    /// graph parking on its await signal. Diagnostic only; it never cancels or alters the flow.
    /// </para>
    /// </summary>
    [HelpURL("https://github.com/JoshuaLetessier/FaollineGraphEcosystem/blob/master/com.faolline.graphgameflow.addressables/README.md")]
    public class AddressablesSceneLoader : MonoBehaviour, ISceneLoader, ISceneUnloader
    {
        [SerializeField, Tooltip("When enabled (default), each scene activates as soon as it reaches 100%. Disable to hold activation open until ActivateReadyScene() is called (e.g. after a loading-screen fade-out).")]
        private bool  _autoActivate = true;
        [SerializeField, Tooltip("Minimum time (unscaled seconds) a load is reported as in-progress before SceneLoadReady fires, even if the scene finishes sooner. Avoids a loading screen that flashes for one frame.")]
        private float _minimumDisplayDuration = 0f;
        [SerializeField, Tooltip("When enabled (default), this GameObject survives scene loads (DontDestroyOnLoad) so its coroutine isn't destroyed mid-load by a Single-mode transition.")]
        private bool  _persistAcrossLoad = true;

        [Header("Flow sync (optional)")]
        [SerializeField, Tooltip("Optional signal raised into the target driver each time a LOAD completes (scene key as string payload). Park the flow on an await-signal node after a LoadSceneAction to synchronise it with the load.")]
        private SignalDef _loadCompletedSignal;
        [SerializeField, Tooltip("Optional signal raised into the target driver each time an UNLOAD completes (scene key as string payload).")]
        private SignalDef _unloadCompletedSignal;
        [SerializeField, Tooltip("Optional signal raised into the target driver when a LOAD fails (payload: \"{key}: {reason}\"). Add as a second AwaitSignalNames entry alongside LoadCompletedSignal so a failure resumes the flow instead of stalling it forever.")]
        private SignalDef _loadFailedSignal;
        [SerializeField, Tooltip("Optional signal raised into the target driver when an UNLOAD fails (payload: \"{key}: {reason}\"). Add as a second AwaitSignalNames entry alongside UnloadCompletedSignal.")]
        private SignalDef _unloadFailedSignal;
        [SerializeField, Tooltip("The driver that receives the completion/failure signals. When null, falls back to GraphFlowDriver.Active (the persistent singleton).")]
        private GraphFlowDriver _signalDriver;
        [SerializeField, Tooltip("When enabled, the target driver (SignalDriver, else GraphFlowDriver.Active) is Paused while the queue is busy, so timed waits don't tick down behind a loading screen. A driver the consumer already paused is left untouched.")]
        private bool _pauseDriverWhileLoading = false;
        [SerializeField, Tooltip("Logs a warning (and raises OperationTakingTooLong) if a single load/unload has been in flight longer than this many real seconds — a hung operation is otherwise silent beyond the graph parking on its await signal. Diagnostic only; never changes flow. 0 or less disables it.")]
        private float _stuckOperationWarningAfter = 15f;

        private struct Request
        {
            public string        Key;
            public LoadSceneMode Mode;
            public bool          IsUnload;
        }

        private readonly Queue<Request> _queue = new Queue<Request>();
        // Every scene loaded BY THIS INSTANCE, key -> handle: Addressables.UnloadSceneAsync needs the load's
        // own handle, not just the scene name (unlike SceneManager.UnloadSceneAsync).
        private readonly Dictionary<string, AsyncOperationHandle<SceneInstance>> _loaded =
            new Dictionary<string, AsyncOperationHandle<SceneInstance>>();

        private bool                                  _pumpRunning;
        private AsyncOperationHandle<SceneInstance>?   _pendingHandle;        // held-ready load awaiting ActivateReadyScene()
        private bool                                   _activationRequested;  // flag flipped by ActivateReadyScene(), read once per frame — never call ActivateAsync() from inside a WaitUntil predicate (it would fire on every poll)
        private GraphFlowDriver                        _pausedDriver;         // the driver WE paused (never one the consumer paused)

        /// <summary>When true, each scene activates automatically once ready (see <see cref="ActivateReadyScene"/>).</summary>
        public bool AutoActivate { get => _autoActivate; set => _autoActivate = value; }

        /// <summary>Minimum unscaled seconds a load is reported in-progress before <see cref="SceneLoadReady"/> fires.</summary>
        public float MinimumDisplayDuration { get => _minimumDisplayDuration; set => _minimumDisplayDuration = value; }

        /// <summary>Optional signal raised into <see cref="SignalDriver"/> each time a load completes.</summary>
        public SignalDef LoadCompletedSignal { get => _loadCompletedSignal; set => _loadCompletedSignal = value; }

        /// <summary>Optional signal raised into <see cref="SignalDriver"/> each time an unload completes.</summary>
        public SignalDef UnloadCompletedSignal { get => _unloadCompletedSignal; set => _unloadCompletedSignal = value; }

        /// <summary>Optional signal raised into <see cref="SignalDriver"/> when a load fails. See <see cref="SceneLoadFailed"/>.</summary>
        public SignalDef LoadFailedSignal { get => _loadFailedSignal; set => _loadFailedSignal = value; }

        /// <summary>Optional signal raised into <see cref="SignalDriver"/> when an unload fails. See <see cref="SceneUnloadFailed"/>.</summary>
        public SignalDef UnloadFailedSignal { get => _unloadFailedSignal; set => _unloadFailedSignal = value; }

        /// <summary>Receiver of the completion/failure signals; null falls back to <see cref="GraphFlowDriver.Active"/>.</summary>
        public GraphFlowDriver SignalDriver { get => _signalDriver; set => _signalDriver = value; }

        /// <summary>
        /// When true, the target driver (<see cref="SignalDriver"/>, else <see cref="GraphFlowDriver.Active"/>)
        /// is <see cref="GraphFlowDriver.Paused"/> while the queue is busy, so timed waits hold behind a
        /// loading screen instead of ticking down. A driver the consumer already paused is left untouched
        /// (and not resumed). Signals still resume awaits while paused — only the time pump stops.
        /// </summary>
        public bool PauseDriverWhileLoading { get => _pauseDriverWhileLoading; set => _pauseDriverWhileLoading = value; }

        /// <summary>
        /// Seconds a single load/unload may be in flight before a diagnostic warning fires (see
        /// <see cref="OperationTakingTooLong"/>). 0 or less disables it. Purely a visibility aid — it never
        /// alters the flow (no timeout, no auto-fail); it only makes a hung operation loud instead of silent.
        /// </summary>
        public float StuckOperationWarningAfter { get => _stuckOperationWarningAfter; set => _stuckOperationWarningAfter = value; }

        /// <summary>True from the moment an operation starts until the whole queue has drained.</summary>
        public bool IsLoading => _pumpRunning;

        /// <summary>Number of requests waiting behind the one in flight.</summary>
        public int PendingCount => _queue.Count;

        /// <summary>Raised once per load, when it begins.</summary>
        public event Action<string> SceneLoadStarted;

        /// <summary>Raised every frame while a load is in flight, with progress normalised to 0..1.</summary>
        public event Action<string, float> SceneLoadProgress;

        /// <summary>
        /// Raised once a scene is fully loaded and the minimum display duration has elapsed. If
        /// <see cref="AutoActivate"/> is false, the scene stays inactive until <see cref="ActivateReadyScene"/>
        /// is called.
        /// </summary>
        public event Action<string> SceneLoadReady;

        /// <summary>Raised once a scene has activated and its load operation is done.</summary>
        public event Action<string> SceneLoadCompleted;

        /// <summary>Raised once per unload, when it begins.</summary>
        public event Action<string> SceneUnloadStarted;

        /// <summary>Raised once a scene has finished unloading.</summary>
        public event Action<string> SceneUnloadCompleted;

        /// <summary>Raised when a load fails (key, then a human-readable reason). No completion event follows.</summary>
        public event Action<string, string> SceneLoadFailed;

        /// <summary>Raised when an unload fails (key, then a human-readable reason). No completion event follows.</summary>
        public event Action<string, string> SceneUnloadFailed;

        /// <summary>
        /// Raised at most once per operation if it has been in flight longer than
        /// <see cref="StuckOperationWarningAfter"/> (key, then elapsed real seconds). Diagnostic only: the
        /// operation is NOT cancelled and may still complete or fail normally afterward.
        /// </summary>
        public event Action<string, float> OperationTakingTooLong;

        private void Awake()
        {
            if (_persistAcrossLoad)
            {
                if (transform.parent != null) transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
            }
        }

        // Destroying the loader also kills its coroutines: a driver we paused must not stay frozen forever.
        private void OnDestroy() => ResumePausedDriver();

        /// <summary>
        /// <paramref name="sceneName"/> is the Addressable KEY (address/label/GUID) — not necessarily the
        /// Scene asset's file name — and needs no Build Settings entry.
        /// </summary>
        public void LoadScene(string sceneName, LoadSceneMode mode)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Logging.Error("GraphGameFlow", "[GraphGameFlow] AddressablesSceneLoader.LoadScene called with a null or empty key; ignored.");
                return;
            }

            Enqueue(new Request { Key = sceneName, Mode = mode, IsUnload = false });
        }

        /// <summary>Unloads a scene previously loaded by THIS instance. An unrecognised key logs a graceful error.</summary>
        public void UnloadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Logging.Error("GraphGameFlow", "[GraphGameFlow] AddressablesSceneLoader.UnloadScene called with a null or empty key; ignored.");
                return;
            }

            Enqueue(new Request { Key = sceneName, IsUnload = true });
        }

        /// <summary>
        /// Activates the scene held ready by a load started with <see cref="AutoActivate"/> false. No-op (with
        /// a warning) if no scene is currently waiting on activation.
        /// </summary>
        public void ActivateReadyScene()
        {
            if (_pendingHandle == null)
            {
                Logging.Warning("GraphGameFlow", "[GraphGameFlow] AddressablesSceneLoader.ActivateReadyScene called with no scene ready to activate; ignored.");
                return;
            }

            _activationRequested = true;
        }

        private void Enqueue(Request request)
        {
            _queue.Enqueue(request);
            if (!_pumpRunning)
            {
                _pumpRunning = true;
                PauseDriverIfConfigured();   // synchronous with the first request, before any frame elapses
                StartCoroutine(PumpRoutine());
            }
        }

        private void PauseDriverIfConfigured()
        {
            if (!_pauseDriverWhileLoading) return;
            var driver = _signalDriver != null ? _signalDriver : GraphFlowDriver.Active;
            if (driver == null || driver.Paused) return;   // absent, or the consumer's own pause: not ours to manage
            driver.Paused = true;
            _pausedDriver = driver;
        }

        private void ResumePausedDriver()
        {
            if (_pausedDriver == null) return;
            _pausedDriver.Paused = false;
            _pausedDriver = null;
        }

        // Serial pump: one operation in flight at a time, strictly FIFO — mirrors AsyncSceneLoader so chained
        // additive load/unload actions in one graph pass are just as reliable through Addressables.
        private IEnumerator PumpRoutine()
        {
            while (_queue.Count > 0)
            {
                var request = _queue.Dequeue();
                yield return request.IsUnload ? UnloadRoutine(request.Key) : LoadRoutine(request.Key, request.Mode);
            }
            _pumpRunning = false;
            ResumePausedDriver();
        }

        private IEnumerator LoadRoutine(string key, LoadSceneMode mode)
        {
            // Fully qualified: our own namespace's last segment is "Addressables" (matches the ecosystem's
            // Faolline.<Package>.<Adapter> convention), which would otherwise shadow the Unity type.
            var handle = global::UnityEngine.AddressableAssets.Addressables.LoadSceneAsync(key, mode, activateOnLoad: false);

            SceneLoadStarted?.Invoke(key);
            float startTime = Time.realtimeSinceStartup;
            bool stuckWarned = false;

            while (!handle.IsDone)
            {
                SceneLoadProgress?.Invoke(key, handle.PercentComplete);
                CheckStuck(key, startTime, ref stuckWarned);
                yield return null;
            }

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                // An invalid key can resolve (and fail) synchronously, within the same call stack as
                // LoadSceneAction.Execute() — before the runner's own (equally synchronous) auto-advance
                // chain has had a chance to reach and park on an awaiting node placed right after it. One
                // frame of delay lets that chain finish first, so the failure signal is delivered live
                // instead of needing ResumeIfSignalAlreadyRaised to recover it from history.
                yield return null;
                var reason = $"Addressables scene '{key}' failed to load: {handle.OperationException}";
                Logging.Error("GraphGameFlow", $"[GraphGameFlow] {reason}");
                SceneLoadFailed?.Invoke(key, reason);
                RaiseFailureSignal(_loadFailedSignal, key, reason);
                yield break;
            }

            SceneLoadProgress?.Invoke(key, 1f);

            if (_minimumDisplayDuration > 0f)
                yield return new WaitForSecondsRealtime(_minimumDisplayDuration);

            SceneLoadReady?.Invoke(key);

            if (_autoActivate)
            {
                yield return handle.Result.ActivateAsync();
            }
            else
            {
                _pendingHandle = handle;
                _activationRequested = false;
                while (!_activationRequested)   // ActivateReadyScene() flips the flag; ActivateAsync() itself is called exactly once, below
                {
                    CheckStuck(key, startTime, ref stuckWarned);
                    yield return null;
                }
                yield return handle.Result.ActivateAsync();
            }

            _pendingHandle = null;
            _loaded[key] = handle;
            SceneLoadCompleted?.Invoke(key);
            RaiseCompletionSignal(_loadCompletedSignal, key);
        }

        private IEnumerator UnloadRoutine(string key)
        {
            if (!_loaded.TryGetValue(key, out var handle))
            {
                yield return null;   // see the matching comment in LoadRoutine
                var reason = $"Scene '{key}' was not loaded by this AddressablesSceneLoader; unload ignored.";
                Logging.Error("GraphGameFlow", $"[GraphGameFlow] {reason}");
                SceneUnloadFailed?.Invoke(key, reason);
                RaiseFailureSignal(_unloadFailedSignal, key, reason);
                yield break;
            }

            if (SceneManager.sceneCount <= 1)
            {
                yield return null;   // see the matching comment in LoadRoutine
                var reason = $"Scene '{key}' is the last loaded scene; Unity cannot unload it.";
                Logging.Error("GraphGameFlow", $"[GraphGameFlow] {reason} Ignored.");
                SceneUnloadFailed?.Invoke(key, reason);
                RaiseFailureSignal(_unloadFailedSignal, key, reason);
                yield break;
            }

            SceneUnloadStarted?.Invoke(key);
            float startTime = Time.realtimeSinceStartup;
            bool stuckWarned = false;

            var op = global::UnityEngine.AddressableAssets.Addressables.UnloadSceneAsync(handle);
            while (!op.IsDone)
            {
                CheckStuck(key, startTime, ref stuckWarned);
                yield return null;
            }

            _loaded.Remove(key);
            SceneUnloadCompleted?.Invoke(key);
            RaiseCompletionSignal(_unloadCompletedSignal, key);
        }

        // Purely diagnostic: never changes flow behavior, only makes an abnormally slow/hung operation loud
        // instead of silent. Deliberately scoped to the operation's OWN in-flight duration (a scene load has
        // a naturally bounded expected time) rather than to how long a graph node has been parked on a
        // signal — the latter is routinely minutes for a perfectly normal "await player input" node, so a
        // generic driver-wide timeout would false-positive constantly on the ecosystem's most common
        // await-signal use case. Fires at most once per operation.
        private void CheckStuck(string key, float startTime, ref bool warned)
        {
            if (warned || _stuckOperationWarningAfter <= 0f) return;

            float elapsed = Time.realtimeSinceStartup - startTime;
            if (elapsed < _stuckOperationWarningAfter) return;

            warned = true;
            Logging.Warning("GraphGameFlow", (
                $"[GraphGameFlow] Addressables scene operation for '{key}' has been in flight for {elapsed:0.0}s " +
                $"(over the {_stuckOperationWarningAfter:0.0}s warning threshold) — it may be hung. No " +
                "automatic action is taken; this is a visibility aid only."));
            OperationTakingTooLong?.Invoke(key, elapsed);
        }

        // Goes through the DRIVER (not the context) so the parked await resumes AND the auto-advance pump
        // drains in the same call — the exact path scene code uses via GraphFlowDriver.RaiseSignal.
        private void RaiseCompletionSignal(SignalDef signal, string key)
            => RaiseSceneSignal(signal, key, key);

        /// <summary>Payload is <c>"{key}: {reason}"</c> — identifies both what failed and why in one string.</summary>
        private void RaiseFailureSignal(SignalDef signal, string key, string reason)
            => RaiseSceneSignal(signal, key, $"{key}: {reason}");

        private void RaiseSceneSignal(SignalDef signal, string key, string payload)
        {
            if (signal == null) return;

            var name = (string)signal;
            if (string.IsNullOrEmpty(name)) return;

            var driver = _signalDriver != null ? _signalDriver : GraphFlowDriver.Active;
            if (driver == null)
            {
                Logging.Warning("GraphGameFlow", (
                    $"[GraphGameFlow] AddressablesSceneLoader: signal configured but no target driver " +
                    $"(SignalDriver unset and GraphFlowDriver.Active is null); signal for '{key}' dropped."));
                return;
            }

            driver.RaiseSignal(name, payload);
        }
    }
}
