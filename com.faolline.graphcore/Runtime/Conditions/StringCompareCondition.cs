using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Compares two <see cref="VariableDef"/> (string) parameters from the context for equality or inequality.
    /// An unassigned or absent side defaults to empty string. Comparison is ordinal (case-sensitive).
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Conditions/String Compare (param vs param)", fileName = "StringCompareCondition")]
    public class StringCompareCondition : BaseCondition, IVariableReferencing
    {
        [SerializeField, Tooltip("Left-hand side: parameter asset (type String).")]
        private VariableDef _left;
        [SerializeField, Tooltip("True = Equal, False = NotEqual.")]
        private bool _expectEqual = true;
        [SerializeField, Tooltip("Right-hand side: parameter asset (type String).")]
        private VariableDef _right;

        public VariableDef Left { get => _left; set => _left = value; }
        public bool ExpectEqual { get => _expectEqual; set => _expectEqual = value; }
        public VariableDef Right { get => _right; set => _right = value; }

        /// <inheritdoc/>
        public IEnumerable<VariableReference> ReferencedVariables
        {
            get
            {
                if (_left != null)  yield return new VariableReference(_left,  VariableType.String);
                if (_right != null) yield return new VariableReference(_right, VariableType.String);
            }
        }

        public override bool Evaluate(BaseContext context)
        {
            string left = null, right = null;
            if (_left != null) context.TryGet<string>(_left, out left);
            if (_right != null) context.TryGet<string>(_right, out right);
            var equal = string.Equals(left ?? "", right ?? "", System.StringComparison.Ordinal);
            return _expectEqual ? equal : !equal;
        }
    }
}
