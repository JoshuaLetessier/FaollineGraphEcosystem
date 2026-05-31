using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphDialogue
{
    /// <summary>
    /// Reads a named bool parameter from the context and compares it to an expected value.
    /// Returns false (with a warning) when the key is absent — never throws.
    /// </summary>
    [CreateAssetMenu(menuName = "GraphDialogue/Conditions/Bool Condition", fileName = "BoolCondition")]
    public class BoolCondition : BaseCondition
    {
        [SerializeField] private string _parameterKey;
        [SerializeField] private bool _expectedValue;

        public string ParameterKey { get => _parameterKey; set => _parameterKey = value; }
        public bool ExpectedValue { get => _expectedValue; set => _expectedValue = value; }

        public override bool Evaluate(BaseContext context)
        {
            if (!context.TryGet<bool>(_parameterKey, out var value))
            {
                Debug.LogWarning($"[GraphDialogue] Condition: parameter key '{_parameterKey}' not found in context — evaluating to false.");
                return false;
            }
            return value == _expectedValue;
        }
    }
}
