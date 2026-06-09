using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphTest
{
    /// <summary>
    /// Condition that passes when collection <see cref="CollectionKey"/> contains <see cref="Item"/>
    /// (optionally negated). Demonstrates a downstream-style membership gate over a context collection.
    /// </summary>
    [CreateAssetMenu(menuName = "GraphTest/Conditions/Collection Contains", fileName = "CollectionContainsCondition")]
    public class TestCollectionContainsCondition : BaseCondition
    {
        [SerializeField] private string _collectionKey;
        [SerializeField] private string _item;
        [SerializeField] private bool _negate;

        /// <summary>The collection key inspected.</summary>
        public string CollectionKey { get => _collectionKey; set => _collectionKey = value; }

        /// <summary>The element whose membership is tested.</summary>
        public string Item { get => _item; set => _item = value; }

        /// <summary>When true, the condition passes on absence instead of presence.</summary>
        public bool Negate { get => _negate; set => _negate = value; }

        /// <inheritdoc/>
        public override bool Evaluate(BaseContext context)
        {
            bool has = context.CollectionContains(_collectionKey, _item);
            return _negate ? !has : has;
        }
    }
}
