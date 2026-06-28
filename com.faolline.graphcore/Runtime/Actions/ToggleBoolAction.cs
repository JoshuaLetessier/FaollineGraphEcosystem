using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>Flips the bool at <see cref="ParameterKey"/> (false→true, true→false).
    /// Defaults to true when the key is absent (toggling an unset flag sets it).</summary>
    [CreateAssetMenu(menuName = "Faolline/Actions/Toggle Bool", fileName = "ToggleBoolAction")]
    public class ToggleBoolAction : BaseAction
    {
        [SerializeField, Tooltip("Context parameter key to toggle.")]
        private string _parameterKey;

        public string ParameterKey { get => _parameterKey; set => _parameterKey = value; }

        public override void Execute(BaseContext context)
        {
            context.TryGet<bool>(_parameterKey, out var current);
            context.Set<bool>(_parameterKey, !current);
        }
    }
}
