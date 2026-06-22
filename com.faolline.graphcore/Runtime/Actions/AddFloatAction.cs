using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>Adds <see cref="Value"/> to the current float at <see cref="ParameterKey"/> (defaults to 0 when
    /// absent). Use for continuous modifiers (health, progress bars) where the result is relative.</summary>
    // No [CreateAssetMenu] — created via the inspector's object picker on node action fields.
    public class AddFloatAction : BaseAction
    {
        [SerializeField] private string _parameterKey;
        [SerializeField] private float _value;

        /// <summary>The context parameter key to modify.</summary>
        public string ParameterKey { get => _parameterKey; set => _parameterKey = value; }

        /// <summary>The float value to add (negative to subtract).</summary>
        public float Value { get => _value; set => _value = value; }

        /// <inheritdoc/>
        public override void Execute(BaseContext context)
        {
            context.TryGet<float>(_parameterKey, out var current);
            context.Set<float>(_parameterKey, current + _value);
        }
    }
}
