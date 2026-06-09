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
    public sealed class LoadSceneAction : BaseAction
    {
        private static readonly ISceneLoader DefaultLoader = new UnitySceneLoader();

        [SerializeField] private string        _sceneName;
        [SerializeField] private LoadSceneMode _mode = LoadSceneMode.Single;

        /// <summary>Target scene, by name (must be in Build Settings).</summary>
        public string SceneName { get => _sceneName; set => _sceneName = value; }

        /// <summary>Load mode: <see cref="LoadSceneMode.Single"/> (replace) or <see cref="LoadSceneMode.Additive"/>.</summary>
        public LoadSceneMode Mode { get => _mode; set => _mode = value; }

        /// <inheritdoc />
        public override void Execute(BaseContext context)
        {
            if (string.IsNullOrEmpty(_sceneName))
            {
                Debug.LogError("[GraphGameFlow] LoadSceneAction has an empty scene name; ignored.");
                return;
            }

            var loader = (context as GameFlowContext)?.SceneLoader ?? DefaultLoader;
            loader.LoadScene(_sceneName, _mode);
        }
    }
}
