using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Universal action: writes a named bool value into the execution context. Canonical home for the primitive
    /// bool setter — downstream libs that historically shipped their own (GraphStandard, GraphDialogue) now
    /// subclass this so there is a single implementation and no cross-namespace ambiguity.
    /// </summary>
    // No [CreateAssetMenu] — created via the inspector's object picker on node action fields.
    public class SetBoolAction : BaseAction
    {
        [SerializeField] private string _parameterKey;
        [SerializeField] private bool _value;

        /// <summary>The context parameter key to write.</summary>
        public string ParameterKey { get => _parameterKey; set => _parameterKey = value; }

        /// <summary>The bool value to set on the context parameter.</summary>
        public bool Value { get => _value; set => _value = value; }

        /// <inheritdoc/>
        public override void Execute(BaseContext context) => context.Set<bool>(_parameterKey, _value);
    }
}
