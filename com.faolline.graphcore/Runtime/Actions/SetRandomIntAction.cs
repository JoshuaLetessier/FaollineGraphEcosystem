using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>Sets <see cref="Variable"/> to a random int in [<see cref="Min"/>, <see cref="Max"/>]
    /// (inclusive on both ends). Useful for dice rolls, random encounter selection, or branching variety.</summary>
    [CreateAssetMenu(menuName = "Faolline/Actions/Set Random Int", fileName = "SetRandomIntAction")]
    public class SetRandomIntAction : BaseAction, IVariableReferencing
    {
        [SerializeField, Tooltip("Variable asset to write the random value to. Drag a VariableDef (type Int).")]
        private VariableDef _variable;
        [SerializeField, Tooltip("Minimum value (inclusive).")]
        private int _min;
        [SerializeField, Tooltip("Maximum value (inclusive).")]
        private int _max = 10;

        /// <summary>The parameter asset to write the random value to.</summary>
        public VariableDef Variable { get => _variable; set => _variable = value; }
        public int Min { get => _min; set => _min = value; }
        public int Max { get => _max; set => _max = value; }

        /// <inheritdoc/>
        public IEnumerable<VariableReference> ReferencedVariables { get { if (_variable != null) yield return new VariableReference(_variable, VariableType.Int); } }

        /// <inheritdoc/>
        public override void Execute(BaseContext context)
        {
            if (_variable == null) return;
            context.Set<int>(_variable, Random.Range(_min, _max + 1));
        }
    }
}
