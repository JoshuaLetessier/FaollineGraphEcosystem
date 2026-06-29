using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Node action that records a value into a context collection. Attach to any node's on-enter or
    /// on-exit list. Idempotent (the underlying set holds each value once) and a graceful no-op when
    /// unconfigured. Supports drag-drop <see cref="CollectionName"/> asset (typo-safe) with fallback
    /// to raw strings.
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Actions/Add To Collection", fileName = "AddToCollectionAction")]
    public class AddToCollectionAction : BaseAction
    {
        [SerializeField, Tooltip("Drag a CollectionName asset for typo-safe references. Takes precedence over the raw string.")]
        private CollectionName _collectionAsset;
        [SerializeField, Tooltip("Fallback: raw collection key (used when no CollectionName asset is assigned).")]
        private string _collectionKey;
        [SerializeField, Tooltip("The value to add. If the CollectionName defines entries, pick from its list.")]
        private string _value;

        public CollectionName CollectionAsset { get => _collectionAsset; set => _collectionAsset = value; }
        public string CollectionKey { get => _collectionKey; set => _collectionKey = value; }
        public string Value { get => _value; set => _value = value; }

        public override void Execute(BaseContext context)
        {
            if (context == null) return;
            string key = _collectionAsset != null ? (string)_collectionAsset : _collectionKey;
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(_value)) return;
            context.AddToCollection(key, _value);
        }
    }
}
