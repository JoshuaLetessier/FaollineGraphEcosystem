using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphTest
{
    /// <summary>
    /// Condition that passes when the last signal named <see cref="SignalDef"/> carried a string payload
    /// equal to <see cref="ExpectedPayload"/>. Demonstrates a downstream-style condition reading a signal
    /// payload via <see cref="BaseContext.TryGetLastSignal"/>. Returns false when the signal was never
    /// raised, carried no payload, or the payload differs / is not a string.
    /// </summary>
    [CreateAssetMenu(menuName = "GraphTest/Conditions/Signal Payload Condition", fileName = "SignalPayloadCondition")]
    public class TestSignalPayloadCondition : BaseCondition
    {
        [SerializeField] private string _signalName;
        [SerializeField] private string _expectedPayload;

        /// <summary>The signal name whose last payload is inspected.</summary>
        public string SignalDef { get => _signalName; set => _signalName = value; }

        /// <summary>The string payload value this condition compares against.</summary>
        public string ExpectedPayload { get => _expectedPayload; set => _expectedPayload = value; }

        /// <inheritdoc/>
        public override bool Evaluate(BaseContext context)
        {
            if (!context.TryGetLastSignal(_signalName, out var args) || !args.HasPayload)
                return false;
            try { return args.GetPayload<string>() == _expectedPayload; }
            catch (System.InvalidCastException) { return false; }
        }
    }
}
