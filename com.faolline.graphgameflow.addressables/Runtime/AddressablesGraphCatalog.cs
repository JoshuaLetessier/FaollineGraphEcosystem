using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Faolline.GraphCore;
using Faolline.GraphGameFlow;
using Faolline.GraphLogging;


namespace Faolline.GraphGameFlow.Addressables
{
    /// <summary>
    /// Drop-in <see cref="IGraphCatalog"/> that resolves a <c>graphId</c> to a <see cref="BaseGraph"/> through
    /// <c>UnityEngine.AddressableAssets.Addressables</c> — the graph-side mirror of <see cref="AddressablesSceneLoader"/>.
    /// The <c>graphId</c> passed to <see cref="Resolve"/> is the Addressable key (address, label, or GUID) the
    /// target graph was registered under — typically its own <see cref="BaseGraph.GraphId"/>, promoted via
    /// <c>GraphKeyRegistryWindow</c>.
    /// <para>
    /// Every resolved <c>graphId</c>'s <see cref="AsyncOperationHandle{TObject}"/> is kept (this instance's own
    /// <c>_handles</c> map, mirroring <see cref="AddressablesSceneLoader"/>'s own loaded-scenes dictionary) so it
    /// can be released later via <see cref="Release"/> — resolving never automatically releases, since the
    /// resolved graph is typically still in use by the caller. Each <c>graphId</c> maps to a LIST of handles,
    /// not a single one: two concurrent <see cref="Resolve"/> calls for the same key each get their own
    /// Addressables-refcounted handle, and overwriting one with the other would silently leak it — one handle
    /// would never be released by anyone. <see cref="Release"/> releases every handle held for that key.
    /// </para>
    /// </summary>
    public class AddressablesGraphCatalog : IGraphCatalog
    {
        private readonly Dictionary<string, List<AsyncOperationHandle<BaseGraph>>> _handles =
            new Dictionary<string, List<AsyncOperationHandle<BaseGraph>>>();

        /// <inheritdoc/>
        public void Resolve(string graphId, Action<BaseGraph> onResolved, Action<string> onFailed)
        {
            if (string.IsNullOrEmpty(graphId))
            {
                const string reason = "AddressablesGraphCatalog.Resolve called with a null or empty graphId.";
                Logging.Error("GraphGameFlow", $"[GraphGameFlow] {reason}");
                onFailed?.Invoke(reason);
                return;
            }

            // Fully qualified: our own namespace's last segment is "Addressables" (matches the ecosystem's
            // Faolline.<Package>.<Adapter> convention), which would otherwise shadow the Unity type.
            var handle = global::UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<BaseGraph>(graphId);
            handle.Completed += op => HandleCompleted(op, graphId, onResolved, onFailed);
        }

        /// <summary>
        /// Releases every Addressables handle held for a previously-resolved <paramref name="graphId"/>
        /// (no-op, with a warning, if nothing was resolved for it by this instance) — call once the graph is
        /// no longer needed so its content can be unloaded. If <paramref name="graphId"/> was resolved more
        /// than once concurrently, ALL of those handles are released, not just the most recent.
        /// </summary>
        public void Release(string graphId)
        {
            if (string.IsNullOrEmpty(graphId) || !_handles.TryGetValue(graphId, out var handles) || handles.Count == 0)
            {
                Logging.Warning("GraphGameFlow", $"[GraphGameFlow] AddressablesGraphCatalog.Release: no handle held for graphId '{graphId}'; ignored.");
                return;
            }

            foreach (var handle in handles)
                global::UnityEngine.AddressableAssets.Addressables.Release(handle);
            _handles.Remove(graphId);
        }

        private void HandleCompleted(AsyncOperationHandle<BaseGraph> op, string graphId,
            Action<BaseGraph> onResolved, Action<string> onFailed)
        {
            if (op.Status == AsyncOperationStatus.Succeeded && op.Result != null)
            {
                if (!_handles.TryGetValue(graphId, out var handles))
                    _handles[graphId] = handles = new List<AsyncOperationHandle<BaseGraph>>();
                handles.Add(op);
                onResolved?.Invoke(op.Result);
                return;
            }

            var reason = $"Addressables graph '{graphId}' failed to resolve: {op.OperationException}";
            Logging.Error("GraphGameFlow", $"[GraphGameFlow] {reason}");
            onFailed?.Invoke(reason);
        }
    }
}
