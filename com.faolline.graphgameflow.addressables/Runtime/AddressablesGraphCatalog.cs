using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Faolline.GraphCore;
using Faolline.GraphGameFlow;

namespace Faolline.GraphGameFlow.Addressables
{
    /// <summary>
    /// Drop-in <see cref="IGraphCatalog"/> that resolves a <c>graphId</c> to a <see cref="BaseGraph"/> through
    /// <c>UnityEngine.AddressableAssets.Addressables</c> — the graph-side mirror of <see cref="AddressablesSceneLoader"/>.
    /// The <c>graphId</c> passed to <see cref="Resolve"/> is the Addressable key (address, label, or GUID) the
    /// target graph was registered under — typically its own <see cref="BaseGraph.GraphId"/>, promoted via
    /// <c>GraphKeyRegistryWindow</c>.
    /// </summary>
    public class AddressablesGraphCatalog : IGraphCatalog
    {
        /// <inheritdoc/>
        public void Resolve(string graphId, Action<BaseGraph> onResolved, Action<string> onFailed)
        {
            if (string.IsNullOrEmpty(graphId))
            {
                const string reason = "AddressablesGraphCatalog.Resolve called with a null or empty graphId.";
                Debug.LogError($"[GraphGameFlow] {reason}");
                onFailed?.Invoke(reason);
                return;
            }

            // Fully qualified: our own namespace's last segment is "Addressables" (matches the ecosystem's
            // Faolline.<Package>.<Adapter> convention), which would otherwise shadow the Unity type.
            var handle = global::UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<BaseGraph>(graphId);
            handle.Completed += op => HandleCompleted(op, graphId, onResolved, onFailed);
        }

        private static void HandleCompleted(AsyncOperationHandle<BaseGraph> op, string graphId,
            Action<BaseGraph> onResolved, Action<string> onFailed)
        {
            if (op.Status == AsyncOperationStatus.Succeeded && op.Result != null)
            {
                onResolved?.Invoke(op.Result);
                return;
            }

            var reason = $"Addressables graph '{graphId}' failed to resolve: {op.OperationException}";
            Debug.LogError($"[GraphGameFlow] {reason}");
            onFailed?.Invoke(reason);
        }
    }
}
