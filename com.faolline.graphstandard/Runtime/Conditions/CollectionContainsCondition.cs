using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphStandard
{
    /// <summary>
    /// Condition satisfied exactly when the context collection contains the specified value.
    /// A key with no collection is treated as empty (not satisfied), never an error.
    /// Supports drag-drop <see cref="CollectionName"/> and <see cref="CollectionEntry"/> assets.
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Conditions/Collection Contains", fileName = "CollectionContainsCondition")]
    public class CollectionContainsCondition : BaseCondition
    {
        [Header("Collection")]
        [SerializeField, Tooltip("Drag a CollectionName asset for typo-safe references. Takes precedence over the raw string.")]
        private CollectionName _collectionAsset;
        [SerializeField, Tooltip("Fallback: raw collection key.")]
        private string _collectionKey;

        [Header("Value")]
        [SerializeField, Tooltip("Drag a CollectionEntry asset (or a subclass). Takes precedence over the raw string.")]
        private CollectionEntry _valueAsset;
        [SerializeField, Tooltip("Fallback: raw value string.")]
        private string _value;

        public CollectionName CollectionAsset { get => _collectionAsset; set => _collectionAsset = value; }
        public string CollectionKey { get => _collectionKey; set => _collectionKey = value; }
        public CollectionEntry ValueAsset { get => _valueAsset; set => _valueAsset = value; }
        public string Value { get => _value; set => _value = value; }

        public override bool Evaluate(BaseContext context)
        {
            if (context == null) return false;
            string key = _collectionAsset != null ? (string)_collectionAsset : _collectionKey;
            string val = _valueAsset != null ? (string)_valueAsset : _value;
            return context.CollectionContains(key, val);
        }
    }
}
