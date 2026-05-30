using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphTest
{
    /// <summary>Action that writes a named float value into the execution context when executed.</summary>
    [CreateAssetMenu(menuName = "GraphTest/Actions/Set Float Action", fileName = "SetFloatAction")]
    public class TestSetFloatAction : BaseAction
    {
        [SerializeField] private string _parameterKey;
        [SerializeField] private float _value;

        /// <summary>The context parameter key to write.</summary>
        public string ParameterKey { get => _parameterKey; set => _parameterKey = value; }

        /// <summary>The float value to set on the context parameter.</summary>
        public float Value { get => _value; set => _value = value; }

        /// <inheritdoc/>
        public override void Execute(BaseContext context)
        {
            context.Set<float>(_parameterKey, _value);
        }
    }
}
