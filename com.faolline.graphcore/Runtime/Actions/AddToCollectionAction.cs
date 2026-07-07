using UnityEngine;
using UnityEngine.Serialization;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Node action that records a value into a context collection. Attach to any node's on-enter or
    /// on-exit list. By default idempotent (the entry's presence is ensured once) and a graceful no-op
    /// when unconfigured; turn on <see cref="Stack"/> to add quantity instead (inventory-style stacking).
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Actions/Add To Collection", fileName = "AddToCollectionAction")]
    public class AddToCollectionAction : BaseAction
    {
        // FormerlySerializedAs recovers assets authored before the 0.22 asset-only refactor, which renamed
        // _collectionAsset → _collection and _valueAsset → _entry (the raw-string _collectionKey / _value
        // fallbacks were intentionally dropped and cannot be migrated to assets).
        [SerializeField, FormerlySerializedAs("_collectionAsset"), Tooltip("The collection to add the value to.")]
        private CollectionDef _collection;

        [SerializeField, FormerlySerializedAs("_valueAsset"), Tooltip("The entry to add. Drag a CollectionEntry asset (or a subclass like an inventory item).")]
        private CollectionEntry _entry;

        [SerializeField, Tooltip("OFF (default): ensure the entry is present — idempotent, no effect if it " +
            "already is (matches every asset authored before this option existed). ON: add Count units to " +
            "the entry's quantity instead — always recorded, even if it was already present (stacking).")]
        private bool _stack;

        [SerializeField, Min(1), Tooltip("Units added when Stack is ON. Ignored when Stack is OFF.")]
        private int _count = 1;

        public CollectionDef Collection { get => _collection; set => _collection = value; }
        public CollectionEntry Entry { get => _entry; set => _entry = value; }
        public bool Stack { get => _stack; set => _stack = value; }
        public int Count { get => _count; set => _count = value; }

        public override void Execute(BaseContext context)
        {
            if (context == null || _collection == null || _entry == null) return;
            string key = (string)_collection;
            string val = (string)_entry;
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(val)) return;
            if (_stack) context.AddToCollection(key, val, Mathf.Max(1, _count));
            else context.AddToCollection(key, val);
        }
    }
}
