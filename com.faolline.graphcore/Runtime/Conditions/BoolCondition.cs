using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Universal condition: reads a <see cref="VariableDef"/> (bool) from the context and compares it to an
    /// expected value. Reads false (silently) when the parameter is unassigned or absent — a not-yet-set flag is
    /// false, never throws; set <see cref="WarnOnMissing"/> to log a warning instead. This is the canonical home
    /// for the primitive bool condition: downstream libs that historically shipped their own (GraphStandard,
    /// GraphDialogue) now subclass this so there is a single implementation and no cross-namespace ambiguity.
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Conditions/Bool", fileName = "BoolCondition")]
    public class BoolCondition : BaseCondition, IVariableReferencing
    {
        [SerializeField, Tooltip("Variable asset to read and compare. Drag a VariableDef (type Bool).")]
        private VariableDef _variable;
        [SerializeField, Tooltip("The bool value this condition expects to find in the context.")]
        private bool _expectedValue;
        [SerializeField, Tooltip("When enabled, logs a warning if the parameter is absent from the context. When disabled (default), absent keys silently evaluate to false.")]
        private bool _warnOnMissing;

        /// <summary>The parameter asset to evaluate.</summary>
        public VariableDef Variable { get => _variable; set => _variable = value; }

        /// <summary>The bool value this condition expects to find in the context.</summary>
        public bool ExpectedValue { get => _expectedValue; set => _expectedValue = value; }

        /// <summary>When true, logs a warning if the parameter is absent (default false — absent reads as false silently).</summary>
        public bool WarnOnMissing { get => _warnOnMissing; set => _warnOnMissing = value; }

        /// <inheritdoc/>
        public IEnumerable<VariableReference> ReferencedVariables { get { if (_variable != null) yield return new VariableReference(_variable, VariableType.Bool); } }

        /// <inheritdoc/>
        public override bool Evaluate(BaseContext context)
        {
            if (_variable == null) return false;
            if (!context.TryGet<bool>(_variable, out var value))
            {
                if (_warnOnMissing)
                    Debug.LogWarning($"[GraphCore] BoolCondition: parameter '{_variable.DisplayName}' not found — false.");
                return false;
            }
            return value == _expectedValue;
        }
    }
}
