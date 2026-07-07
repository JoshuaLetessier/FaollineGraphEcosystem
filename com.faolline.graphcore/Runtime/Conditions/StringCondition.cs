using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Universal condition: reads a <see cref="VariableDef"/> (string) from the context and compares it for
    /// equality to an expected value (optionally negated). False (silently) when the parameter is unassigned or
    /// absent — set <see cref="WarnOnMissing"/> to warn instead; a wrong-typed value always warns (a real
    /// misconfiguration). Canonical home in GraphCore; downstream libs subclass this.
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Conditions/String", fileName = "StringCondition")]
    public class StringCondition : BaseCondition, IVariableReferencing
    {
        [SerializeField, Tooltip("Variable asset to read and compare. Drag a VariableDef (type String).")]
        private VariableDef _variable;
        [SerializeField, Tooltip("The string value this condition compares against (case-sensitive equality).")]
        private string _expectedValue;
        [SerializeField, Tooltip("When enabled, the condition passes on inequality instead of equality.")]
        private bool _negate;
        [SerializeField, Tooltip("When enabled, logs a warning if the parameter is absent from the context. When disabled (default), absent keys silently evaluate to false.")]
        private bool _warnOnMissing;

        /// <summary>The parameter asset to evaluate.</summary>
        public VariableDef Variable { get => _variable; set => _variable = value; }

        /// <summary>The string value this condition compares against.</summary>
        public string ExpectedValue { get => _expectedValue; set => _expectedValue = value; }

        /// <summary>When true, the condition passes on inequality instead of equality.</summary>
        public bool Negate { get => _negate; set => _negate = value; }

        /// <summary>When true, logs a warning if the parameter is absent (default false — absent reads as false silently).</summary>
        public bool WarnOnMissing { get => _warnOnMissing; set => _warnOnMissing = value; }

        /// <inheritdoc/>
        public IEnumerable<VariableReference> ReferencedVariables { get { if (_variable != null) yield return new VariableReference(_variable, VariableType.String); } }

        /// <inheritdoc/>
        public override bool Evaluate(BaseContext context)
        {
            if (_variable == null) return false;
            string value;
            try
            {
                if (!context.TryGet<string>(_variable, out value))
                {
                    if (_warnOnMissing)
                        Debug.LogWarning($"[GraphCore] StringCondition: parameter '{_variable.DisplayName}' not found — false.");
                    return false;
                }
            }
            catch (System.InvalidCastException)
            {
                Debug.LogWarning($"[GraphCore] StringCondition: parameter '{_variable.DisplayName}' is not a string — false.");
                return false;
            }
            bool equal = value == _expectedValue;
            return _negate ? !equal : equal;
        }
    }
}
