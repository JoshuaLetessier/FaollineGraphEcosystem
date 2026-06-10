using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphStandard
{
    /// <summary>
    /// Condition satisfied exactly when the context collection at <see cref="CollectionKey"/> contains
    /// <see cref="Value"/>. A key with no collection is treated as empty (the condition is simply not
    /// satisfied), never an error. Attach to an edge, choice, or node entry to gate on membership.
    /// </summary>
    [CreateAssetMenu(menuName = "GraphStandard/Conditions/Collection Contains", fileName = "CollectionContainsCondition")]
    public class CollectionContainsCondition : BaseCondition
    {
        [SerializeField] private string _collectionKey;
        [SerializeField] private string _value;

        /// <summary>The collection key inspected.</summary>
        public string CollectionKey { get => _collectionKey; set => _collectionKey = value; }

        /// <summary>The value whose membership is tested.</summary>
        public string Value { get => _value; set => _value = value; }

        /// <inheritdoc/>
        public override bool Evaluate(BaseContext context)
            => context != null && context.CollectionContains(_collectionKey, _value);
    }
}
