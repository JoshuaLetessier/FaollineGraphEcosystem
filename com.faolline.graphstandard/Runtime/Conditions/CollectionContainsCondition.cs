using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphStandard
{
    /// <summary>
    /// Condition satisfied exactly when the context collection contains <see cref="Value"/>.
    /// A key with no collection is treated as empty (not satisfied), never an error.
    /// Supports drag-drop <see cref="CollectionName"/> asset with fallback to raw string.
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Conditions/Collection Contains", fileName = "CollectionContainsCondition")]
    public class CollectionContainsCondition : BaseCondition
    {
        [SerializeField, Tooltip("Drag a CollectionName asset for typo-safe references. Takes precedence over the raw string.")]
        private CollectionName _collectionAsset;
        [SerializeField, Tooltip("Fallback: raw collection key.")]
        private string _collectionKey;
        [SerializeField, Tooltip("The value whose membership in the collection is tested.")]
        private string _value;

        public CollectionName CollectionAsset { get => _collectionAsset; set => _collectionAsset = value; }
        public string CollectionKey { get => _collectionKey; set => _collectionKey = value; }
        public string Value { get => _value; set => _value = value; }

        public override bool Evaluate(BaseContext context)
        {
            if (context == null) return false;
            string key = _collectionAsset != null ? (string)_collectionAsset : _collectionKey;
            return context.CollectionContains(key, _value);
        }
    }
}
