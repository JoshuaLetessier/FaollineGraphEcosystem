using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphDialogue
{
    /// <summary>
    /// Reads a named int parameter and compares it to an expected value using a
    /// <see cref="ComparisonOperator"/>. Returns false (with a warning) when the key is absent.
    /// </summary>
    [CreateAssetMenu(menuName = "GraphDialogue/Conditions/Int Condition", fileName = "IntCondition")]
    public class IntCondition : BaseCondition
    {
        [SerializeField] private string _parameterKey;
        [SerializeField] private ComparisonOperator _operator = ComparisonOperator.Equal;
        [SerializeField] private int _expectedValue;

        public string ParameterKey { get => _parameterKey; set => _parameterKey = value; }
        public ComparisonOperator Operator { get => _operator; set => _operator = value; }
        public int ExpectedValue { get => _expectedValue; set => _expectedValue = value; }

        public override bool Evaluate(BaseContext context)
        {
            if (!context.TryGet<int>(_parameterKey, out var value))
            {
                Debug.LogWarning($"[GraphDialogue] Condition: parameter key '{_parameterKey}' not found in context — evaluating to false.");
                return false;
            }

            return _operator switch
            {
                ComparisonOperator.Equal          => value == _expectedValue,
                ComparisonOperator.NotEqual       => value != _expectedValue,
                ComparisonOperator.Less           => value <  _expectedValue,
                ComparisonOperator.LessOrEqual    => value <= _expectedValue,
                ComparisonOperator.Greater        => value >  _expectedValue,
                ComparisonOperator.GreaterOrEqual => value >= _expectedValue,
                _                                 => false
            };
        }
    }
}
