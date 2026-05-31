using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphDialogue
{
    /// <summary>Condition that always evaluates to false. Useful for testing blocked branches.</summary>
    [CreateAssetMenu(menuName = "GraphDialogue/Conditions/Always False", fileName = "AlwaysFalseCondition")]
    public class AlwaysFalseCondition : BaseCondition
    {
        public override bool Evaluate(BaseContext context) => false;
    }
}
