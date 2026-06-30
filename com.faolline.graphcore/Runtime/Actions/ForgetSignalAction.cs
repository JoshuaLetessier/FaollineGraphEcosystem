using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Removes a signal from the context's raised-signal history so that a subsequent
    /// <see cref="SignalRaisedCondition"/> returns <c>false</c> again. Use this to allow
    /// a signal-gated branch to be re-entered (replay, dialogue restart, chapter reset, etc.).
    /// No-op when the signal was not in the history.
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Actions/Forget Signal", fileName = "ForgetSignalAction")]
    [Icon("Packages/com.faolline.graphcore/Editor/Icons/ico_action.png")]
    public class ForgetSignalAction : BaseAction
    {
        [SerializeField] private SignalName _signal;

        public SignalName Signal { get => _signal; set => _signal = value; }

        public override void Execute(BaseContext context)
        {
            if (context == null || _signal == null) return;
            string name = (string)_signal;
            if (!string.IsNullOrEmpty(name))
                context.ForgetSignal(name);
        }
    }
}
