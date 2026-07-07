using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Compares two <see cref="VariableDef"/> (float) parameters from the context (e.g. <c>speed &gt; maxSpeed</c>).
    /// An unassigned or absent side defaults to 0.
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Conditions/Float Compare (param vs param)", fileName = "FloatCompareCondition")]
    public class FloatCompareCondition : BaseCondition, IVariableReferencing
    {
        [SerializeField, Tooltip("Left-hand side: parameter asset (type Float).")]
        private VariableDef _left;
        [SerializeField, Tooltip("Comparison operator.")]
        private ComparisonOperator _operator = ComparisonOperator.Equal;
        [SerializeField, Tooltip("Right-hand side: parameter asset (type Float).")]
        private VariableDef _right;

        public VariableDef Left { get => _left; set => _left = value; }
        public ComparisonOperator Operator { get => _operator; set => _operator = value; }
        public VariableDef Right { get => _right; set => _right = value; }

        /// <inheritdoc/>
        public IEnumerable<VariableReference> ReferencedVariables
        {
            get
            {
                if (_left != null)  yield return new VariableReference(_left,  VariableType.Float);
                if (_right != null) yield return new VariableReference(_right, VariableType.Float);
            }
        }

        public override bool Evaluate(BaseContext context)
        {
            float left = 0f, right = 0f;
            if (_left != null) context.TryGet<float>(_left, out left);
            if (_right != null) context.TryGet<float>(_right, out right);
            return _operator.Matches(left.CompareTo(right));
        }
    }
}
