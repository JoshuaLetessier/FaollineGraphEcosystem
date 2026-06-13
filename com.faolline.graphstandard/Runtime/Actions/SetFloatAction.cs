using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphStandard
{
    /// <summary>Domain-neutral action: writes a named float value into the execution context.</summary>
    [CreateAssetMenu(menuName = "GraphStandard/Actions/Set Float Action", fileName = "SetFloatAction")]
    public class SetFloatAction : BaseAction
    {
        [SerializeField] private string _parameterKey;
        [SerializeField] private float _value;

        /// <summary>The context parameter key to write.</summary>
        public string ParameterKey { get => _parameterKey; set => _parameterKey = value; }

        /// <summary>The float value to set on the context parameter.</summary>
        public float Value { get => _value; set => _value = value; }

        /// <inheritdoc/>
        public override void Execute(BaseContext context) => context.Set<float>(_parameterKey, _value);
    }
}
