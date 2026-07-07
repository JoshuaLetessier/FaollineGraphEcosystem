using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>Adds <see cref="Value"/> to the current int at <see cref="Parameter"/> (defaults to 0 when
    /// absent). Use for rewards, costs, and counters where the result is relative to the current value.</summary>
    [CreateAssetMenu(menuName = "Faolline/Actions/Add Int", fileName = "AddIntAction")]
    public class AddIntAction : BaseAction, IParameterReferencing
    {
        [SerializeField, Tooltip("Parameter asset to modify. Drag a ParameterName (type Int); its stable GUID is the context key.")]
        private ParameterName _parameter;
        [SerializeField, Tooltip("The int value added to the current value (use a negative number to subtract).")]
        private int _value;

        /// <summary>The parameter asset to modify.</summary>
        public ParameterName Parameter { get => _parameter; set => _parameter = value; }

        /// <summary>The int value to add (negative to subtract).</summary>
        public int Value { get => _value; set => _value = value; }

        /// <inheritdoc/>
        public IEnumerable<ParameterReference> ReferencedParameters { get { if (_parameter != null) yield return new ParameterReference(_parameter, ParameterType.Int); } }

        /// <inheritdoc/>
        public override void Execute(BaseContext context)
        {
            if (_parameter == null) return;
            context.TryGet<int>(_parameter, out var current);
            context.Set<int>(_parameter, current + _value);
        }
    }
}
