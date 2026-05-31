using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphDialogue
{
    /// <summary>Writes a named string value into the execution context when executed.</summary>
    [CreateAssetMenu(menuName = "GraphDialogue/Actions/Set String Action", fileName = "SetStringAction")]
    public class SetStringAction : BaseAction
    {
        [SerializeField] private string _parameterKey;
        [SerializeField] private string _value;

        public string ParameterKey { get => _parameterKey; set => _parameterKey = value; }
        public string Value { get => _value; set => _value = value; }

        public override void Execute(BaseContext context) => context.Set<string>(_parameterKey, _value);
    }
}
