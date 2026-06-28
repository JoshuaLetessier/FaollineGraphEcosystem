using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Compares two string parameters from the context for equality or inequality.
    /// Both absent keys default to empty string. Comparison is ordinal (case-sensitive).
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Conditions/String Compare (param vs param)", fileName = "StringCompareCondition")]
    public class StringCompareCondition : BaseCondition
    {
        [SerializeField, Tooltip("Left-hand side: context parameter key (string).")]
        private string _leftKey;
        [SerializeField, Tooltip("True = Equal, False = NotEqual.")]
        private bool _expectEqual = true;
        [SerializeField, Tooltip("Right-hand side: context parameter key (string).")]
        private string _rightKey;

        public string LeftKey { get => _leftKey; set => _leftKey = value; }
        public bool ExpectEqual { get => _expectEqual; set => _expectEqual = value; }
        public string RightKey { get => _rightKey; set => _rightKey = value; }

        public override bool Evaluate(BaseContext context)
        {
            context.TryGet<string>(_leftKey, out var left);
            context.TryGet<string>(_rightKey, out var right);
            var equal = string.Equals(left ?? "", right ?? "", System.StringComparison.Ordinal);
            return _expectEqual ? equal : !equal;
        }
    }
}
