using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphStandard
{
    /// <summary>
    /// Domain-neutral condition: reads a named bool parameter from the context and compares it to an expected
    /// value. False (silently) when the parameter key is absent — a not-yet-set flag reads as false, like the
    /// collection conditions; set <see cref="WarnOnMissing"/> to log a warning instead.
    /// </summary>
    [CreateAssetMenu(menuName = "GraphStandard/Conditions/Bool Condition", fileName = "BoolCondition")]
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
                    Debug.LogWarning($"[GraphStandard] BoolCondition: parameter '{_parameterKey}' not found — false.");
                return false;
            }
            return value == _expectedValue;
        }
    }
}
