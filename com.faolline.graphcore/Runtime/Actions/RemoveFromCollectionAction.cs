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
        [Header("Collection")]
        [SerializeField, Tooltip("Drag a CollectionName asset for typo-safe references. Takes precedence over the raw string.")]
        private CollectionName _collectionAsset;
        [SerializeField, Tooltip("Fallback: raw collection key (used when no CollectionName asset is assigned).")]
        private string _collectionKey;

        [Header("Value")]
        [SerializeField, Tooltip("Drag a CollectionEntry asset (or a subclass). Takes precedence over the raw string.")]
        private CollectionEntry _valueAsset;
        [SerializeField, Tooltip("Fallback: raw value string (used when no CollectionEntry asset is assigned).")]
        private string _value;

        public CollectionName CollectionAsset { get => _collectionAsset; set => _collectionAsset = value; }
        public string CollectionKey { get => _collectionKey; set => _collectionKey = value; }
        public CollectionEntry ValueAsset { get => _valueAsset; set => _valueAsset = value; }
        public string Value { get => _value; set => _value = value; }

        public override void Execute(BaseContext context)
        {
            if (context == null) return;
            string key = _collectionAsset != null ? (string)_collectionAsset : _collectionKey;
            string val = _valueAsset != null ? (string)_valueAsset : _value;
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(val)) return;
            context.RemoveFromCollection(key, val);
        }
    }
}
