using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>Universal action: writes a named int value into the execution context. Canonical home in GraphCore;
    /// downstream libs subclass this.</summary>
    [CreateAssetMenu(menuName = "Faolline/Actions/Set Int", fileName = "SetIntAction")]
    public class SetIntAction : BaseAction
    {
        [SerializeField, Tooltip("Context parameter key to write. Must match a key declared on the graph's Parameters list.")]
        private string _parameterKey;
        [SerializeField, Tooltip("The int value written to the context parameter.")]
        private int _value;

        /// <summary>The context parameter key to write.</summary>
        public string ParameterKey { get => _parameterKey; set => _parameterKey = value; }

        /// <summary>The int value to set on the context parameter.</summary>
        public int Value { get => _value; set => _value = value; }

        /// <inheritdoc/>
        public override void Execute(BaseContext context) => context.Set<int>(_parameterKey, _value);
    }
}
