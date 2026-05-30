using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.StarterGraph
{
    /// <summary>
    /// Condition that reads a named int parameter from the context and compares it to an expected
    /// value via a <see cref="ComparisonOperator"/>. Returns false (with a warning) when the key is
    /// absent or holds a non-int value.
    /// </summary>
    [CreateAssetMenu(menuName = "StarterGraph/Conditions/Int Condition", fileName = "IntCondition")]
    public class StarterIntCondition : BaseCondition
    {
        [SerializeField] private string _parameterKey;
        [SerializeField] private ComparisonOperator _operator = ComparisonOperator.Equal;
        [SerializeField] private int _expectedValue;

        /// <summary>The context parameter key to evaluate.</summary>
        public string ParameterKey { get => _parameterKey; set => _parameterKey = value; }

        /// <summary>The comparison applied between the parameter value and <see cref="ExpectedValue"/>.</summary>
        public ComparisonOperator Operator { get => _operator; set => _operator = value; }

        /// <summary>The int value this condition compares against.</summary>
        public int ExpectedValue { get => _expectedValue; set => _expectedValue = value; }

        /// <inheritdoc/>
        public override bool Evaluate(BaseContext context)
        {
            int value;
            try
            {
                if (!context.TryGet<int>(_parameterKey, out value))
                {
                    Debug.LogWarning($"[StarterGraph] Condition: int parameter '{_parameterKey}' not found in context — evaluating to false.");
                    return false;
                }
            }
            catch (System.InvalidCastException)
            {
                Debug.LogWarning($"[StarterGraph] Condition: parameter '{_parameterKey}' is not an int — evaluating to false.");
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
