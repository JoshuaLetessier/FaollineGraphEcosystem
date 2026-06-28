using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Composite condition: negates a single sub-condition (logical NOT).
    /// Evaluates to true when <see cref="Condition"/> is null (nothing to negate).
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Conditions/Not (negate)", fileName = "NotCondition")]
    public class NotCondition : BaseCondition
    {
        [SerializeField, Tooltip("The condition to negate.")]
        private BaseCondition _condition;

        public BaseCondition Condition { get => _condition; set => _condition = value; }

        public override bool Evaluate(BaseContext context)
        {
            if (_condition == null) return true;
            return !_condition.Evaluate(context);
        }
    }
}
