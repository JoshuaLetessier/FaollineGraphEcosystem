using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>Condition that always evaluates to true, regardless of context state. Canonical home in GraphCore;
    /// downstream libs subclass this.</summary>
    // No [CreateAssetMenu] — created via the inspector's object picker on condition fields.
    public class AlwaysTrueCondition : BaseCondition
    {
        /// <inheritdoc/>
        public override bool Evaluate(BaseContext context) => true;
    }
}
