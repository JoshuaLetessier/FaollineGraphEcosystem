using UnityEngine;
using UnityEngine.SceneManagement;
using Faolline.GraphCore;

namespace Faolline.GraphGameFlow
{
    /// <summary>
    /// Loads a Unity scene when it runs. This is a graphcore <see cref="BaseAction"/>, NOT a dedicated node
    /// type — attach it to ANY node's <see cref="BaseNodeData.OnEnterActions"/> or
    /// <see cref="BaseNodeData.OnExitActions"/> (a statement, a choice, a subgraph node…), exactly like any
    /// other action. It resolves the active <see cref="ISceneLoader"/> from the running
    /// <see cref="GameFlowContext"/>, falling back to a default <see cref="UnitySceneLoader"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "GraphGameFlow/Actions/Load Scene", fileName = "NewLoadSceneAction")]
    public sealed class LoadSceneAction : BaseAction
    {
        private static readonly ISceneLoader DefaultLoader = new UnitySceneLoader();

        [SerializeField, Tooltip("Target scene name (must be in Build Settings). The custom editor provides a dropdown picker.")]
        private string        _sceneName;
        [SerializeField, Tooltip("Single replaces the current scene(s). Additive loads on top of existing ones.")]
        private LoadSceneMode _mode = LoadSceneMode.Single;
        [SerializeField, Tooltip("Additive only: once the scene finishes loading, make it the ACTIVE scene (its lighting/fog settings apply; new objects parent into it). A Single load is made active by Unity automatically, so the flag is ignored there.")]
        private bool          _setActiveOnLoad;

        /// <summary>Target scene, by name (must be in Build Settings).</summary>
        public string SceneName { get => _sceneName; set => _sceneName = value; }

        /// <summary>Load mode: <see cref="LoadSceneMode.Single"/> (replace) or <see cref="LoadSceneMode.Additive"/>.</summary>
        public LoadSceneMode Mode { get => _mode; set => _mode = value; }

        /// <summary>Additive only: make the scene the active scene once it finishes loading (ignored for Single, which Unity activates itself).</summary>
        public bool SetActiveOnLoad { get => _setActiveOnLoad; set => _setActiveOnLoad = value; }

        /// <inheritdoc />
        public override void Execute(BaseContext context)
        {
            if (string.IsNullOrEmpty(_sceneName))
            {
                Debug.LogError("[GraphGameFlow] LoadSceneAction has an empty scene name; ignored.");
                return;
            }

            if (_setActiveOnLoad && _mode == LoadSceneMode.Additive)
                RequestSetActiveWhenLoaded(_sceneName);

            var loader = (context as GameFlowContext)?.SceneLoader ?? DefaultLoader;
            loader.LoadScene(_sceneName, _mode);
        }

        // Loader-agnostic by design: activation must wait until the scene has actually finished loading
        // (even the "blocking" SceneManager.LoadScene completes on the next frame, and SetActiveScene
        // rejects a scene that is not fully loaded), so a one-shot SceneManager.sceneLoaded handler does it
        // regardless of which ISceneLoader runs the load — including a consumer-written one. If the load is
        // gracefully skipped (bad name / not in Build Settings) the handler stays parked, matching by name,
        // until such a scene loads or the domain reloads: inert either way.
        private static void RequestSetActiveWhenLoaded(string sceneName)
        {
            void Handler(Scene scene, LoadSceneMode mode)
            {
                if (scene.name != sceneName) return;
                SceneManager.sceneLoaded -= Handler;
                SceneManager.SetActiveScene(scene);
            }
            SceneManager.sceneLoaded += Handler;
        }
    }
}
