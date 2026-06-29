using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphStandard
{
    /// <summary>
    /// Condition satisfied exactly when the context collection holds at least <see cref="Threshold"/>
    /// values. A key with no collection counts as 0; a threshold of 0 is always satisfied.
    /// Supports drag-drop <see cref="CollectionName"/> asset with fallback to raw string.
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Conditions/Collection Count At Least", fileName = "CollectionCountAtLeastCondition")]
    public class CollectionCountAtLeastCondition : BaseCondition
    {
        [SerializeField, Tooltip("Drag a CollectionName asset for typo-safe references. Takes precedence over the raw string.")]
        private CollectionName _collectionAsset;
        [SerializeField, Tooltip("Fallback: raw collection key.")]
        private string _collectionKey;
        [SerializeField, Tooltip("Minimum element count that satisfies this condition. 0 = always satisfied.")]
        private int _threshold;

        public CollectionName CollectionAsset { get => _collectionAsset; set => _collectionAsset = value; }
        public string CollectionKey { get => _collectionKey; set => _collectionKey = value; }
        public int Threshold { get => _threshold; set => _threshold = value; }

        public override bool Evaluate(BaseContext context)
        {
            if (context == null) return false;
            string key = _collectionAsset != null ? (string)_collectionAsset : _collectionKey;
            return context.CollectionCount(key) >= _threshold;
        }
    }
}
