using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Universal condition: reads a <see cref="ParameterName"/> (int) from the context and compares it to an
    /// expected value via a <see cref="ComparisonOperator"/>. False (silently) when the parameter is unassigned
    /// or absent — set <see cref="WarnOnMissing"/> to warn instead; a wrong-typed value always warns (a real
    /// misconfiguration). Canonical home in GraphCore; downstream libs subclass this.
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Conditions/Int", fileName = "IntCondition")]
    public class IntCondition : BaseCondition, IParameterReferencing
    {
        [SerializeField, Tooltip("Parameter asset to read and compare. Drag a ParameterName (type Int).")]
        private ParameterName _parameter;
        [SerializeField, Tooltip("Comparison applied between the context value and the expected value (Equal, NotEqual, Less, LessOrEqual, Greater, GreaterOrEqual).")]
        private ComparisonOperator _operator = ComparisonOperator.Equal;
        [SerializeField, Tooltip("The int value this condition compares against.")]
        private int _expectedValue;
        [SerializeField, Tooltip("When enabled, logs a warning if the parameter is absent from the context. When disabled (default), absent keys silently evaluate to false.")]
        private bool _warnOnMissing;

        /// <summary>The parameter asset to evaluate.</summary>
        public ParameterName Parameter { get => _parameter; set => _parameter = value; }

        /// <summary>The comparison applied between the parameter value and <see cref="ExpectedValue"/>.</summary>
        public ComparisonOperator Operator { get => _operator; set => _operator = value; }

        /// <summary>The int value this condition compares against.</summary>
        public int ExpectedValue { get => _expectedValue; set => _expectedValue = value; }

        /// <summary>When true, logs a warning if the parameter is absent (default false — absent reads as false silently).</summary>
        public bool WarnOnMissing { get => _warnOnMissing; set => _warnOnMissing = value; }

        /// <inheritdoc/>
        public IEnumerable<ParameterReference> ReferencedParameters { get { if (_parameter != null) yield return new ParameterReference(_parameter, ParameterType.Int); } }

        /// <inheritdoc/>
        public override bool Evaluate(BaseContext context)
        {
            if (_parameter == null) return false;
            int value;
            try
            {
                if (!context.TryGet<int>(_parameter, out value))
                {
                    if (_warnOnMissing)
                        Debug.LogWarning($"[GraphCore] IntCondition: parameter '{_parameter.DisplayName}' not found — false.");
                    return false;
                }
            }
            catch (System.InvalidCastException)
            {
                Debug.LogWarning($"[GraphCore] IntCondition: parameter '{_parameter.DisplayName}' is not an int — false.");
                return false;
            }
            return _operator.Matches(value.CompareTo(_expectedValue));
        }
    }
}
