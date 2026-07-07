using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>Universal action: writes a fixed int value into the execution context under a
    /// <see cref="ParameterName"/> asset's stable GUID key. Canonical home in GraphCore; downstream libs subclass
    /// this.</summary>
    [CreateAssetMenu(menuName = "Faolline/Actions/Set Int", fileName = "SetIntAction")]
    public class SetIntAction : BaseAction, IParameterReferencing
    {
        [SerializeField, Tooltip("Parameter asset to write. Drag a ParameterName (type Int); its stable GUID is the context key.")]
        private ParameterName _parameter;
        [SerializeField, Tooltip("The int value written to the parameter.")]
        private int _value;

        /// <summary>The parameter asset to write.</summary>
        public ParameterName Parameter { get => _parameter; set => _parameter = value; }

        /// <summary>The int value to set on the parameter.</summary>
        public int Value { get => _value; set => _value = value; }

        /// <inheritdoc/>
        public IEnumerable<ParameterReference> ReferencedParameters { get { if (_parameter != null) yield return new ParameterReference(_parameter, ParameterType.Int); } }

        /// <inheritdoc/>
        public override void Execute(BaseContext context)
        {
            if (_parameter == null) return;
            context.Set<int>(_parameter, _value);
        }
    }
}
