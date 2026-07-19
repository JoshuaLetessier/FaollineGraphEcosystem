using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Faolline.GraphCore;

namespace Faolline.GraphGameFlow
{
    /// <summary>
    /// Drop-in <see cref="ISceneLoader"/>/<see cref="ISceneUnloader"/> that loads through
    /// <c>SceneManager.LoadSceneAsync</c> instead of <see cref="UnitySceneLoader"/>'s blocking call, and
    /// raises progress/lifecycle events a loading-screen UI can subscribe to. The lib owns no visuals (a
    /// consumer's UI is its own territory) — this component is only the mechanism: progress reporting plus
    /// an activation gate a screen can hold open past 100% (e.g. for a fade-out or a "press to continue"
    /// beat).
    /// <para>
    /// Requests are QUEUED, not dropped: a <see cref="LoadScene"/> or <see cref="UnloadScene"/> issued while
    /// another operation is in flight is appended to a FIFO queue and processed in order — the contract an
    /// additive flow needs when a graph chains several scene operations in one auto-advance pass.
    /// </para>
    /// <para>
    /// Assign an instance to <see cref="GraphFlowDriver.SceneLoader"/> (or a <see cref="GameFlowContext"/>
    /// directly) in place of the default <see cref="UnitySceneLoader"/>; <see cref="LoadSceneAction"/> and
    /// <see cref="UnloadSceneAction"/> need no changes, since they only depend on the seam interfaces.
    /// </para>
    /// <para>
    /// To let a flow WAIT for a scene operation (instead of running ahead of it), set
    /// <see cref="LoadCompletedSignal"/>/<see cref="UnloadCompletedSignal"/>: each completion is raised as
    /// that signal into <see cref="SignalDriver"/> (falling back to <see cref="GraphFlowDriver.Active"/>)
    /// with the scene name as string payload — park the graph on an await-signal node right after the
    /// load/unload action and it resumes exactly when the operation lands. No manual event wiring needed.
    /// </para>
    /// <para>
    /// Persists across the load by default (<see cref="DontDestroyOnLoad"/>): a Single-mode load unloads the
    /// scene it is dropped into, which would otherwise kill the coroutine mid-load.
    /// </para>
    /// </summary>
    [HelpURL("https://github.com/JoshuaLetessier/FaollineGraphEcosystem/blob/master/com.faolline.graphgameflow/README.md")]
    public class AsyncSceneLoader : MonoBehaviour, ISceneLoader, ISceneUnloader
    {
        [SerializeField, Tooltip("When enabled (default), each scene activates as soon as it reaches 100% (plus the minimum display duration). Disable to hold activation open until ActivateReadyScene() is called (e.g. after a loading-screen fade-out).")]
        private bool  _autoActivate = true;
        [SerializeField, Tooltip("Minimum time (unscaled seconds) a load is reported as in-progress before SceneLoadReady fires, even if the scene finishes sooner. Avoids a loading screen that flashes for one frame.")]
        private float _minimumDisplayDuration = 0f;
        [SerializeField, Tooltip("When enabled (default), this GameObject survives scene loads (DontDestroyOnLoad) so its coroutine isn't destroyed mid-load by a Single-mode transition.")]
        private bool  _persistAcrossLoad = true;

        [Header("Flow sync (optional)")]
        [SerializeField, Tooltip("Optional signal raised into the target driver each time a LOAD completes (scene name as string payload). Park the flow on an await-signal node after a LoadSceneAction to synchronise it with the load.")]
        private SignalDef _loadCompletedSignal;
        [SerializeField, Tooltip("Optional signal raised into the target driver each time an UNLOAD completes (scene name as string payload).")]
        private SignalDef _unloadCompletedSignal;
        [SerializeField, Tooltip("The driver that receives the completion signals. When null, falls back to GraphFlowDriver.Active (the persistent singleton).")]
        private GraphFlowDriver _signalDriver;

        private struct Request
        {
            public string        Scene;
            public LoadSceneMode Mode;
            public bool          IsUnload;
        }

        private readonly Queue<Request> _queue = new Queue<Request>();
        private bool           _pumpRunning;
        private AsyncOperation _pendingOperation;
        private string         _pendingScene;

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

        /// <summary>True from the moment an operation starts until the whole queue has drained.</summary>
        public bool IsLoading => _pumpRunning;

        /// <summary>Number of requests waiting behind the one in flight.</summary>
        public int PendingCount => _queue.Count;

        /// <summary>Raised once per load, when it begins.</summary>
        public event Action<string> SceneLoadStarted;

        /// <summary>Raised every frame while a load is in flight, with progress normalised to 0..1 (Unity caps the underlying operation at 0.9 pre-activation).</summary>
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

        /// <inheritdoc />
        public void LoadScene(string sceneName, LoadSceneMode mode)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[GraphGameFlow] AsyncSceneLoader.LoadScene called with a null or empty scene name; ignored.");
                return;
            }

