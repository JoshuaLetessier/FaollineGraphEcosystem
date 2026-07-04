#if UNITY_EDITOR
using System.Collections.Generic;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Editor-only registry mapping live <see cref="IGraphRunProbe"/>s to their <see cref="BaseContext"/>,
    /// so editor tools (e.g. the Context Watch window) can inspect the running context without modifying
    /// <see cref="IGraphRunProbe"/> (which is a frozen interface). Populated by <see cref="BaseRunner"/>
    /// alongside its <see cref="GraphRunMonitor"/> registration. Lives in the Runtime assembly (guarded
    /// by <c>UNITY_EDITOR</c>) so <see cref="BaseRunner"/> can reference it directly.
    /// </summary>
    public static class GraphRunContextRegistry
    {
        private static readonly Dictionary<IGraphRunProbe, BaseContext> _map =
            new Dictionary<IGraphRunProbe, BaseContext>();

        /// <summary>Associates <paramref name="probe"/> with <paramref name="context"/>. Overwrites silently.</summary>
        public static void Register(IGraphRunProbe probe, BaseContext context)
        {
            if (probe == null || context == null) return;
            _map[probe] = context;
        }

        /// <summary>Removes the mapping for <paramref name="probe"/>. No-op when absent.</summary>
        public static void Unregister(IGraphRunProbe probe)
        {
            if (probe != null) _map.Remove(probe);
        }

        /// <summary>Returns the context for <paramref name="probe"/>, or null when not registered.</summary>
        public static BaseContext GetContext(IGraphRunProbe probe)
        {
            if (probe != null && _map.TryGetValue(probe, out var ctx)) return ctx;
            return null;
        }

        /// <summary>Removes every mapping. Editor infrastructure: called when Play mode exits (see
        /// <see cref="GraphRunMonitor.Clear"/>).</summary>
        public static void Clear() => _map.Clear();
    }
}
#endif
