using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Universal action: writes a fixed bool value into the execution context under a <see cref="VariableDef"/>
    /// asset's stable GUID key. Canonical home for the primitive bool setter — downstream libs that historically
    /// shipped their own (GraphStandard, GraphDialogue) now subclass this so there is a single implementation and
    /// no cross-namespace ambiguity.
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Actions/Set Bool", fileName = "SetBoolAction")]
    public class SetBoolAction : BaseAction, IVariableReferencing
    {
        [SerializeField, Tooltip("Variable asset to write. Drag a VariableDef (type Bool); its stable GUID is the context key.")]
        private VariableDef _variable;
        [SerializeField, Tooltip("The bool value written to the parameter.")]
        private bool _value;

        /// <summary>The parameter asset to write.</summary>
        public VariableDef Variable { get => _variable; set => _variable = value; }

        /// <summary>The bool value to set on the parameter.</summary>
        public bool Value { get => _value; set => _value = value; }

        /// <inheritdoc/>
        public IEnumerable<VariableReference> ReferencedVariables { get { if (_variable != null) yield return new VariableReference(_variable, VariableType.Bool); } }

        /// <inheritdoc/>
        public override void Execute(BaseContext context)
        {
            if (_variable == null) return;
            context.Set<bool>(_variable, _value);
        }
    }
}
