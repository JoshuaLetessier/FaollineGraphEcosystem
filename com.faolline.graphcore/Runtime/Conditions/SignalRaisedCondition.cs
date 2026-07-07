using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Returns <c>true</c> when the named signal has been raised at least once in the context.
    /// Unlike a transient signal subscriber, this reads the durable raised-signal history maintained
    /// by <see cref="BaseContext"/> — it works on any frame, not just the one the signal fired on.
    /// Pair with <see cref="ForgetSignalAction"/> to reset the "seen" state (e.g. for replay).
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Conditions/Signal Raised", fileName = "SignalRaisedCondition")]
    [Icon("Packages/com.faolline.graphcore/Editor/Icons/ico_condition.png")]
    public class SignalRaisedCondition : BaseCondition
    {
        [SerializeField] private SignalDef _signal;

        public SignalDef Signal { get => _signal; set => _signal = value; }

        public override bool Evaluate(BaseContext context)
        {
            if (context == null || _signal == null) return false;
            string name = (string)_signal;
            return !string.IsNullOrEmpty(name) && context.HasSignalBeenRaised(name);
        }
    }
}
