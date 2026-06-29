using UnityEngine;

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
        [SerializeField, Tooltip("Drag a SignalName asset for typo-safe references. Takes precedence over the raw string.")]
        private SignalName _signalAsset;
        [SerializeField, Tooltip("Fallback: raw signal name string (used when no SignalName asset is assigned).")]
        private string _signalRaw;

        public SignalName SignalAsset { get => _signalAsset; set => _signalAsset = value; }
        public string SignalRaw { get => _signalRaw; set => _signalRaw = value; }

        public override void Execute(BaseContext context)
        {
            if (context == null) return;
            string name = _signalAsset != null ? (string)_signalAsset : _signalRaw;
            if (string.IsNullOrEmpty(name)) return;
            context.RaiseSignal(name);
        }
    }
}
