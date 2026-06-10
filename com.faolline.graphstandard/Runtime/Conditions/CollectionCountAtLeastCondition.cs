using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphStandard
{
    /// <summary>
    /// Condition satisfied exactly when the context collection at <see cref="CollectionKey"/> holds at least
    /// <see cref="Threshold"/> values. This is how a "k-of-N done unlocks this" gate is expressed on a Linear
    /// edge. A key with no collection counts as 0; a <see cref="Threshold"/> of 0 is always satisfied.
    /// </summary>
    [CreateAssetMenu(menuName = "GraphStandard/Conditions/Collection Count At Least", fileName = "CollectionCountAtLeastCondition")]
    public class CollectionCountAtLeastCondition : BaseCondition
    {
        [SerializeField] private string _collectionKey;
        [SerializeField] private int _threshold;

        /// <summary>The collection key whose cardinality is read.</summary>
        public string CollectionKey { get => _collectionKey; set => _collectionKey = value; }

        /// <summary>The minimum element count that satisfies the condition.</summary>
        public int Threshold { get => _threshold; set => _threshold = value; }

        /// <inheritdoc/>
        public override bool Evaluate(BaseContext context)
            => context != null && context.CollectionCount(_collectionKey) >= _threshold;
    }
}
