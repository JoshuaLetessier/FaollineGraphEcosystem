using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphStandard
{
    /// <summary>Condition that always evaluates to true, regardless of context state.</summary>
    [CreateAssetMenu(menuName = "GraphStandard/Conditions/Always True", fileName = "AlwaysTrueCondition")]
    public class AlwaysTrueCondition : BaseCondition
    {
        /// <inheritdoc/>
        public override bool Evaluate(BaseContext context) => true;
    }
}
