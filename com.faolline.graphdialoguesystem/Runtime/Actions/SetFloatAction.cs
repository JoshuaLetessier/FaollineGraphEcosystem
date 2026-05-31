using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphDialogue
{
    /// <summary>Writes a named float value into the execution context when executed.</summary>
    [CreateAssetMenu(menuName = "GraphDialogue/Actions/Set Float Action", fileName = "SetFloatAction")]
    public class SetFloatAction : BaseAction
    {
        [SerializeField] private string _parameterKey;
        [SerializeField] private float _value;

        public string ParameterKey { get => _parameterKey; set => _parameterKey = value; }
        public float Value { get => _value; set => _value = value; }

        public override void Execute(BaseContext context) => context.Set<float>(_parameterKey, _value);
    }
}
