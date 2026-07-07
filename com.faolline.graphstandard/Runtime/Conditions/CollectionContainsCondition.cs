using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphStandard
{
    /// <summary>
    /// Condition satisfied exactly when the context collection contains the specified entry.
    /// A key with no collection is treated as empty (not satisfied), never an error.
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Conditions/Collection Contains", fileName = "CollectionContainsCondition")]
    public class CollectionContainsCondition : BaseCondition
    {
        [SerializeField, Tooltip("The collection to inspect.")]
        private CollectionDef _collection;

        [SerializeField, Tooltip("The entry whose membership is tested.")]
        private CollectionEntry _entry;

        public CollectionDef Collection { get => _collection; set => _collection = value; }
        public CollectionEntry Entry { get => _entry; set => _entry = value; }

        public override bool Evaluate(BaseContext context)
        {
            if (context == null || _collection == null || _entry == null) return false;
            return context.CollectionContains((string)_collection, (string)_entry);
        }
    }
}
