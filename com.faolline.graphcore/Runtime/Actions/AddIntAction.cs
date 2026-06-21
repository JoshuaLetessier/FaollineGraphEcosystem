using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>Adds <see cref="Value"/> to the current int at <see cref="ParameterKey"/> (defaults to 0 when
    /// absent). Use for rewards, costs, and counters where the result is relative to the current value.</summary>
    [CreateAssetMenu(menuName = "GraphCore/Actions/Add Int Action", fileName = "AddIntAction")]
    public class AddIntAction : BaseAction
    {
        [SerializeField] private string _parameterKey;
        [SerializeField] private int _value;

        /// <summary>The context parameter key to modify.</summary>
        public string ParameterKey { get => _parameterKey; set => _parameterKey = value; }

        /// <summary>The int value to add (negative to subtract).</summary>
        public int Value { get => _value; set => _value = value; }

        /// <inheritdoc/>
        public override void Execute(BaseContext context)
        {
            context.TryGet<int>(_parameterKey, out var current);
            context.Set<int>(_parameterKey, current + _value);
        }
    }
}
