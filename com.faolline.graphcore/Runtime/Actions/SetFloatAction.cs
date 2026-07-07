using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>Universal action: writes a fixed float value into the execution context under a
    /// <see cref="VariableDef"/> asset's stable GUID key. Canonical home in GraphCore; downstream libs subclass
    /// this.</summary>
    [CreateAssetMenu(menuName = "Faolline/Actions/Set Float", fileName = "SetFloatAction")]
    public class SetFloatAction : BaseAction, IVariableReferencing
    {
        [SerializeField, Tooltip("Variable asset to write. Drag a VariableDef (type Float); its stable GUID is the context key.")]
        private VariableDef _variable;
        [SerializeField, Tooltip("The float value written to the parameter.")]
        private float _value;

        /// <summary>The parameter asset to write.</summary>
        public VariableDef Variable { get => _variable; set => _variable = value; }

        /// <summary>The float value to set on the parameter.</summary>
        public float Value { get => _value; set => _value = value; }

        /// <inheritdoc/>
        public IEnumerable<VariableReference> ReferencedVariables { get { if (_variable != null) yield return new VariableReference(_variable, VariableType.Float); } }

        /// <inheritdoc/>
        public override void Execute(BaseContext context)
        {
            if (_variable == null) return;
            context.Set<float>(_variable, _value);
        }
    }
}
