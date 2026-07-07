using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>Flips the bool at <see cref="Variable"/> (false→true, true→false).
    /// Defaults to true when the key is absent (toggling an unset flag sets it).</summary>
    [CreateAssetMenu(menuName = "Faolline/Actions/Toggle Bool", fileName = "ToggleBoolAction")]
    public class ToggleBoolAction : BaseAction, IVariableReferencing
    {
        [SerializeField, Tooltip("Variable asset to toggle. Drag a VariableDef (type Bool).")]
        private VariableDef _variable;

        /// <summary>The parameter asset to toggle.</summary>
        public VariableDef Variable { get => _variable; set => _variable = value; }

        /// <inheritdoc/>
        public IEnumerable<VariableReference> ReferencedVariables { get { if (_variable != null) yield return new VariableReference(_variable, VariableType.Bool); } }

        /// <inheritdoc/>
        public override void Execute(BaseContext context)
        {
            if (_variable == null) return;
            context.TryGet<bool>(_variable, out var current);
            context.Set<bool>(_variable, !current);
        }
    }
}
