using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>Sets <see cref="ParameterKey"/> to a random int in [<see cref="Min"/>, <see cref="Max"/>]
    /// (inclusive on both ends). Useful for dice rolls, random encounter selection, or branching variety.</summary>
    [CreateAssetMenu(menuName = "Faolline/Actions/Set Random Int", fileName = "SetRandomIntAction")]
    public class SetRandomIntAction : BaseAction
    {
        [SerializeField, Tooltip("Context parameter key to write the random value to.")]
        private string _parameterKey;
        [SerializeField, Tooltip("Minimum value (inclusive).")]
        private int _min;
        [SerializeField, Tooltip("Maximum value (inclusive).")]
        private int _max = 10;

        public string ParameterKey { get => _parameterKey; set => _parameterKey = value; }
        public int Min { get => _min; set => _min = value; }
        public int Max { get => _max; set => _max = value; }

        public override void Execute(BaseContext context)
        {
            context.Set<int>(_parameterKey, Random.Range(_min, _max + 1));
        }
    }
}
