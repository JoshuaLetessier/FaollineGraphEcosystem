using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Universal condition: reads a named float parameter from the context and compares it to an expected value
    /// via a <see cref="ComparisonOperator"/>. False (silently) when the key is absent — set
    /// <see cref="WarnOnMissing"/> to warn instead; a wrong-typed value always warns (a real misconfiguration).
    /// Canonical home in GraphCore; downstream libs subclass this.
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Conditions/Float", fileName = "FloatCondition")]
    public class FloatCondition : BaseCondition
    {
        [SerializeField, Tooltip("Context parameter key to read and compare.")]
        private string _parameterKey;
        [SerializeField, Tooltip("Comparison applied between the context value and the expected value (Equal, NotEqual, Less, LessOrEqual, Greater, GreaterOrEqual).")]
        private ComparisonOperator _operator = ComparisonOperator.Equal;
        [SerializeField, Tooltip("The float value this condition compares against.")]
        private float _expectedValue;
        [SerializeField, Tooltip("When enabled, logs a warning if the parameter key is absent from the context. When disabled (default), absent keys silently evaluate to false.")]
        private bool _warnOnMissing;

        /// <summary>The context parameter key to evaluate.</summary>
        public string ParameterKey { get => _parameterKey; set => _parameterKey = value; }

        /// <summary>The comparison applied between the parameter value and <see cref="ExpectedValue"/>.</summary>
        public ComparisonOperator Operator { get => _operator; set => _operator = value; }

        /// <summary>The float value this condition compares against.</summary>
        public float ExpectedValue { get => _expectedValue; set => _expectedValue = value; }

        /// <summary>When true, logs a warning if the key is absent (default false — absent reads as false silently).</summary>
        public bool WarnOnMissing { get => _warnOnMissing; set => _warnOnMissing = value; }

        /// <inheritdoc/>
        public override bool Evaluate(BaseContext context)
        {
            float value;
            try
            {
                if (!context.TryGet<float>(_parameterKey, out value))
                {
                    if (_warnOnMissing)
                        Debug.LogWarning($"[GraphCore] FloatCondition: parameter '{_parameterKey}' not found — false.");
                    return false;
                }
            }
            catch (System.InvalidCastException)
            {
                Debug.LogWarning($"[GraphCore] FloatCondition: parameter '{_parameterKey}' is not a float — false.");
                return false;
            }
            return _operator.Matches(value.CompareTo(_expectedValue));
        }
    }
}
