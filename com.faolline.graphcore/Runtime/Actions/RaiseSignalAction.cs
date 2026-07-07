using UnityEngine;
using UnityEngine.Serialization;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Raises a named signal into the context when executed. Pairs with
    /// <see cref="BaseNodeData.AwaitSignalName"/> to let one part of a graph
    /// unblock another without host code.
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Actions/Raise Signal", fileName = "RaiseSignalAction")]
    public class RaiseSignalAction : BaseAction
    {
        // FormerlySerializedAs recovers assets authored before the 0.22 asset-only refactor, which renamed
        // _signalAsset → _signal (the raw-string _signalRaw fallback was intentionally dropped and cannot be
        // migrated to a SignalDef asset).
        [SerializeField, FormerlySerializedAs("_signalAsset"), Tooltip("The signal to raise. Drag a SignalDef asset.")]
        private SignalDef _signal;

        public SignalDef Signal { get => _signal; set => _signal = value; }

        public override void Execute(BaseContext context)
        {
            if (context == null || _signal == null) return;
            string name = (string)_signal;
            if (string.IsNullOrEmpty(name)) return;
            context.RaiseSignal(name);
        }
    }
}
