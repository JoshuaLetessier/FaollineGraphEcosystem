using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphStandard
{
    /// <summary>
    /// Condition satisfied exactly when a SPECIFIC entry's quantity in a context collection reaches
    /// <see cref="Threshold"/> (e.g. "has at least 3 potions"). The stacking counterpart of
    /// <see cref="CollectionCountAtLeastCondition"/>, which instead counts DISTINCT entries. An absent
    /// entry counts as 0; a threshold of 0 is always satisfied.
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Conditions/Collection Item Count At Least", fileName = "CollectionItemCountAtLeastCondition")]
    public class CollectionItemCountAtLeastCondition : BaseCondition
    {
        [SerializeField, Tooltip("The collection to inspect.")]
        private CollectionName _collection;
        [SerializeField, Tooltip("The entry whose quantity is checked.")]
        private CollectionEntry _entry;
        [SerializeField, Tooltip("Minimum quantity that satisfies this condition. 0 = always satisfied.")]
        private int _threshold;

        public CollectionName Collection { get => _collection; set => _collection = value; }
        public CollectionEntry Entry { get => _entry; set => _entry = value; }
        public int Threshold { get => _threshold; set => _threshold = value; }

        public override bool Evaluate(BaseContext context)
        {
            if (context == null || _collection == null || _entry == null) return _threshold <= 0;
            return context.CollectionItemCount((string)_collection, (string)_entry) >= _threshold;
        }
    }
}
