using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Composite condition: passes when ANY sub-condition passes (logical OR).
    /// An empty list evaluates to false.
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Conditions/Or (any must pass)", fileName = "OrCondition")]
    public class OrCondition : BaseCondition
    {
        [SerializeField, Tooltip("At least one of these conditions must pass for this condition to pass.")]
        private List<BaseCondition> _conditions = new List<BaseCondition>();

        public List<BaseCondition> Conditions => _conditions;

        public override bool Evaluate(BaseContext context)
        {
            foreach (var c in _conditions)
            {
                if (c == null) continue;
                if (c.Evaluate(context)) return true;
            }
            return false;
        }
    }
}
