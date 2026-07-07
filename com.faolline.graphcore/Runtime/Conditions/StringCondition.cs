using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Universal condition: reads a <see cref="ParameterName"/> (string) from the context and compares it for
    /// equality to an expected value (optionally negated). False (silently) when the parameter is unassigned or
    /// absent — set <see cref="WarnOnMissing"/> to warn instead; a wrong-typed value always warns (a real
    /// misconfiguration). Canonical home in GraphCore; downstream libs subclass this.
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Conditions/String", fileName = "StringCondition")]
    public class StringCondition : BaseCondition, IParameterReferencing
    {
        [SerializeField, Tooltip("Parameter asset to read and compare. Drag a ParameterName (type String).")]
        private ParameterName _parameter;
        [SerializeField, Tooltip("The string value this condition compares against (case-sensitive equality).")]
        private string _expectedValue;
        [SerializeField, Tooltip("When enabled, the condition passes on inequality instead of equality.")]
        private bool _negate;
        [SerializeField, Tooltip("When enabled, logs a warning if the parameter is absent from the context. When disabled (default), absent keys silently evaluate to false.")]
        private bool _warnOnMissing;

        /// <summary>The parameter asset to evaluate.</summary>
        public ParameterName Parameter { get => _parameter; set => _parameter = value; }

        /// <summary>The string value this condition compares against.</summary>
        public string ExpectedValue { get => _expectedValue; set => _expectedValue = value; }

        /// <summary>When true, the condition passes on inequality instead of equality.</summary>
        public bool Negate { get => _negate; set => _negate = value; }

        /// <summary>When true, logs a warning if the parameter is absent (default false — absent reads as false silently).</summary>
        public bool WarnOnMissing { get => _warnOnMissing; set => _warnOnMissing = value; }

        /// <inheritdoc/>
        public IEnumerable<ParameterReference> ReferencedParameters { get { if (_parameter != null) yield return new ParameterReference(_parameter, ParameterType.String); } }

        /// <inheritdoc/>
        public override bool Evaluate(BaseContext context)
        {
            if (_parameter == null) return false;
            string value;
            try
            {
                if (!context.TryGet<string>(_parameter, out value))
                {
                    if (_warnOnMissing)
                        Debug.LogWarning($"[GraphCore] StringCondition: parameter '{_parameter.DisplayName}' not found — false.");
                    return false;
                }
            }
            catch (System.InvalidCastException)
            {
                Debug.LogWarning($"[GraphCore] StringCondition: parameter '{_parameter.DisplayName}' is not a string — false.");
                return false;
            }
            bool equal = value == _expectedValue;
            return _negate ? !equal : equal;
        }
    }
}
