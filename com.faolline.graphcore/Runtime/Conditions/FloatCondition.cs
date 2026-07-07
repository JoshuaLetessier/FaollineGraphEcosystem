using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Universal condition: reads a <see cref="ParameterName"/> (float) from the context and compares it to an
    /// expected value via a <see cref="ComparisonOperator"/>. False (silently) when the parameter is unassigned
    /// or absent — set <see cref="WarnOnMissing"/> to warn instead; a wrong-typed value always warns (a real
    /// misconfiguration). Canonical home in GraphCore; downstream libs subclass this.
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Conditions/Float", fileName = "FloatCondition")]
    public class FloatCondition : BaseCondition, IParameterReferencing
    {
        [SerializeField, Tooltip("Parameter asset to read and compare. Drag a ParameterName (type Float).")]
        private ParameterName _parameter;
        [SerializeField, Tooltip("Comparison applied between the context value and the expected value (Equal, NotEqual, Less, LessOrEqual, Greater, GreaterOrEqual).")]
        private ComparisonOperator _operator = ComparisonOperator.Equal;
        [SerializeField, Tooltip("The float value this condition compares against.")]
        private float _expectedValue;
        [SerializeField, Tooltip("When enabled, logs a warning if the parameter is absent from the context. When disabled (default), absent keys silently evaluate to false.")]
        private bool _warnOnMissing;

        /// <summary>The parameter asset to evaluate.</summary>
        public ParameterName Parameter { get => _parameter; set => _parameter = value; }

        /// <summary>The comparison applied between the parameter value and <see cref="ExpectedValue"/>.</summary>
        public ComparisonOperator Operator { get => _operator; set => _operator = value; }

        /// <summary>The float value this condition compares against.</summary>
        public float ExpectedValue { get => _expectedValue; set => _expectedValue = value; }

        /// <summary>When true, logs a warning if the parameter is absent (default false — absent reads as false silently).</summary>
        public bool WarnOnMissing { get => _warnOnMissing; set => _warnOnMissing = value; }

        /// <inheritdoc/>
        public IEnumerable<ParameterReference> ReferencedParameters { get { if (_parameter != null) yield return new ParameterReference(_parameter, ParameterType.Float); } }

        /// <inheritdoc/>
        public override bool Evaluate(BaseContext context)
        {
            if (_parameter == null) return false;
            float value;
            try
            {
                if (!context.TryGet<float>(_parameter, out value))
                {
                    if (_warnOnMissing)
                        Debug.LogWarning($"[GraphCore] FloatCondition: parameter '{_parameter.DisplayName}' not found — false.");
                    return false;
                }
            }
            catch (System.InvalidCastException)
            {
                Debug.LogWarning($"[GraphCore] FloatCondition: parameter '{_parameter.DisplayName}' is not a float — false.");
                return false;
            }
            return _operator.Matches(value.CompareTo(_expectedValue));
        }
    }
}
