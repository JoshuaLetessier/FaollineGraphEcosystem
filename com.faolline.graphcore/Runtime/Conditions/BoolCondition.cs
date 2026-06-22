using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Universal condition: reads a named bool parameter from the context and compares it to an expected value.
    /// Reads false (silently) when the key is absent — a not-yet-set flag is false, never throws; set
    /// <see cref="WarnOnMissing"/> to log a warning instead. This is the canonical home for the primitive bool
    /// condition: downstream libs that historically shipped their own (GraphStandard, GraphDialogue) now subclass
    /// this so there is a single implementation and no cross-namespace ambiguity for a consumer using both.
    /// </summary>
    // No [CreateAssetMenu] — created via the inspector's object picker on condition fields.
    public class BoolCondition : BaseCondition
    {
        [SerializeField] private string _parameterKey;
        [SerializeField] private bool _expectedValue;
        [SerializeField] private bool _warnOnMissing;

        /// <summary>The context parameter key to evaluate.</summary>
        public string ParameterKey { get => _parameterKey; set => _parameterKey = value; }

        /// <summary>The bool value this condition expects to find in the context.</summary>
        public bool ExpectedValue { get => _expectedValue; set => _expectedValue = value; }

        /// <summary>When true, logs a warning if the key is absent (default false — absent reads as false silently).</summary>
        public bool WarnOnMissing { get => _warnOnMissing; set => _warnOnMissing = value; }

        /// <inheritdoc/>
        public override bool Evaluate(BaseContext context)
        {
            if (!context.TryGet<bool>(_parameterKey, out var value))
            {
                if (_warnOnMissing)
                    Debug.LogWarning($"[GraphCore] BoolCondition: parameter '{_parameterKey}' not found — false.");
                return false;
            }
            return value == _expectedValue;
        }
    }
}
