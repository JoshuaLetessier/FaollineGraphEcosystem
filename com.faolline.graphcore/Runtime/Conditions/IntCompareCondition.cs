using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Compares two int parameters from the context (e.g. <c>hp &lt; hpMax</c>).
    /// Both absent keys default to 0.
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Conditions/Int Compare (param vs param)", fileName = "IntCompareCondition")]
    public class IntCompareCondition : BaseCondition
    {
        [SerializeField, Tooltip("Left-hand side: context parameter key (int).")]
        private string _leftKey;
        [SerializeField, Tooltip("Comparison operator.")]
        private ComparisonOperator _operator = ComparisonOperator.Equal;
        [SerializeField, Tooltip("Right-hand side: context parameter key (int).")]
        private string _rightKey;

        public string LeftKey { get => _leftKey; set => _leftKey = value; }
        public ComparisonOperator Operator { get => _operator; set => _operator = value; }
        public string RightKey { get => _rightKey; set => _rightKey = value; }

        public override bool Evaluate(BaseContext context)
        {
            context.TryGet<int>(_leftKey, out var left);
            context.TryGet<int>(_rightKey, out var right);
            return _operator.Matches(left.CompareTo(right));
        }
    }
}
