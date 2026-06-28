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
        [SerializeField, Tooltip("The signal name to raise. Must match an AwaitSignalName on the target node.")]
        private string _signalName;

        public string SignalName { get => _signalName; set => _signalName = value; }

        public override void Execute(BaseContext context)
        {
            if (context == null || string.IsNullOrEmpty(_signalName)) return;
            context.RaiseSignal(_signalName);
        }
    }
}
