using System.Collections.Generic;

namespace Faolline.GraphGameFlow.Editor
{
    /// <summary>
    /// Optional extra source of scene identifiers for the <see cref="LoadSceneActionEditor"/>/
    /// <see cref="UnloadSceneActionEditor"/> "scene name" field, beyond the always-available Build Settings
    /// dropdown — e.g. registered Addressable scene addresses when Addressables is installed. graphgameflow
    /// core never references any concrete source package; an adapter package (like
    /// <c>com.faolline.graphgameflow.addressables</c>) registers one with <see cref="SceneKeySourceRegistry"/>,
    /// typically from an <c>[InitializeOnLoadMethod]</c> hook. Mirrors graphcore's
    /// <c>ContextKeyLabelRegistry</c>/<c>IContextLabelResolver</c> seam.
    /// </summary>
    public interface ISceneKeySourceProvider
    {
        /// <summary>Toolbar label for this source, e.g. "Addressable".</summary>
        string SourceLabel { get; }

        /// <summary>All keys/addresses this source currently knows about, for the dropdown.</summary>
        IReadOnlyList<string> GetKeys();

        /// <summary>
        /// Whether this source can "promote" the plain project scene at <paramref name="projectScenePath"/>
        /// (named <paramref name="sceneName"/>) into one of its own keys — powers the "Mark as …" helper
        /// button. Return false to skip the button entirely for this scene.
        /// </summary>
        bool CanPromote(string projectScenePath, string sceneName);

        /// <summary>Promotes <paramref name="projectScenePath"/> into this source, keyed as <paramref name="sceneName"/>.</summary>
        void Promote(string projectScenePath, string sceneName);
    }

    /// <summary>Opt-in registry of <see cref="ISceneKeySourceProvider"/>s. Empty by default.</summary>
    public static class SceneKeySourceRegistry
    {
        private static readonly List<ISceneKeySourceProvider> _providers = new List<ISceneKeySourceProvider>();

        /// <summary>Every registered provider, in registration order.</summary>
        public static IReadOnlyList<ISceneKeySourceProvider> Providers => _providers;

        /// <summary>Registers <paramref name="provider"/> (idempotent; nulls ignored).</summary>
        public static void Register(ISceneKeySourceProvider provider)
        {
            if (provider != null && !_providers.Contains(provider)) _providers.Add(provider);
        }

        /// <summary>Removes <paramref name="provider"/>.</summary>
        public static void Unregister(ISceneKeySourceProvider provider) => _providers.Remove(provider);
    }
}
