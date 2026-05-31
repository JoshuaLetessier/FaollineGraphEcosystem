using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphDialogue
{
    /// <summary>
    /// Reads a named string parameter and compares it to an expected value. Supports negation.
    /// Returns false (with a warning) when the key is absent — never throws.
    /// </summary>
    [CreateAssetMenu(menuName = "GraphDialogue/Conditions/String Condition", fileName = "StringCondition")]
    public class StringCondition : BaseCondition
    {
        [SerializeField] private string _parameterKey;
        [SerializeField] private string _expectedValue;
        [SerializeField] private bool _negate;

        public string ParameterKey { get => _parameterKey; set => _parameterKey = value; }
        public string ExpectedValue { get => _expectedValue; set => _expectedValue = value; }
        public bool Negate { get => _negate; set => _negate = value; }

        public override bool Evaluate(BaseContext context)
        {
            if (!context.TryGet<string>(_parameterKey, out var value))
            {
                Debug.LogWarning($"[GraphDialogue] Condition: parameter key '{_parameterKey}' not found in context — evaluating to false.");
                return false;
            }

            bool equal = value == _expectedValue;
            return _negate ? !equal : equal;
        }
    }
}
