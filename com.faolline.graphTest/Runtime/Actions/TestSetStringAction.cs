using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphTest
{
    /// <summary>Action that writes a named string value into the execution context when executed.</summary>
    [CreateAssetMenu(menuName = "GraphTest/Actions/Set String Action", fileName = "SetStringAction")]
    public class TestSetStringAction : BaseAction
    {
        [SerializeField] private string _parameterKey;
        [SerializeField] private string _value;

        /// <summary>The context parameter key to write.</summary>
        public string ParameterKey { get => _parameterKey; set => _parameterKey = value; }

        /// <summary>The string value to set on the context parameter.</summary>
        public string Value { get => _value; set => _value = value; }

        /// <inheritdoc/>
        public override void Execute(BaseContext context)
        {
            context.Set<string>(_parameterKey, _value ?? string.Empty);
        }
    }
}
