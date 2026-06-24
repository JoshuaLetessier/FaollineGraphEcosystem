using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>Adds <see cref="Value"/> to the current int at <see cref="ParameterKey"/> (defaults to 0 when
    /// absent). Use for rewards, costs, and counters where the result is relative to the current value.</summary>
    [CreateAssetMenu(menuName = "Faolline/Actions/Add Int", fileName = "AddIntAction")]
    public class AddIntAction : BaseAction
    {
        [SerializeField, Tooltip("Context parameter key to modify. Must match a key declared on the graph's Parameters list.")]
        private string _parameterKey;
        [SerializeField, Tooltip("The int value added to the current value (use a negative number to subtract).")]
        private int _value;

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
