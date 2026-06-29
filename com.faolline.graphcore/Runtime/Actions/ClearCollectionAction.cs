using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Empties a context collection. No-op when unconfigured or the collection is already absent.
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Actions/Clear Collection", fileName = "ClearCollectionAction")]
    public class ClearCollectionAction : BaseAction
    {
        [SerializeField, Tooltip("Drag a CollectionName asset for typo-safe references. Takes precedence over the raw string.")]
        private CollectionName _collectionAsset;
        [SerializeField, Tooltip("Fallback: raw collection key (used when no CollectionName asset is assigned).")]
        private string _collectionKey;

        public CollectionName CollectionAsset { get => _collectionAsset; set => _collectionAsset = value; }
        public string CollectionKey { get => _collectionKey; set => _collectionKey = value; }

        public override void Execute(BaseContext context)
        {
            if (context == null) return;
            string key = _collectionAsset != null ? (string)_collectionAsset : _collectionKey;
            if (string.IsNullOrWhiteSpace(key)) return;
            context.ClearCollection(key);
        }
    }
}
