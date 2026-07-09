using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Faolline.GraphGameFlow
{
    /// <summary>
    /// Drop-in <see cref="ISceneLoader"/> that loads through <c>SceneManager.LoadSceneAsync</c> instead of
    /// <see cref="UnitySceneLoader"/>'s blocking call, and raises progress/lifecycle events a loading-screen
    /// UI can subscribe to. The lib owns no visuals (a consumer's UI is its own territory) — this component is
    /// only the mechanism: progress reporting plus an activation gate a screen can hold open past 100% (e.g.
    /// for a fade-out or a "press to continue" beat).
    /// <para>
    /// Assign an instance to <see cref="GraphFlowDriver.SceneLoader"/> (or a <see cref="GameFlowContext"/>
    /// directly) in place of the default <see cref="UnitySceneLoader"/>; <see cref="LoadSceneAction"/> needs
    /// no changes, since it only depends on <see cref="ISceneLoader"/>.
    /// </para>
    /// <para>
    /// Persists across the load by default (<see cref="DontDestroyOnLoad"/>): a Single-mode load unloads the
    /// scene it is dropped into, which would otherwise kill the coroutine mid-load.
    /// </para>
    /// </summary>
    [HelpURL("https://github.com/JoshuaLetessier/FaollineGraphEcosystem/blob/master/com.faolline.graphgameflow/README.md")]
    public class AsyncSceneLoader : MonoBehaviour, ISceneLoader
    {
        [SerializeField, Tooltip("When enabled (default), the scene activates as soon as it reaches 100% (plus the minimum display duration). Disable to hold activation open until ActivateReadyScene() is called (e.g. after a loading-screen fade-out).")]
        private bool  _autoActivate = true;
        [SerializeField, Tooltip("Minimum time (unscaled seconds) the load is reported as in-progress before SceneLoadReady fires, even if the scene finishes sooner. Avoids a loading screen that flashes for one frame.")]
        private float _minimumDisplayDuration = 0f;
        [SerializeField, Tooltip("When enabled (default), this GameObject survives the scene load (DontDestroyOnLoad) so its coroutine isn't destroyed mid-load by a Single-mode transition.")]
        private bool  _persistAcrossLoad = true;

        private AsyncOperation _pendingOperation;
        private string         _pendingScene;

        /// <summary>When true, the scene activates automatically once ready (see <see cref="ActivateReadyScene"/>).</summary>
        public bool AutoActivate { get => _autoActivate; set => _autoActivate = value; }

        /// <summary>Minimum unscaled seconds the load is reported in-progress before <see cref="SceneLoadReady"/> fires.</summary>
        public float MinimumDisplayDuration { get => _minimumDisplayDuration; set => _minimumDisplayDuration = value; }

        /// <summary>True from the moment a load starts until it completes (scene activated).</summary>
        public bool IsLoading { get; private set; }

        /// <summary>Raised once, when a load begins.</summary>
        public event Action<string> SceneLoadStarted;

        /// <summary>Raised every frame while loading, with progress normalised to 0..1 (Unity caps the underlying operation at 0.9 pre-activation).</summary>
        public event Action<string, float> SceneLoadProgress;

        /// <summary>
        /// Raised once the scene is fully loaded and the minimum display duration has elapsed. If
        /// <see cref="AutoActivate"/> is false, the scene stays inactive until <see cref="ActivateReadyScene"/>
        /// is called.
        /// </summary>
        public event Action<string> SceneLoadReady;

        /// <summary>Raised once the scene has activated and the load operation is done.</summary>
        public event Action<string> SceneLoadCompleted;

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

            if (IsLoading)
            {
                Debug.LogWarning(
                    $"[GraphGameFlow] AsyncSceneLoader.LoadScene('{sceneName}') ignored; a load of '{_pendingScene}' is already in progress.");
                return;
            }

            StartCoroutine(LoadRoutine(sceneName, mode));
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

        private IEnumerator LoadRoutine(string sceneName, LoadSceneMode mode)
        {
            IsLoading = true;
            _pendingScene = sceneName;

            var op = BeginLoad(sceneName, mode);
            if (op == null)
            {
                IsLoading = false;
                _pendingScene = null;
                yield break;
            }

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
            IsLoading = false;
            SceneLoadCompleted?.Invoke(sceneName);
        }
    }
}
