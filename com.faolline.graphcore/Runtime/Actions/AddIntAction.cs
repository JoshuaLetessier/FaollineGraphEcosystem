using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>Adds <see cref="Value"/> to the current int at <see cref="Variable"/> (defaults to 0 when
    /// absent). Use for rewards, costs, and counters where the result is relative to the current value.</summary>
    [CreateAssetMenu(menuName = "Faolline/Actions/Add Int", fileName = "AddIntAction")]
    public class AddIntAction : BaseAction, IVariableReferencing
    {
        [SerializeField, Tooltip("Variable asset to modify. Drag a VariableDef (type Int); its stable GUID is the context key.")]
        private VariableDef _variable;
        [SerializeField, Tooltip("The int value added to the current value (use a negative number to subtract).")]
        private int _value;

        /// <summary>The parameter asset to modify.</summary>
        public VariableDef Variable { get => _variable; set => _variable = value; }

        /// <summary>The int value to add (negative to subtract).</summary>
        public int Value { get => _value; set => _value = value; }

        /// <inheritdoc/>
        public IEnumerable<VariableReference> ReferencedVariables { get { if (_variable != null) yield return new VariableReference(_variable, VariableType.Int); } }

        /// <inheritdoc/>
        public override void Execute(BaseContext context)
        {
            if (_variable == null) return;
            context.TryGet<int>(_variable, out var current);
            context.Set<int>(_variable, current + _value);
        }
    }
}
