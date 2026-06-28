using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphStandard
{
    /// <summary>
    /// Removes a configured <see cref="Value"/> from the context collection at
    /// <see cref="CollectionKey"/>. No-op when the key, value, or collection is absent.
    /// Symmetric counterpart of <see cref="AddToCollectionAction"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Actions/Remove From Collection", fileName = "RemoveFromCollectionAction")]
    public class RemoveFromCollectionAction : BaseAction
    {
        [SerializeField, Tooltip("Context collection key to remove the value from.")]
        private string _collectionKey;
        [SerializeField, Tooltip("The string value to remove from the collection.")]
        private string _value;

        public string CollectionKey { get => _collectionKey; set => _collectionKey = value; }
        public string Value { get => _value; set => _value = value; }

        public override void Execute(BaseContext context)
        {
            if (context == null) return;
            if (string.IsNullOrWhiteSpace(_collectionKey) || string.IsNullOrWhiteSpace(_value))
                return;
            context.RemoveFromCollection(_collectionKey, _value);
        }
    }
}
