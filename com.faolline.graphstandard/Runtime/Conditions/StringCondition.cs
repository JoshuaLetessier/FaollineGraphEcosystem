using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphStandard
{
    /// <summary>
    /// Domain-neutral condition: reads a named string parameter from the context and compares it for equality to
    /// an expected value (optionally negated). False (with a warning) when the key is absent or not a string.
    /// </summary>
    [CreateAssetMenu(menuName = "GraphStandard/Conditions/String Condition", fileName = "StringCondition")]
    public class StringCondition : BaseCondition
    {
        [SerializeField] private string _parameterKey;
        [SerializeField] private string _expectedValue;
        [SerializeField] private bool _negate;

        /// <summary>The context parameter key to evaluate.</summary>
        public string ParameterKey { get => _parameterKey; set => _parameterKey = value; }

        /// <summary>The string value this condition compares against.</summary>
        public string ExpectedValue { get => _expectedValue; set => _expectedValue = value; }

        /// <summary>When true, the condition passes on inequality instead of equality.</summary>
        public bool Negate { get => _negate; set => _negate = value; }

        /// <inheritdoc/>
        public override bool Evaluate(BaseContext context)
        {
            string value;
            try
            {
                if (!context.TryGet<string>(_parameterKey, out value))
                {
                    Debug.LogWarning($"[GraphStandard] StringCondition: parameter '{_parameterKey}' not found — false.");
                    return false;
                }
            }
            catch (System.InvalidCastException)
            {
                Debug.LogWarning($"[GraphStandard] StringCondition: parameter '{_parameterKey}' is not a string — false.");
                return false;
            }
            bool equal = value == _expectedValue;
            return _negate ? !equal : equal;
        }
    }
}
