using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphDialogue
{
    /// <summary>Writes a named bool value into the execution context when executed.</summary>
    [CreateAssetMenu(menuName = "GraphDialogue/Actions/Set Bool Action", fileName = "SetBoolAction")]
    public class SetBoolAction : BaseAction
    {
        [SerializeField] private string _parameterKey;
        [SerializeField] private bool _value;

        public string ParameterKey { get => _parameterKey; set => _parameterKey = value; }
        public bool Value { get => _value; set => _value = value; }

        public override void Execute(BaseContext context) => context.Set<bool>(_parameterKey, _value);
    }
}
