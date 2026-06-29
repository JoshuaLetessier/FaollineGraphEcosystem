using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Node action that records a value into a context collection. Attach to any node's on-enter or
    /// on-exit list. Idempotent (the underlying set holds each value once) and a graceful no-op when
    /// unconfigured.
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Actions/Add To Collection", fileName = "AddToCollectionAction")]
    public class AddToCollectionAction : BaseAction
    {
        [SerializeField, Tooltip("The collection to add the value to.")]
        private CollectionName _collection;

        [SerializeField, Tooltip("The entry to add. Drag a CollectionEntry asset (or a subclass like an inventory item).")]
        private CollectionEntry _entry;

        public CollectionName Collection { get => _collection; set => _collection = value; }
        public CollectionEntry Entry { get => _entry; set => _entry = value; }

        public override void Execute(BaseContext context)
        {
            if (context == null || _collection == null || _entry == null) return;
            string key = (string)_collection;
            string val = (string)_entry;
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(val)) return;
            context.AddToCollection(key, val);
        }
    }
}
