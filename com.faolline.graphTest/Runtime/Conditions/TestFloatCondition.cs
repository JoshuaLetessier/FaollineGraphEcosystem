using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphTest
{
    /// <summary>
    /// Condition that reads a named float parameter from the context and compares it to an expected
    /// value via a <see cref="ComparisonOperator"/>. Returns false (with a warning) when the key is
    /// absent or holds a non-float value.
    /// </summary>
    [CreateAssetMenu(menuName = "GraphTest/Conditions/Float Condition", fileName = "FloatCondition")]
    public class TestFloatCondition : BaseCondition
    {
        [SerializeField] private string _parameterKey;
        [SerializeField] private ComparisonOperator _operator = ComparisonOperator.Equal;
        [SerializeField] private float _expectedValue;

        /// <summary>The context parameter key to evaluate.</summary>
        public string ParameterKey { get => _parameterKey; set => _parameterKey = value; }

        /// <summary>The comparison applied between the parameter value and <see cref="ExpectedValue"/>.</summary>
        public ComparisonOperator Operator { get => _operator; set => _operator = value; }

        /// <summary>The float value this condition compares against.</summary>
        public float ExpectedValue { get => _expectedValue; set => _expectedValue = value; }

        /// <inheritdoc/>
        public override bool Evaluate(BaseContext context)
        {
            float value;
            try
            {
                if (!context.TryGet<float>(_parameterKey, out value))
                {
                    Debug.LogWarning($"[GraphTest] Condition: float parameter '{_parameterKey}' not found in context — evaluating to false.");
                    return false;
                }
            }
            catch (System.InvalidCastException)
            {
                Debug.LogWarning($"[GraphTest] Condition: parameter '{_parameterKey}' is not a float — evaluating to false.");
                return false;
            }
            return Matches(value.CompareTo(_expectedValue), _operator);
        }

        private static bool Matches(int comparison, ComparisonOperator op)
        {
            switch (op)
            {
                case ComparisonOperator.Equal:          return comparison == 0;
                case ComparisonOperator.NotEqual:       return comparison != 0;
                case ComparisonOperator.Less:           return comparison < 0;
                case ComparisonOperator.LessOrEqual:    return comparison <= 0;
                case ComparisonOperator.Greater:        return comparison > 0;
                case ComparisonOperator.GreaterOrEqual: return comparison >= 0;
                default:                                return false;
            }
        }
    }
}
