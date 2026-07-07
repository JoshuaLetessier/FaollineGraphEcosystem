using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>Adds <see cref="Value"/> to the current float at <see cref="Parameter"/> (defaults to 0 when
    /// absent). Use for continuous modifiers (health, progress bars) where the result is relative.</summary>
    [CreateAssetMenu(menuName = "Faolline/Actions/Add Float", fileName = "AddFloatAction")]
    public class AddFloatAction : BaseAction, IParameterReferencing
    {
        [SerializeField, Tooltip("Parameter asset to modify. Drag a ParameterName (type Float); its stable GUID is the context key.")]
        private ParameterName _parameter;
        [SerializeField, Tooltip("The float value added to the current value (use a negative number to subtract).")]
        private float _value;

        /// <summary>The parameter asset to modify.</summary>
        public ParameterName Parameter { get => _parameter; set => _parameter = value; }

        /// <summary>The float value to add (negative to subtract).</summary>
        public float Value { get => _value; set => _value = value; }

        /// <inheritdoc/>
        public IEnumerable<ParameterReference> ReferencedParameters { get { if (_parameter != null) yield return new ParameterReference(_parameter, ParameterType.Float); } }

        /// <inheritdoc/>
        public override void Execute(BaseContext context)
        {
            if (_parameter == null) return;
            context.TryGet<float>(_parameter, out var current);
            context.Set<float>(_parameter, current + _value);
        }
    }
}
