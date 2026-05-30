using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphTest
{
    /// <summary>
    /// Condition that always evaluates to true regardless of context state.
    /// Use to unconditionally allow edge traversal or node entry in test graphs.
    /// </summary>
    [CreateAssetMenu(menuName = "GraphTest/Conditions/Always True", fileName = "AlwaysTrueCondition")]
    public class TestAlwaysTrueCondition : BaseCondition
    {
        /// <inheritdoc/>
        public override bool Evaluate(BaseContext context) => true;
    }
}