            Enqueue(new Request { Scene = sceneName, Mode = mode, IsUnload = false });
        }

        /// <inheritdoc />
        public void UnloadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[GraphGameFlow] AsyncSceneLoader.UnloadScene called with a null or empty scene name; ignored.");
                return;
            }

            Enqueue(new Request { Scene = sceneName, IsUnload = true });
        }

        /// <summary>
        /// Activates the scene held ready by a load started with <see cref="AutoActivate"/> false. No-op (with
        /// a warning) if no scene is currently waiting on activation.
        /// </summary>
        public void ActivateReadyScene()
        {
            if (_pendingOperation == null)
            {
                Debug.LogWarning("[GraphGameFlow] AsyncSceneLoader.ActivateReadyScene called with no scene ready to activate; ignored.");
                return;
            }

            _pendingOperation.allowSceneActivation = true;
        }

        /// <summary>
        /// Seam for tests: starts the async load. Defaults to <c>SceneManager.LoadSceneAsync</c>, guarded by
        /// <c>Application.CanStreamedLevelBeLoaded</c> (returns null and logs on a scene not in Build
        /// Settings / Addressables). A test override loading by editor path — like
        /// <c>CrossSceneSurvivalTests.EditorPathSceneLoader</c> — bypasses that guard entirely, since it never
        /// goes through Build Settings.
        /// </summary>
        protected virtual AsyncOperation BeginLoad(string sceneName, LoadSceneMode mode)
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError(
                    $"[GraphGameFlow] Scene '{sceneName}' cannot be loaded (not in Build Settings / Addressables); ignored.");
                return null;
            }

            return SceneManager.LoadSceneAsync(sceneName, mode);
        }

        /// <summary>
        /// Seam for tests: starts the async unload. Defaults to <c>SceneManager.UnloadSceneAsync</c>, guarded
        /// on "the scene is actually loaded" and "it is not the last one" (Unity cannot unload the last
        /// loaded scene) — both report a graceful <c>[GraphGameFlow]</c> error instead of throwing.
        /// </summary>
        protected virtual AsyncOperation BeginUnload(string sceneName)
        {
            if (!SceneManager.GetSceneByName(sceneName).isLoaded)
            {
                Debug.LogError($"[GraphGameFlow] Scene '{sceneName}' is not loaded; unload ignored.");
                return null;
            }

            if (SceneManager.sceneCount <= 1)
            {
                Debug.LogError(
                    $"[GraphGameFlow] Scene '{sceneName}' is the last loaded scene; Unity cannot unload it. Ignored.");
                return null;
            }

            return SceneManager.UnloadSceneAsync(sceneName);
        }

        private void Enqueue(Request request)
        {
            _queue.Enqueue(request);
            if (!_pumpRunning)
            {
                _pumpRunning = true;
                StartCoroutine(PumpRoutine());
            }
        }

        // Serial pump: one operation in flight at a time, strictly FIFO. Unity itself serialises scene
        // operations internally, so parallelising here would buy nothing — but queueing (vs the old
        // drop-with-warning) is what makes chained additive load/unload actions in one graph pass reliable.
        private IEnumerator PumpRoutine()
        {
            while (_queue.Count > 0)
            {
                var request = _queue.Dequeue();
                yield return request.IsUnload ? UnloadRoutine(request.Scene) : LoadRoutine(request.Scene, request.Mode);
            }
            _pumpRunning = false;
        }

        private IEnumerator LoadRoutine(string sceneName, LoadSceneMode mode)
        {
            var op = BeginLoad(sceneName, mode);
            if (op == null) yield break;

            _pendingScene = sceneName;
            SceneLoadStarted?.Invoke(sceneName);
            op.allowSceneActivation = false;

            while (op.progress < 0.9f)
            {
                SceneLoadProgress?.Invoke(sceneName, op.progress / 0.9f);
                yield return null;
            }
            SceneLoadProgress?.Invoke(sceneName, 1f);

            if (_minimumDisplayDuration > 0f)
                yield return new WaitForSecondsRealtime(_minimumDisplayDuration);

            SceneLoadReady?.Invoke(sceneName);

            if (_autoActivate)
            {
                op.allowSceneActivation = true;
            }
            else
            {
                _pendingOperation = op;
                yield return new WaitUntil(() => op.allowSceneActivation);
            }

            while (!op.isDone)
                yield return null;

            _pendingOperation = null;
            _pendingScene = null;
            SceneLoadCompleted?.Invoke(sceneName);
            RaiseCompletionSignal(_loadCompletedSignal, sceneName);
        }

        private IEnumerator UnloadRoutine(string sceneName)
        {
            var op = BeginUnload(sceneName);
            if (op == null) yield break;

            SceneUnloadStarted?.Invoke(sceneName);

            while (!op.isDone)
                yield return null;

            SceneUnloadCompleted?.Invoke(sceneName);
            RaiseCompletionSignal(_unloadCompletedSignal, sceneName);
        }

        // Goes through the DRIVER (not the context) so the parked await resumes AND the auto-advance pump
        // drains in the same call — the exact path scene code uses via GraphFlowDriver.RaiseSignal.
        private void RaiseCompletionSignal(SignalDef signal, string sceneName)
        {
            if (signal == null) return;

            var name = (string)signal;
            if (string.IsNullOrEmpty(name)) return;

            var driver = _signalDriver != null ? _signalDriver : GraphFlowDriver.Active;
            if (driver == null)
            {
                Debug.LogWarning(
                    $"[GraphGameFlow] AsyncSceneLoader: completion signal configured but no target driver " +
                    $"(SignalDriver unset and GraphFlowDriver.Active is null); signal for '{sceneName}' dropped.");
                return;
            }

            driver.RaiseSignal(name, sceneName);
        }
    }
}
