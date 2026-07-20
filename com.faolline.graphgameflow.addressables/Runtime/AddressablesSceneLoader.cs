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
        [SerializeField, Tooltip("The driver that receives the completion signals. When null, falls back to GraphFlowDriver.Active (the persistent singleton).")]
        private GraphFlowDriver _signalDriver;
        [SerializeField, Tooltip("When enabled, the target driver (SignalDriver, else GraphFlowDriver.Active) is Paused while the queue is busy, so timed waits don't tick down behind a loading screen. A driver the consumer already paused is left untouched.")]
        private bool _pauseDriverWhileLoading = false;

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

        /// <summary>Receiver of the completion signals; null falls back to <see cref="GraphFlowDriver.Active"/>.</summary>
        public GraphFlowDriver SignalDriver { get => _signalDriver; set => _signalDriver = value; }

        /// <summary>
        /// When true, the target driver (<see cref="SignalDriver"/>, else <see cref="GraphFlowDriver.Active"/>)
        /// is <see cref="GraphFlowDriver.Paused"/> while the queue is busy, so timed waits hold behind a
        /// loading screen instead of ticking down. A driver the consumer already paused is left untouched
        /// (and not resumed). Signals still resume awaits while paused — only the time pump stops.
        /// </summary>
        public bool PauseDriverWhileLoading { get => _pauseDriverWhileLoading; set => _pauseDriverWhileLoading = value; }

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
                Debug.LogError("[GraphGameFlow] AddressablesSceneLoader.LoadScene called with a null or empty key; ignored.");
                return;
            }

            Enqueue(new Request { Key = sceneName, Mode = mode, IsUnload = false });
        }

        /// <summary>Unloads a scene previously loaded by THIS instance. An unrecognised key logs a graceful error.</summary>
        public void UnloadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[GraphGameFlow] AddressablesSceneLoader.UnloadScene called with a null or empty key; ignored.");
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
                Debug.LogWarning("[GraphGameFlow] AddressablesSceneLoader.ActivateReadyScene called with no scene ready to activate; ignored.");
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

            while (!handle.IsDone)
            {
                SceneLoadProgress?.Invoke(key, handle.PercentComplete);
                yield return null;
            }

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError(
                    $"[GraphGameFlow] Addressables scene '{key}' failed to load: {handle.OperationException}");
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
                yield return new WaitUntil(() => _activationRequested);   // ActivateReadyScene() flips the flag; the actual ActivateAsync() call happens exactly once, below
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
                Debug.LogError(
                    $"[GraphGameFlow] Scene '{key}' was not loaded by this AddressablesSceneLoader; unload ignored.");
                yield break;
            }

            if (SceneManager.sceneCount <= 1)
            {
                Debug.LogError(
                    $"[GraphGameFlow] Scene '{key}' is the last loaded scene; Unity cannot unload it. Ignored.");
                yield break;
            }

            SceneUnloadStarted?.Invoke(key);

            var op = global::UnityEngine.AddressableAssets.Addressables.UnloadSceneAsync(handle);
            yield return op;

            _loaded.Remove(key);
            SceneUnloadCompleted?.Invoke(key);
            RaiseCompletionSignal(_unloadCompletedSignal, key);
        }

        // Goes through the DRIVER (not the context) so the parked await resumes AND the auto-advance pump
        // drains in the same call — the exact path scene code uses via GraphFlowDriver.RaiseSignal.
        private void RaiseCompletionSignal(SignalDef signal, string key)
        {
            if (signal == null) return;

            var name = (string)signal;
            if (string.IsNullOrEmpty(name)) return;

            var driver = _signalDriver != null ? _signalDriver : GraphFlowDriver.Active;
            if (driver == null)
            {
                Debug.LogWarning(
                    $"[GraphGameFlow] AddressablesSceneLoader: completion signal configured but no target driver " +
                    $"(SignalDriver unset and GraphFlowDriver.Active is null); signal for '{key}' dropped.");
                return;
            }

            driver.RaiseSignal(name, key);
        }
    }
}
