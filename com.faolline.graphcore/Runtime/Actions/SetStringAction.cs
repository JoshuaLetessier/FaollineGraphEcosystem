using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>Universal action: writes a named string value into the execution context. Canonical home in
    /// GraphCore; downstream libs subclass this.</summary>
    [CreateAssetMenu(menuName = "Faolline/Actions/Set String", fileName = "SetStringAction")]
    public class SetStringAction : BaseAction
    {
        [SerializeField] private string _parameterKey;
        [SerializeField] private string _value;

        /// <summary>The context parameter key to write.</summary>
        public string ParameterKey { get => _parameterKey; set => _parameterKey = value; }

        /// <summary>The string value to set on the context parameter.</summary>
        public string Value { get => _value; set => _value = value; }

        /// <inheritdoc/>
        public override void Execute(BaseContext context) => context.Set<string>(_parameterKey, _value);
    }
}
