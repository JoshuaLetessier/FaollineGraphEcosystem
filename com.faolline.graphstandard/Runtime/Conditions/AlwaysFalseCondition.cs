using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphStandard
{
    /// <summary>Condition that always evaluates to false, regardless of context state.</summary>
    [CreateAssetMenu(menuName = "GraphStandard/Conditions/Always False", fileName = "AlwaysFalseCondition")]
    public class AlwaysFalseCondition : BaseCondition
    {
        /// <inheritdoc/>
        public override bool Evaluate(BaseContext context) => false;
    }
}
