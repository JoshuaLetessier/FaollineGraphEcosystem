using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphDialogue
{
    /// <summary>Writes a named int value into the execution context when executed.</summary>
    [CreateAssetMenu(menuName = "GraphDialogue/Actions/Set Int Action", fileName = "SetIntAction")]
    public class SetIntAction : BaseAction
    {
        [SerializeField] private string _parameterKey;
        [SerializeField] private int _value;

        public string ParameterKey { get => _parameterKey; set => _parameterKey = value; }
        public int Value { get => _value; set => _value = value; }

        public override void Execute(BaseContext context) => context.Set<int>(_parameterKey, _value);
    }
}
