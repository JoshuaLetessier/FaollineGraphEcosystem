using System.Collections.Generic;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Generic opinion-poll a downstream lib can plug into <see cref="GraphValidator"/> without graphcore
    /// ever learning what that lib's domain concepts mean (Constitution II — graphcore has zero knowledge
    /// of dialoguesystem/gameflow/questsystem/etc.). Mirrors the ecosystem's existing
    /// <c>ContextKeyLabelRegistry</c>/<c>IContextLabelResolver</c> seam shape.
    /// </summary>
    public interface IGraphValidatorExtension
    {
        /// <summary>
        /// Called for every <see cref="SubGraphNodeData"/> with a resolved target. Return a non-empty
        /// message if this extension considers <paramref name="targetGraph"/> a problematic hard-reference
        /// target (e.g. "this is itself a registered chapter root"); return <c>null</c>/empty for no opinion.
        /// </summary>
        string CheckSubGraphTarget(BaseGraph targetGraph);
    }

    /// <summary>Opt-in registry of <see cref="IGraphValidatorExtension"/>s. Empty by default.</summary>
    public static class GraphValidatorExtensionRegistry
    {
        private static readonly List<IGraphValidatorExtension> _extensions = new List<IGraphValidatorExtension>();

        /// <summary>Every registered extension, in registration order.</summary>
        public static IReadOnlyList<IGraphValidatorExtension> Extensions => _extensions;

        /// <summary>Registers <paramref name="extension"/> (idempotent; nulls ignored).</summary>
        public static void Register(IGraphValidatorExtension extension)
        {
            if (extension != null && !_extensions.Contains(extension)) _extensions.Add(extension);
        }

        /// <summary>Removes <paramref name="extension"/>.</summary>
        public static void Unregister(IGraphValidatorExtension extension) => _extensions.Remove(extension);
    }
}
