using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Faolline.GraphLocalization.Editor
{
    /// <summary>
    /// Discovers all <see cref="IGraphLocalizationAdapter"/> implementations in the project.
    ///
    /// Discovery is automatic via <see cref="TypeCache"/>: any non-abstract type implementing the
    /// interface with a public parameterless constructor is found and instantiated — a lib only needs
    /// to implement the interface, no registration call required. <see cref="Register"/> remains for
    /// adapters that cannot be default-constructed (manual/runtime registration). Results are merged
    /// and de-duplicated by <see cref="IGraphLocalizationAdapter.LibName"/>.
    ///
    /// Because discovery is recomputed on demand, no static state can be left in a broken condition
    /// (e.g. by a test) — the build always sees the real adapters.
    /// </summary>
    public static class GraphLocalizationAdapterRegistry
    {
        // Manual registrations only (for adapters without a parameterless ctor, or runtime-added).
        private static readonly List<IGraphLocalizationAdapter> _manual = new List<IGraphLocalizationAdapter>();

        /// <summary>The manually-registered adapters (does not include auto-discovered ones).</summary>
        public static IReadOnlyList<IGraphLocalizationAdapter> Adapters => _manual;

        /// <summary>
        /// All adapters available to the builder: auto-discovered via TypeCache plus any manual
        /// registrations, de-duplicated by LibName. This is what <c>Build All Tables</c> uses.
        /// </summary>
        public static IReadOnlyList<IGraphLocalizationAdapter> DiscoverAdapters()
        {
            var result = new List<IGraphLocalizationAdapter>();
            var seen = new HashSet<string>();

            foreach (var type in TypeCache.GetTypesDerivedFrom<IGraphLocalizationAdapter>())
            {
                if (type.IsAbstract || type.IsInterface) continue;
                if (type.GetConstructor(Type.EmptyTypes) == null) continue; // needs parameterless ctor

                IGraphLocalizationAdapter instance;
                try { instance = (IGraphLocalizationAdapter)Activator.CreateInstance(type); }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[GraphLocalizationAdapterRegistry] Could not instantiate '{type.FullName}': {ex.Message}");
                    continue;
                }

                if (instance != null && !string.IsNullOrEmpty(instance.LibName) && seen.Add(instance.LibName))
                    result.Add(instance);
            }

            foreach (var adapter in _manual)
                if (adapter != null && !string.IsNullOrEmpty(adapter.LibName) && seen.Add(adapter.LibName))
                    result.Add(adapter);

            return result;
        }

        /// <summary>
        /// Manually registers an adapter (for types that cannot be auto-discovered, e.g. without a
        /// parameterless ctor). Duplicate LibNames are ignored.
        /// </summary>
        public static void Register(IGraphLocalizationAdapter adapter)
        {
            if (adapter == null) return;
            foreach (var existing in _manual)
                if (existing.LibName == adapter.LibName) return;
            _manual.Add(adapter);
        }

        /// <summary>Removes a manually-registered adapter.</summary>
        public static void Unregister(IGraphLocalizationAdapter adapter) => _manual.Remove(adapter);

        /// <summary>Clears manual registrations (auto-discovery is unaffected).</summary>
        public static void Clear() => _manual.Clear();
    }
}
