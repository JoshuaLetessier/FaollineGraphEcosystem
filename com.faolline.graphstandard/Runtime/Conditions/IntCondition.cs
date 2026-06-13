using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphStandard
{
    /// <summary>
    /// Domain-neutral condition: reads a named int parameter from the context and compares it to an expected
    /// value via a <see cref="ComparisonOperator"/>. False (with a warning) when the key is absent or not an int.
    /// </summary>
    [CreateAssetMenu(menuName = "GraphStandard/Conditions/Int Condition", fileName = "IntCondition")]
    public class IntCondition : BaseCondition
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
                    Debug.LogWarning($"[GraphStandard] IntCondition: parameter '{_parameterKey}' not found — false.");
                    return false;
                }
            }
            catch (System.InvalidCastException)
            {
                Debug.LogWarning($"[GraphStandard] IntCondition: parameter '{_parameterKey}' is not an int — false.");
                return false;
            }
            return _operator.Matches(value.CompareTo(_expectedValue));
        }
    }
}
