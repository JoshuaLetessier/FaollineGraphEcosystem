using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Composite condition: passes when ALL sub-conditions pass (logical AND).
    /// An empty list evaluates to true (vacuous truth).
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Conditions/And (all must pass)", fileName = "AndCondition")]
    public class AndCondition : BaseCondition
    {
        [SerializeField, Tooltip("All of these conditions must pass for this condition to pass.")]
        private List<BaseCondition> _conditions = new List<BaseCondition>();

        public List<BaseCondition> Conditions => _conditions;

        public override bool Evaluate(BaseContext context)
        {
            foreach (var c in _conditions)
            {
                if (c == null) continue;
                if (!c.Evaluate(context)) return false;
            }
            return true;
        }
    }
}
