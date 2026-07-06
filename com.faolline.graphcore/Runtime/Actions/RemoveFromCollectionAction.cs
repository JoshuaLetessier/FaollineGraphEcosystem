using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Removes a value from a context collection. No-op when unconfigured or the value is absent.
    /// Symmetric counterpart of <see cref="AddToCollectionAction"/>. By default removes the entry entirely
    /// whatever its quantity; turn on <see cref="Stack"/> to decrement by <see cref="Count"/> instead.
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Actions/Remove From Collection", fileName = "RemoveFromCollectionAction")]
    public class RemoveFromCollectionAction : BaseAction
    {
        [SerializeField, Tooltip("The collection to remove the value from.")]
        private CollectionName _collection;

        [SerializeField, Tooltip("The entry to remove.")]
        private CollectionEntry _entry;

        [SerializeField, Tooltip("OFF (default): remove the entry entirely, whatever its quantity (matches " +
            "every asset authored before this option existed). ON: subtract Count units instead — the entry " +
            "is only dropped once its quantity reaches zero.")]
        private bool _stack;

        [SerializeField, Min(1), Tooltip("Units subtracted when Stack is ON. Ignored when Stack is OFF.")]
        private int _count = 1;

        public CollectionName Collection { get => _collection; set => _collection = value; }
        public CollectionEntry Entry { get => _entry; set => _entry = value; }
        public bool Stack { get => _stack; set => _stack = value; }
        public int Count { get => _count; set => _count = value; }

        public override void Execute(BaseContext context)
        {
            if (context == null || _collection == null || _entry == null) return;
            string key = (string)_collection;
            string val = (string)_entry;
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(val)) return;
            if (_stack) context.RemoveFromCollection(key, val, Mathf.Max(1, _count));
            else context.RemoveFromCollection(key, val);
        }
    }
}
