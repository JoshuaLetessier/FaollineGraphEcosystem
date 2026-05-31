using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphDialogue
{
    /// <summary>Condition that always evaluates to true. Useful as a default or for testing.</summary>
    [CreateAssetMenu(menuName = "GraphDialogue/Conditions/Always True", fileName = "AlwaysTrueCondition")]
    public class AlwaysTrueCondition : BaseCondition
    {
        public override bool Evaluate(BaseContext context) => true;
    }
}
