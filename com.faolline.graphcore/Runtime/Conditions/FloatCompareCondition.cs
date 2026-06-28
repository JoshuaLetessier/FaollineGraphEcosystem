using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Compares two float parameters from the context (e.g. <c>speed &gt; maxSpeed</c>).
    /// Both absent keys default to 0.
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Conditions/Float Compare (param vs param)", fileName = "FloatCompareCondition")]
    public class FloatCompareCondition : BaseCondition
    {
        [SerializeField, Tooltip("Left-hand side: context parameter key (float).")]
        private string _leftKey;
        [SerializeField, Tooltip("Comparison operator.")]
        private ComparisonOperator _operator = ComparisonOperator.Equal;
        [SerializeField, Tooltip("Right-hand side: context parameter key (float).")]
        private string _rightKey;

        public string LeftKey { get => _leftKey; set => _leftKey = value; }
        public ComparisonOperator Operator { get => _operator; set => _operator = value; }
        public string RightKey { get => _rightKey; set => _rightKey = value; }

        public override bool Evaluate(BaseContext context)
        {
            context.TryGet<float>(_leftKey, out var left);
            context.TryGet<float>(_rightKey, out var right);
            return _operator.Matches(left.CompareTo(right));
        }
    }
}
