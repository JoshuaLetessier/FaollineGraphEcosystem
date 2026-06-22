using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>Condition that always evaluates to false, regardless of context state. Canonical home in GraphCore;
    /// downstream libs subclass this.</summary>
    // No [CreateAssetMenu] — created via the inspector's object picker on condition fields.
    public class AlwaysFalseCondition : BaseCondition
    {
        /// <inheritdoc/>
        public override bool Evaluate(BaseContext context) => false;
    }
}
