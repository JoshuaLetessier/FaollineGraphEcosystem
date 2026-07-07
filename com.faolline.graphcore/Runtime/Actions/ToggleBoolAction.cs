using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>Flips the bool at <see cref="Parameter"/> (false→true, true→false).
    /// Defaults to true when the key is absent (toggling an unset flag sets it).</summary>
    [CreateAssetMenu(menuName = "Faolline/Actions/Toggle Bool", fileName = "ToggleBoolAction")]
    public class ToggleBoolAction : BaseAction, IParameterReferencing
    {
        [SerializeField, Tooltip("Parameter asset to toggle. Drag a ParameterName (type Bool).")]
        private ParameterName _parameter;

        /// <summary>The parameter asset to toggle.</summary>
        public ParameterName Parameter { get => _parameter; set => _parameter = value; }

        /// <inheritdoc/>
        public IEnumerable<ParameterReference> ReferencedParameters { get { if (_parameter != null) yield return new ParameterReference(_parameter, ParameterType.Bool); } }

        /// <inheritdoc/>
        public override void Execute(BaseContext context)
        {
            if (_parameter == null) return;
            context.TryGet<bool>(_parameter, out var current);
            context.Set<bool>(_parameter, !current);
        }
    }
}
