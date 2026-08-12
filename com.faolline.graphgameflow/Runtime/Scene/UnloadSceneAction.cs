using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphLogging;


namespace Faolline.GraphGameFlow
{
    /// <summary>
    /// Unloads an ADDITIVELY loaded Unity scene when it runs — the missing other half of
    /// <see cref="LoadSceneAction"/> for additive scene systems (hub + streamed zones, overlays). Like its
    /// load counterpart it is a graphcore <see cref="BaseAction"/>, NOT a node type: attach it to any node's
    /// enter or exit list. It resolves the active <see cref="ISceneLoader"/> from the running
    /// <see cref="GameFlowContext"/> and unloads through it when the loader also implements
    /// <see cref="ISceneUnloader"/>; a loader without unload support (a consumer-written
    /// <see cref="ISceneLoader"/>-only implementation) falls back to a default
    /// <see cref="UnitySceneLoader"/> with a warning, so the flow keeps running either way.
    /// </summary>
    [CreateAssetMenu(menuName = "GraphGameFlow/Actions/Unload Scene", fileName = "NewUnloadSceneAction")]
    public sealed class UnloadSceneAction : BaseAction
    {
        private static readonly ISceneUnloader DefaultUnloader = new UnitySceneLoader();

        [SerializeField, Tooltip("Name of the loaded (additive) scene to unload. The custom editor provides a dropdown picker.")]
        private string _sceneName;

        /// <summary>The loaded scene to unload, by name.</summary>
        public string SceneName { get => _sceneName; set => _sceneName = value; }

        /// <inheritdoc />
        public override void Execute(BaseContext context)
        {
            if (string.IsNullOrEmpty(_sceneName))
            {
                Logging.Error("GraphGameFlow", "[GraphGameFlow] UnloadSceneAction has an empty scene name; ignored.");
                return;
            }

            var loader = (context as GameFlowContext)?.SceneLoader;
            if (loader != null && !(loader is ISceneUnloader))
                Logging.Warning("GraphGameFlow", $"[GraphGameFlow] UnloadSceneAction: the context's scene loader ({loader.GetType().Name}) " +
                    "does not implement ISceneUnloader; falling back to the default UnitySceneLoader unload.");

            ((loader as ISceneUnloader) ?? DefaultUnloader).UnloadScene(_sceneName);
        }
    }
}
