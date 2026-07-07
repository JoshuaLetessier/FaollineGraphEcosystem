using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>Adds <see cref="Value"/> to the current float at <see cref="Variable"/> (defaults to 0 when
    /// absent). Use for continuous modifiers (health, progress bars) where the result is relative.</summary>
    [CreateAssetMenu(menuName = "Faolline/Actions/Add Float", fileName = "AddFloatAction")]
    public class AddFloatAction : BaseAction, IVariableReferencing
    {
        [SerializeField, Tooltip("Variable asset to modify. Drag a VariableDef (type Float); its stable GUID is the context key.")]
        private VariableDef _variable;
        [SerializeField, Tooltip("The float value added to the current value (use a negative number to subtract).")]
        private float _value;

        /// <summary>The parameter asset to modify.</summary>
        public VariableDef Variable { get => _variable; set => _variable = value; }

        /// <summary>The float value to add (negative to subtract).</summary>
        public float Value { get => _value; set => _value = value; }

        /// <inheritdoc/>
        public IEnumerable<VariableReference> ReferencedVariables { get { if (_variable != null) yield return new VariableReference(_variable, VariableType.Float); } }

        /// <inheritdoc/>
        public override void Execute(BaseContext context)
        {
            if (_variable == null) return;
            context.TryGet<float>(_variable, out var current);
            context.Set<float>(_variable, current + _value);
        }
    }
}
