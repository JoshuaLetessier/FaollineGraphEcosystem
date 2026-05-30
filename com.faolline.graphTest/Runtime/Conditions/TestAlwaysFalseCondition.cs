using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphTest
{
    /// <summary>
    /// Condition that always evaluates to false regardless of context state.
    /// Use to unconditionally block edge traversal or node entry in test graphs.
    /// </summary>
    [CreateAssetMenu(menuName = "GraphTest/Conditions/Always False", fileName = "AlwaysFalseCondition")]
    public class TestAlwaysFalseCondition : BaseCondition
    {
        /// <inheritdoc/>
        public override bool Evaluate(BaseContext context) => false;
    }
}
