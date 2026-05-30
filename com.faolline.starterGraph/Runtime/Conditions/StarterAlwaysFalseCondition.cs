using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.StarterGraph
{
    /// <summary>
    /// Condition that always evaluates to false regardless of context state.
    /// Use to unconditionally block edge traversal or node entry in test graphs.
    /// </summary>
    [CreateAssetMenu(menuName = "StarterGraph/Conditions/Always False", fileName = "AlwaysFalseCondition")]
    public class StarterAlwaysFalseCondition : BaseCondition
    {
        /// <inheritdoc/>
        public override bool Evaluate(BaseContext context) => false;
    }
}
