namespace Faolline.GraphGameFlow
{
    /// <summary>
    /// Optional companion seam to <see cref="ISceneLoader"/>: unloading an ADDITIVELY loaded scene. Kept as
    /// a separate interface (rather than a new member on <see cref="ISceneLoader"/>) so every existing
    /// loader implementation — including consumer-written ones — keeps compiling unchanged; a loader opts
    /// in by implementing both. The shipped loaders (<see cref="UnitySceneLoader"/>,
    /// <see cref="AsyncSceneLoader"/>) implement it.
    /// </summary>
    public interface ISceneUnloader
    {
        /// <summary>
        /// Unloads the loaded scene named <paramref name="sceneName"/>. Only meaningful for scenes stacked
        /// with <c>LoadSceneMode.Additive</c> — Unity cannot unload the last remaining scene.
        /// </summary>
        void UnloadScene(string sceneName);
    }
}
