using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Removes a value from a context collection. No-op when unconfigured or the value is absent.
    /// Symmetric counterpart of <see cref="AddToCollectionAction"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Actions/Remove From Collection", fileName = "RemoveFromCollectionAction")]
    public class RemoveFromCollectionAction : BaseAction
    {
        [SerializeField, Tooltip("The collection to remove the value from.")]
        private CollectionName _collection;

        [SerializeField, Tooltip("The entry to remove.")]
        private CollectionEntry _entry;

        public CollectionName Collection { get => _collection; set => _collection = value; }
        public CollectionEntry Entry { get => _entry; set => _entry = value; }

        public override void Execute(BaseContext context)
        {
            if (context == null || _collection == null || _entry == null) return;
            string key = (string)_collection;
            string val = (string)_entry;
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(val)) return;
            context.RemoveFromCollection(key, val);
        }
    }
}
