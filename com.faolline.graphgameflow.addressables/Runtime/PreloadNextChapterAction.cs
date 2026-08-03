using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Faolline.GraphCore;
using Faolline.GraphGameFlow;

namespace Faolline.GraphGameFlow.Addressables
{
    /// <summary>
    /// Triggers an early, asynchronous load of the next chapter's graph via a soft <see cref="AssetReferenceT{TObject}"/>
    /// — never a build-time dependency of the graph that owns this action — so the target can be ready well
    /// before it's needed. <see cref="Execute"/> returns immediately (synchronous <c>void</c>, exactly like every
    /// other <see cref="BaseAction"/>); the actual load happens on Addressables' own async machinery, same shape
    /// as <see cref="AddressablesSceneLoader"/>.
    /// <para>
    /// Supports both preload usage forms: <b>early trigger + reboot</b> — read
    /// <see cref="GameFlowContext.PendingNextGraph"/> from the host's <c>OnEnded</c> handler once the chapter
    /// naturally ends, then reboot the driver onto it; or <b>park on signal</b> — configure
    /// <see cref="CompletedSignal"/> and place an <c>AwaitSignalNames</c> node after this action so the flow
    /// parks until the preload resolves. Neither needs any change to <c>BaseRunner</c>.
    /// </para>
    /// </summary>
    public class PreloadNextChapterAction : BaseAction
    {
        [SerializeField, Tooltip("The next chapter's graph, referenced softly — never pulled into this graph's build/bundle dependencies.")]
        private AssetReferenceT<BaseGraph> _nextChapter;
        [SerializeField, Tooltip("Optional signal raised into the target driver once the preload resolves (no payload use beyond the key). Pair with an AwaitSignalNames node to park the flow until the preload completes.")]
        private SignalDef _completedSignal;
        [SerializeField, Tooltip("Optional signal raised into the target driver if the preload fails. Add as a second AwaitSignalNames entry alongside CompletedSignal so a failure resumes the flow instead of stalling it forever.")]
        private SignalDef _failedSignal;
        [SerializeField, Tooltip("The driver that receives the completion/failure signals and whose context's PendingNextGraph is set. When null, falls back to GraphFlowDriver.Active.")]
        private GraphFlowDriver _signalDriver;

        /// <summary>The next chapter's graph, referenced softly.</summary>
        public AssetReferenceT<BaseGraph> NextChapter { get => _nextChapter; set => _nextChapter = value; }

        /// <summary>Optional signal raised once the preload resolves.</summary>
        public SignalDef CompletedSignal { get => _completedSignal; set => _completedSignal = value; }

        /// <summary>Optional signal raised if the preload fails.</summary>
        public SignalDef FailedSignal { get => _failedSignal; set => _failedSignal = value; }

        /// <summary>
        /// Receiver of the completion/failure signals; null falls back to <see cref="GraphFlowDriver.Active"/>.
        /// A graph built in code (rather than authored with this action already assigned in the inspector) has
        /// no other way to target a specific driver — mirrors <see cref="AddressablesSceneLoader.SignalDriver"/>.
        /// </summary>
        public GraphFlowDriver SignalDriver { get => _signalDriver; set => _signalDriver = value; }

        /// <summary>
        /// Releases the Addressables handle for <see cref="NextChapter"/> (via <c>AssetReference.ReleaseAsset</c>),
        /// letting the resolved graph be unloaded once nothing else references it. Call this once the preloaded
        /// graph is no longer needed (e.g. after rebooting past it, or when abandoning a preload) — neither
        /// <see cref="Execute"/> nor the completion callback releases it automatically, since the whole point of
        /// preloading is to keep the graph alive for the host to use afterward.
        /// </summary>
        public void ReleaseNextChapter() => _nextChapter?.ReleaseAsset();

        /// <inheritdoc/>
        public override void Execute(BaseContext context)
        {
            if (_nextChapter == null || !_nextChapter.RuntimeKeyIsValid())
            {
                Debug.LogError("[GraphGameFlow] PreloadNextChapterAction.Execute called with no valid NextChapter reference; ignored.");
                return;
            }

            var handle = _nextChapter.LoadAssetAsync();
            handle.Completed += op => HandleCompleted(op, context as GameFlowContext);
        }

        private void HandleCompleted(AsyncOperationHandle<BaseGraph> op, GameFlowContext context)
        {
            var driver = _signalDriver != null ? _signalDriver : GraphFlowDriver.Active;

            if (op.Status != AsyncOperationStatus.Succeeded || op.Result == null)
            {
                var reason = $"PreloadNextChapterAction: next chapter failed to preload: {op.OperationException}";
                Debug.LogError($"[GraphGameFlow] {reason}");
                RaiseSignal(_failedSignal, driver, reason);
                return;
            }

            if (context != null) context.PendingNextGraph = op.Result;
            RaiseSignal(_completedSignal, driver, op.Result.GraphId);
        }

        private static void RaiseSignal(SignalDef signal, GraphFlowDriver driver, string payload)
        {
            if (signal == null) return;
            var name = (string)signal;
            if (string.IsNullOrEmpty(name)) return;

            if (driver == null)
            {
                Debug.LogWarning(
                    "[GraphGameFlow] PreloadNextChapterAction: signal configured but no target driver " +
                    "(SignalDriver unset and GraphFlowDriver.Active is null); signal dropped.");
                return;
            }

            driver.RaiseSignal(name, payload);
        }
    }
}
