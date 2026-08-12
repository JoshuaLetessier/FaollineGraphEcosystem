using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphLogging;

namespace Faolline.GraphTest
{
    /// <summary>
    /// Condition that reads a named bool parameter from the context and compares it to an expected value.
    /// Returns false (with a warning) when the parameter key is not found in the context.
    /// </summary>
    [CreateAssetMenu(menuName = "GraphTest/Conditions/Bool Condition", fileName = "BoolCondition")]
    public class TestBoolCondition : BaseCondition
    {
        [SerializeField] private string _parameterKey;
        [SerializeField] private bool _expectedValue;

        /// <summary>The context parameter key to evaluate.</summary>
        public string ParameterKey { get => _parameterKey; set => _parameterKey = value; }

        /// <summary>The bool value this condition expects to find in the context.</summary>
        public bool ExpectedValue { get => _expectedValue; set => _expectedValue = value; }

        /// <inheritdoc/>
        public override bool Evaluate(BaseContext context)
        {
            if (!context.TryGet<bool>(_parameterKey, out var value))
            {
                Logging.Warning("GraphTest", $"[GraphTest] Condition: parameter key '{_parameterKey}' not found in context — evaluating to false.");
                return false;
            }
            return value == _expectedValue;
        }
    }
}
