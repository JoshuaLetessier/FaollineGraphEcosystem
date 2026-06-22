using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>Condition that always evaluates to false, regardless of context state. Canonical home in GraphCore;
    /// downstream libs subclass this.</summary>
    [CreateAssetMenu(menuName = "Faolline/Conditions/Always False", fileName = "AlwaysFalseCondition")]
    public class AlwaysFalseCondition : BaseCondition
    {
        /// <inheritdoc/>
        public override bool Evaluate(BaseContext context) => false;
    }
}
