using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>Universal action: writes a fixed float value into the execution context under a
    /// <see cref="ParameterName"/> asset's stable GUID key. Canonical home in GraphCore; downstream libs subclass
    /// this.</summary>
    [CreateAssetMenu(menuName = "Faolline/Actions/Set Float", fileName = "SetFloatAction")]
    public class SetFloatAction : BaseAction, IParameterReferencing
    {
        [SerializeField, Tooltip("Parameter asset to write. Drag a ParameterName (type Float); its stable GUID is the context key.")]
        private ParameterName _parameter;
        [SerializeField, Tooltip("The float value written to the parameter.")]
        private float _value;

        /// <summary>The parameter asset to write.</summary>
        public ParameterName Parameter { get => _parameter; set => _parameter = value; }

        /// <summary>The float value to set on the parameter.</summary>
        public float Value { get => _value; set => _value = value; }

        /// <inheritdoc/>
        public IEnumerable<ParameterReference> ReferencedParameters { get { if (_parameter != null) yield return new ParameterReference(_parameter, ParameterType.Float); } }

        /// <inheritdoc/>
        public override void Execute(BaseContext context)
        {
            if (_parameter == null) return;
            context.Set<float>(_parameter, _value);
        }
    }
}
