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
        [SerializeField, Tooltip("The signal to raise. Drag a SignalName asset.")]
        private SignalName _signal;

        public SignalName Signal { get => _signal; set => _signal = value; }

        public override void Execute(BaseContext context)
        {
            if (context == null || _signal == null) return;
            string name = (string)_signal;
            if (string.IsNullOrEmpty(name)) return;
            context.RaiseSignal(name);
        }
    }
}
