using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.StarterGraph
{
    /// <summary>
    /// Condition that always evaluates to true regardless of context state.
    /// Use to unconditionally allow edge traversal or node entry in test graphs.
    /// </summary>
    [CreateAssetMenu(menuName = "StarterGraph/Conditions/Always True", fileName = "AlwaysTrueCondition")]
    public class StarterAlwaysTrueCondition : BaseCondition
    {
        /// <inheritdoc/>
        public override bool Evaluate(BaseContext context) => true;
    }
}
