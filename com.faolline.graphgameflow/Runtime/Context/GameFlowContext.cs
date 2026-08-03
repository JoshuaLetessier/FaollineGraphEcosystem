using System.Collections.Generic;
using Faolline.GraphCore;

namespace Faolline.GraphGameFlow
{
    /// <summary>
    /// Typed <see cref="BaseContext"/> for the host layer (Constitution VI). For slice 1 it carries the
    /// active <see cref="ISceneLoader"/> — a runtime service, not a bool/int/float/string parameter, so it
    /// lives as a field rather than a context parameter. This is the single shared blackboard the driver
    /// owns and that later slices (Reactive progression, Flow abilities) will extend.
    /// </summary>
    public class GameFlowContext : BaseContext
    {
        private readonly HashSet<string> _loadedScenes = new HashSet<string>();

        /// <summary>
        /// The scene loader the <see cref="LoadSceneAction"/> uses. The driver sets this at boot (defaulting
        /// to a <see cref="UnitySceneLoader"/>); tests inject a recording stub.
        /// </summary>
        public ISceneLoader SceneLoader { get; set; }

        /// <summary>
        /// Resolves a <c>BaseGraph.GraphId</c> to its asset — the seam a multi-root-graph project (or
        /// <c>graphsave</c>'s restore path) uses instead of a hand-maintained lookup table. Defaults to
        /// <c>null</c>; a project with a single root graph never needs to set it.
        /// </summary>
        public IGraphCatalog GraphCatalog { get; set; }

        /// <summary>
        /// Set by an early-preload action (e.g. <c>PreloadNextChapterAction</c> in the Addressables adapter)
        /// once its target graph resolves. The host reads this after <c>OnEnded</c> to reboot the driver onto
        /// the already-loaded next chapter with no additional wait. <c>null</c> until a preload completes;
        /// a project that never triggers a preload never touches this.
        /// </summary>
        public BaseGraph PendingNextGraph { get; set; }

        /// <summary>
        /// Scenes currently loaded, by Unity scene name. <see cref="GraphFlowDriver"/> keeps this in sync
        /// with <c>SceneManager.sceneLoaded</c>/<c>sceneUnloaded</c> — accurate regardless of which
        /// <see cref="ISceneLoader"/> did the loading (or whether anything outside the flow loaded a scene).
        /// Query it from a custom <c>BaseCondition</c>/<c>BaseAction</c> instead of importing
        /// <c>UnityEngine.SceneManagement</c> directly, keeping graph logic uniformly context-based.
        /// </summary>
        public IReadOnlyCollection<string> LoadedScenes => _loadedScenes;

        /// <summary>True if <paramref name="sceneName"/> is currently tracked as loaded.</summary>
        public bool IsSceneLoaded(string sceneName)
            => !string.IsNullOrEmpty(sceneName) && _loadedScenes.Contains(sceneName);

        /// <summary>
        /// Marks <paramref name="sceneName"/> loaded. Called by <see cref="GraphFlowDriver"/> in response to
        /// Unity's own scene events — not meant to be called directly by graph/scene code (load scenes via
        /// <see cref="LoadSceneAction"/> or your <see cref="ISceneLoader"/> instead; the registry follows).
        /// </summary>
        public void MarkSceneLoaded(string sceneName)
        {
            if (!string.IsNullOrEmpty(sceneName)) _loadedScenes.Add(sceneName);
        }

        /// <summary>Marks <paramref name="sceneName"/> unloaded. See <see cref="MarkSceneLoaded"/>.</summary>
        public void MarkSceneUnloaded(string sceneName)
        {
            if (!string.IsNullOrEmpty(sceneName)) _loadedScenes.Remove(sceneName);
        }

        /// <inheritdoc />
        protected override BaseContext CreateCloneInstance() => new GameFlowContext();

        /// <inheritdoc />
        public override BaseContext DeepClone()
        {
            var clone = (GameFlowContext)base.DeepClone();
            clone.SceneLoader = SceneLoader; // a shared service reference, not per-snapshot state
            clone.GraphCatalog = GraphCatalog; // same treatment — a shared service, not per-snapshot state
            clone.PendingNextGraph = PendingNextGraph;
            foreach (var scene in _loadedScenes) clone._loadedScenes.Add(scene);
            return clone;
        }
    }
}
