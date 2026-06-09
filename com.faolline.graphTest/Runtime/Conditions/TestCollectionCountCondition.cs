using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphTest
{
    /// <summary>
    /// Condition that compares the element count of collection <see cref="CollectionKey"/> against
    /// <see cref="Value"/> via a <see cref="ComparisonOperator"/> (e.g. count ≥ N). Previews the P4
    /// count-threshold join shape.
    /// </summary>
    [CreateAssetMenu(menuName = "GraphTest/Conditions/Collection Count", fileName = "CollectionCountCondition")]
    public class TestCollectionCountCondition : BaseCondition
    {
        [SerializeField] private string _collectionKey;
        [SerializeField] private ComparisonOperator _operator = ComparisonOperator.GreaterOrEqual;
        [SerializeField] private int _value;

        /// <summary>The collection key whose cardinality is compared.</summary>
        public string CollectionKey { get => _collectionKey; set => _collectionKey = value; }

        /// <summary>The comparison applied between the collection count and <see cref="Value"/>.</summary>
        public ComparisonOperator Operator { get => _operator; set => _operator = value; }

        /// <summary>The count this condition compares against.</summary>
        public int Value { get => _value; set => _value = value; }

        /// <inheritdoc/>
        public override bool Evaluate(BaseContext context)
        {
            int comparison = context.CollectionCount(_collectionKey).CompareTo(_value);
            switch (_operator)
            {
                case ComparisonOperator.Equal:          return comparison == 0;
                case ComparisonOperator.NotEqual:       return comparison != 0;
                case ComparisonOperator.Less:           return comparison < 0;
                case ComparisonOperator.LessOrEqual:    return comparison <= 0;
                case ComparisonOperator.Greater:        return comparison > 0;
                case ComparisonOperator.GreaterOrEqual: return comparison >= 0;
                default:                                return false;
            }
        }
    }
}
