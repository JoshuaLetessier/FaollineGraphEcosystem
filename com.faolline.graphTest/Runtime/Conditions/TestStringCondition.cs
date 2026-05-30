using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphTest
{
    /// <summary>
    /// Condition that reads a named string parameter from the context and compares it for equality
    /// to an expected value (optionally negated). Returns false (with a warning) when the key is
    /// absent or holds a non-string value.
    /// </summary>
    [CreateAssetMenu(menuName = "GraphTest/Conditions/String Condition", fileName = "StringCondition")]
    public class TestStringCondition : BaseCondition
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
                    Debug.LogWarning($"[GraphTest] Condition: string parameter '{_parameterKey}' not found in context — evaluating to false.");
                    return false;
                }
            }
            catch (System.InvalidCastException)
            {
                Debug.LogWarning($"[GraphTest] Condition: parameter '{_parameterKey}' is not a string — evaluating to false.");
                return false;
            }
            bool equal = value == _expectedValue;
            return _negate ? !equal : equal;
        }
    }
}
