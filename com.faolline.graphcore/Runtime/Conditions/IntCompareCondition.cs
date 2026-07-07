using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Compares two <see cref="VariableDef"/> (int) parameters from the context (e.g. <c>hp &lt; hpMax</c>).
    /// An unassigned or absent side defaults to 0.
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Conditions/Int Compare (param vs param)", fileName = "IntCompareCondition")]
    public class IntCompareCondition : BaseCondition, IVariableReferencing
    {
        [SerializeField, Tooltip("Left-hand side: parameter asset (type Int).")]
        private VariableDef _left;
        [SerializeField, Tooltip("Comparison operator.")]
        private ComparisonOperator _operator = ComparisonOperator.Equal;
        [SerializeField, Tooltip("Right-hand side: parameter asset (type Int).")]
        private VariableDef _right;

        public VariableDef Left { get => _left; set => _left = value; }
        public ComparisonOperator Operator { get => _operator; set => _operator = value; }
        public VariableDef Right { get => _right; set => _right = value; }

        /// <inheritdoc/>
        public IEnumerable<VariableReference> ReferencedVariables
        {
            get
            {
                if (_left != null)  yield return new VariableReference(_left,  VariableType.Int);
                if (_right != null) yield return new VariableReference(_right, VariableType.Int);
            }
        }

        public override bool Evaluate(BaseContext context)
        {
            int left = 0, right = 0;
            if (_left != null) context.TryGet<int>(_left, out left);
            if (_right != null) context.TryGet<int>(_right, out right);
            return _operator.Matches(left.CompareTo(right));
        }
    }
}
