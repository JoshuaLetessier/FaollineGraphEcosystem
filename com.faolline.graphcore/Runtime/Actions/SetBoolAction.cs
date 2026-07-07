using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Universal action: writes a fixed bool value into the execution context under a <see cref="ParameterName"/>
    /// asset's stable GUID key. Canonical home for the primitive bool setter — downstream libs that historically
    /// shipped their own (GraphStandard, GraphDialogue) now subclass this so there is a single implementation and
    /// no cross-namespace ambiguity.
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Actions/Set Bool", fileName = "SetBoolAction")]
    public class SetBoolAction : BaseAction, IParameterReferencing
    {
        [SerializeField, Tooltip("Parameter asset to write. Drag a ParameterName (type Bool); its stable GUID is the context key.")]
        private ParameterName _parameter;
        [SerializeField, Tooltip("The bool value written to the parameter.")]
        private bool _value;

        /// <summary>The parameter asset to write.</summary>
        public ParameterName Parameter { get => _parameter; set => _parameter = value; }

        /// <summary>The bool value to set on the parameter.</summary>
        public bool Value { get => _value; set => _value = value; }

        /// <inheritdoc/>
        public IEnumerable<ParameterReference> ReferencedParameters { get { if (_parameter != null) yield return new ParameterReference(_parameter, ParameterType.Bool); } }

        /// <inheritdoc/>
        public override void Execute(BaseContext context)
        {
            if (_parameter == null) return;
            context.Set<bool>(_parameter, _value);
        }
    }
}
