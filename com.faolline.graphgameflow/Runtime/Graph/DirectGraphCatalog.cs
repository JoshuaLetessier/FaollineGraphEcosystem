using System;
using System.Collections.Generic;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphGameFlow
{
    /// <summary>
    /// Zero-dependency <see cref="IGraphCatalog"/>: a plain in-memory <c>graphId → BaseGraph</c> map, resolved
    /// synchronously. This is the default a project reaches for when it has multiple root graphs but no
    /// asynchronous asset-loading technology installed — proving the seam never mandates one.
    /// </summary>
    public class DirectGraphCatalog : IGraphCatalog
    {
        private readonly Dictionary<string, BaseGraph> _graphs = new Dictionary<string, BaseGraph>();

        /// <summary>Registers (or replaces) the direct mapping for <paramref name="graphId"/>.</summary>
        public void Register(string graphId, BaseGraph graph)
        {
            if (string.IsNullOrEmpty(graphId) || graph == null) return;
            _graphs[graphId] = graph;
        }

        /// <summary>Removes the mapping for <paramref name="graphId"/>, if any.</summary>
        public void Unregister(string graphId)
        {
            if (!string.IsNullOrEmpty(graphId)) _graphs.Remove(graphId);
        }

        /// <inheritdoc/>
        public void Resolve(string graphId, Action<BaseGraph> onResolved, Action<string> onFailed)
        {
            if (string.IsNullOrEmpty(graphId))
            {
                onFailed?.Invoke("DirectGraphCatalog.Resolve called with a null or empty graphId.");
                return;
            }

            if (_graphs.TryGetValue(graphId, out var graph))
            {
                onResolved?.Invoke(graph);
                return;
            }

            var reason = $"DirectGraphCatalog has no graph registered for id '{graphId}'.";
            Debug.LogError($"[GraphGameFlow] {reason}");
            onFailed?.Invoke(reason);
        }
    }
}
