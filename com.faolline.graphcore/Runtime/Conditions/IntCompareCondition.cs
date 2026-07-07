using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Compares two <see cref="ParameterName"/> (int) parameters from the context (e.g. <c>hp &lt; hpMax</c>).
    /// An unassigned or absent side defaults to 0.
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Conditions/Int Compare (param vs param)", fileName = "IntCompareCondition")]
    public class IntCompareCondition : BaseCondition, IParameterReferencing
    {
        [SerializeField, Tooltip("Left-hand side: parameter asset (type Int).")]
        private ParameterName _left;
        [SerializeField, Tooltip("Comparison operator.")]
        private ComparisonOperator _operator = ComparisonOperator.Equal;
        [SerializeField, Tooltip("Right-hand side: parameter asset (type Int).")]
        private ParameterName _right;

        public ParameterName Left { get => _left; set => _left = value; }
        public ComparisonOperator Operator { get => _operator; set => _operator = value; }
        public ParameterName Right { get => _right; set => _right = value; }

        /// <inheritdoc/>
        public IEnumerable<ParameterReference> ReferencedParameters
        {
            get
            {
                if (_left != null)  yield return new ParameterReference(_left,  ParameterType.Int);
                if (_right != null) yield return new ParameterReference(_right, ParameterType.Int);
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
