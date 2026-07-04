#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Editor-only registry of the live graph executions in the running game, so a graph editor window can show
    /// an in-game cursor (the active node) during Play — the way the Animator window tracks the running state.
    /// A host (e.g. a driver) registers an <see cref="IGraphRunProbe"/> when its run starts and unregisters when
    /// it stops; it calls <see cref="NotifyChanged"/> whenever the active node moves. The editor subscribes to
    /// <see cref="Changed"/> and reads <see cref="Probes"/>, matching each probe's <see cref="IGraphRunProbe.ActiveGraph"/>
    /// against the graph it displays. Compiled out of player builds entirely (zero runtime footprint).
    /// </summary>
    public static class GraphRunMonitor
    {
        private static readonly List<IGraphRunProbe> _probes = new List<IGraphRunProbe>();

        /// <summary>The live executions currently registered. Read by the editor; treat as read-only.</summary>
        public static IReadOnlyList<IGraphRunProbe> Probes => _probes;

        /// <summary>
        /// Raised when a probe is registered or unregistered, or when a probe's active node/state changes.
        /// The editor repaints its cursor in response. Handlers are invoked on the calling (main) thread.
        /// </summary>
        public static event Action Changed;

        /// <summary>Registers a live execution. Ignores null and duplicates. Raises <see cref="Changed"/>.</summary>
        public static void Register(IGraphRunProbe probe)
        {
            if (probe == null || _probes.Contains(probe)) return;
            _probes.Add(probe);
            Changed?.Invoke();
        }

        /// <summary>Removes a previously registered execution. Raises <see cref="Changed"/> when it was present.</summary>
        public static void Unregister(IGraphRunProbe probe)
        {
            if (probe != null && _probes.Remove(probe))
                Changed?.Invoke();
        }

        /// <summary>Signals that a registered probe's active node or status changed (moves the editor cursor).</summary>
        public static void NotifyChanged() => Changed?.Invoke();

        /// <summary>
        /// Removes every registered probe. Editor infrastructure: called when Play mode exits so probes from
        /// the finished session (including ones a host forgot to detach, or leftovers surviving a disabled
        /// domain reload) never shadow the next session's runs. Raises <see cref="Changed"/> when non-empty.
        /// </summary>
        public static void Clear()
        {
            if (_probes.Count == 0) return;
            _probes.Clear();
            Changed?.Invoke();
        }
    }
}
#endif
