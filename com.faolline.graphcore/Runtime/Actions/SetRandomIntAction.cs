using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>Sets <see cref="Parameter"/> to a random int in [<see cref="Min"/>, <see cref="Max"/>]
    /// (inclusive on both ends). Useful for dice rolls, random encounter selection, or branching variety.</summary>
    [CreateAssetMenu(menuName = "Faolline/Actions/Set Random Int", fileName = "SetRandomIntAction")]
    public class SetRandomIntAction : BaseAction, IParameterReferencing
    {
        [SerializeField, Tooltip("Parameter asset to write the random value to. Drag a ParameterName (type Int).")]
        private ParameterName _parameter;
        [SerializeField, Tooltip("Minimum value (inclusive).")]
        private int _min;
        [SerializeField, Tooltip("Maximum value (inclusive).")]
        private int _max = 10;

        /// <summary>The parameter asset to write the random value to.</summary>
        public ParameterName Parameter { get => _parameter; set => _parameter = value; }
        public int Min { get => _min; set => _min = value; }
        public int Max { get => _max; set => _max = value; }

        /// <inheritdoc/>
        public IEnumerable<ParameterReference> ReferencedParameters { get { if (_parameter != null) yield return new ParameterReference(_parameter, ParameterType.Int); } }

        /// <inheritdoc/>
        public override void Execute(BaseContext context)
        {
            if (_parameter == null) return;
            context.Set<int>(_parameter, Random.Range(_min, _max + 1));
        }
    }
}
