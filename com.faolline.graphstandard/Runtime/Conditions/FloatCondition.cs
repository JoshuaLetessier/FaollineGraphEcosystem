using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphStandard
{
    /// <summary>
    /// Domain-neutral condition: reads a named float parameter from the context and compares it to an expected
    /// value via a <see cref="ComparisonOperator"/>. False (with a warning) when the key is absent or not a float.
    /// </summary>
    [CreateAssetMenu(menuName = "GraphStandard/Conditions/Float Condition", fileName = "FloatCondition")]
    public class FloatCondition : BaseCondition
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
                    Debug.LogWarning($"[GraphStandard] FloatCondition: parameter '{_parameterKey}' not found — false.");
                    return false;
                }
            }
            catch (System.InvalidCastException)
            {
                Debug.LogWarning($"[GraphStandard] FloatCondition: parameter '{_parameterKey}' is not a float — false.");
                return false;
            }
            return _operator.Matches(value.CompareTo(_expectedValue));
        }
    }
}
