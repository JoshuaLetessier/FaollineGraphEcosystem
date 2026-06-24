using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Universal action: writes a named bool value into the execution context. Canonical home for the primitive
    /// bool setter — downstream libs that historically shipped their own (GraphStandard, GraphDialogue) now
    /// subclass this so there is a single implementation and no cross-namespace ambiguity.
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Actions/Set Bool", fileName = "SetBoolAction")]
    public class SetBoolAction : BaseAction
    {
        [SerializeField, Tooltip("Context parameter key to write. Must match a key declared on the graph's Parameters list.")]
        private string _parameterKey;
        [SerializeField, Tooltip("The bool value written to the context parameter.")]
        private bool _value;

        /// <summary>The context parameter key to write.</summary>
        public string ParameterKey { get => _parameterKey; set => _parameterKey = value; }

        /// <summary>The bool value to set on the context parameter.</summary>
        public bool Value { get => _value; set => _value = value; }

        /// <inheritdoc/>
        public override void Execute(BaseContext context) => context.Set<bool>(_parameterKey, _value);
    }
}
