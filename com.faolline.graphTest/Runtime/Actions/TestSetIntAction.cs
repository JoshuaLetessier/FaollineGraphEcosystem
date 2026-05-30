using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphTest
{
    /// <summary>Action that writes a named int value into the execution context when executed.</summary>
    [CreateAssetMenu(menuName = "GraphTest/Actions/Set Int Action", fileName = "SetIntAction")]
    public class TestSetIntAction : BaseAction
    {
        [SerializeField] private string _parameterKey;
        [SerializeField] private int _value;

        /// <summary>The context parameter key to write.</summary>
        public string ParameterKey { get => _parameterKey; set => _parameterKey = value; }

        /// <summary>The int value to set on the context parameter.</summary>
        public int Value { get => _value; set => _value = value; }

        /// <inheritdoc/>
        public override void Execute(BaseContext context)
        {
            context.Set<int>(_parameterKey, _value);
        }
    }
}
