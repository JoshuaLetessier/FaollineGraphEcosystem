using System.Collections.Generic;

namespace Faolline.GraphGameFlow.Editor
{
    /// <summary>
    /// Graph-side mirror of <see cref="ISceneKeySourceProvider"/>/<see cref="SceneKeySourceRegistry"/>: an
    /// opt-in source of graph-key identifiers (e.g. registered Addressable graph addresses when Addressables
    /// is installed) for the <see cref="GraphKeyRegistryWindow"/>. graphgameflow core never references any
    /// concrete source package; an adapter package (like <c>com.faolline.graphgameflow.addressables</c>)
    /// registers one, typically from an <c>[InitializeOnLoadMethod]</c> hook.
    /// </summary>
    public interface IGraphKeySourceProvider
    {
        /// <summary>Toolbar label for this source, e.g. "Addressable".</summary>
        string SourceLabel { get; }

        /// <summary>All keys this source currently knows about.</summary>
        IReadOnlyList<string> GetKeys();

        /// <summary>
        /// Whether this source can "promote" the plain graph asset at <paramref name="graphAssetPath"/>
        /// (identified by <paramref name="graphId"/>) into one of its own keys.
        /// </summary>
        bool CanPromote(string graphAssetPath, string graphId);

        /// <summary>Promotes <paramref name="graphAssetPath"/> into this source, keyed as <paramref name="graphId"/>.</summary>
        void Promote(string graphAssetPath, string graphId);

        /// <summary>
        /// Reverse lookup: does <paramref name="assetGuid"/> currently resolve as one of this source's keys?
        /// Needed by <c>ChapterRootSubGraphValidatorExtension</c> — unlike the scene-side registry, a graph
        /// validator rule needs to ask "is THIS asset a registered entry point", not just list known keys.
        /// </summary>
        bool TryResolveGuid(string assetGuid, out string key);
    }

    /// <summary>Opt-in registry of <see cref="IGraphKeySourceProvider"/>s. Empty by default.</summary>
    public static class GraphKeySourceRegistry
    {
        private static readonly List<IGraphKeySourceProvider> _providers = new List<IGraphKeySourceProvider>();

        /// <summary>Every registered provider, in registration order.</summary>
        public static IReadOnlyList<IGraphKeySourceProvider> Providers => _providers;

        /// <summary>Registers <paramref name="provider"/> (idempotent; nulls ignored).</summary>
        public static void Register(IGraphKeySourceProvider provider)
        {
            if (provider != null && !_providers.Contains(provider)) _providers.Add(provider);
        }

        /// <summary>Removes <paramref name="provider"/>.</summary>
        public static void Unregister(IGraphKeySourceProvider provider) => _providers.Remove(provider);
    }
}
