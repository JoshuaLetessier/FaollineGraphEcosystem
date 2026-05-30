using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.StarterGraph
{
    /// <summary>
    /// Action that writes a named bool value into the execution context when executed.
    /// Use to simulate state changes that downstream conditions can react to.
    /// </summary>
    [CreateAssetMenu(menuName = "StarterGraph/Actions/Set Bool Action", fileName = "SetBoolAction")]
    public class StarterSetBoolAction : BaseAction
    {
        [SerializeField] private string _parameterKey;
        [SerializeField] private bool _value;

        /// <summary>The context parameter key to write.</summary>
        public string ParameterKey { get => _parameterKey; set => _parameterKey = value; }

        /// <summary>The bool value to set on the context parameter.</summary>
        public bool Value { get => _value; set => _value = value; }

        /// <inheritdoc/>
        public override void Execute(BaseContext context)
        {
            context.Set<bool>(_parameterKey, _value);
        }
    }
}
