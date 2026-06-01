using System.Collections.Generic;

namespace Faolline.GraphLocalization.Editor
{
    /// <summary>
    /// Central registry of all <see cref="IGraphLocalizationAdapter"/> implementations.
    /// Each graph lib's Editor assembly registers its adapter once via [InitializeOnLoad].
    /// The builder iterates all registered adapters on each build.
    /// </summary>
    public static class GraphLocalizationAdapterRegistry
    {
        private static readonly List<IGraphLocalizationAdapter> _adapters = new List<IGraphLocalizationAdapter>();

        /// <summary>All currently registered adapters.</summary>
        public static IReadOnlyList<IGraphLocalizationAdapter> Adapters => _adapters;

        /// <summary>
        /// Registers an adapter. Called once per lib from an [InitializeOnLoad] static ctor.
        /// Duplicate registrations (same LibName) are silently ignored.
        /// </summary>
        public static void Register(IGraphLocalizationAdapter adapter)
        {
            if (adapter == null) return;
            foreach (var existing in _adapters)
                if (existing.LibName == adapter.LibName) return;
            _adapters.Add(adapter);
        }

        /// <summary>Removes a previously registered adapter (useful in tests).</summary>
        public static void Unregister(IGraphLocalizationAdapter adapter)
        {
            _adapters.Remove(adapter);
        }

        /// <summary>Clears all registrations (used in tests).</summary>
        public static void Clear() => _adapters.Clear();
    }
}
