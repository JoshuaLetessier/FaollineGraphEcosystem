using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Universal condition: reads a named string parameter from the context and compares it for equality to an
    /// expected value (optionally negated). False (silently) when the key is absent — set <see cref="WarnOnMissing"/>
    /// to warn instead; a wrong-typed value always warns (a real misconfiguration). Canonical home in GraphCore;
    /// downstream libs subclass this.
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Conditions/String", fileName = "StringCondition")]
    public class StringCondition : BaseCondition
    {
        [SerializeField, Tooltip("Context parameter key to read and compare.")]
        private string _parameterKey;
        [SerializeField, Tooltip("The string value this condition compares against (case-sensitive equality).")]
        private string _expectedValue;
        [SerializeField, Tooltip("When enabled, the condition passes on inequality instead of equality.")]
        private bool _negate;
        [SerializeField, Tooltip("When enabled, logs a warning if the parameter key is absent from the context. When disabled (default), absent keys silently evaluate to false.")]
        private bool _warnOnMissing;

        /// <summary>The context parameter key to evaluate.</summary>
        public string ParameterKey { get => _parameterKey; set => _parameterKey = value; }

        /// <summary>The string value this condition compares against.</summary>
        public string ExpectedValue { get => _expectedValue; set => _expectedValue = value; }

        /// <summary>When true, the condition passes on inequality instead of equality.</summary>
        public bool Negate { get => _negate; set => _negate = value; }

        /// <summary>When true, logs a warning if the key is absent (default false — absent reads as false silently).</summary>
        public bool WarnOnMissing { get => _warnOnMissing; set => _warnOnMissing = value; }

        /// <inheritdoc/>
        public override bool Evaluate(BaseContext context)
        {
            string value;
            try
            {
                if (!context.TryGet<string>(_parameterKey, out value))
                {
                    if (_warnOnMissing)
                        Debug.LogWarning($"[GraphCore] StringCondition: parameter '{_parameterKey}' not found — false.");
                    return false;
                }
            }
            catch (System.InvalidCastException)
            {
                Debug.LogWarning($"[GraphCore] StringCondition: parameter '{_parameterKey}' is not a string — false.");
                return false;
            }
            bool equal = value == _expectedValue;
            return _negate ? !equal : equal;
        }
    }
}
