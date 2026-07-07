using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphStandard
{
    /// <summary>
    /// Condition satisfied exactly when the context collection holds at least <see cref="Threshold"/>
    /// values. A key with no collection counts as 0; a threshold of 0 is always satisfied.
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Conditions/Collection Count At Least", fileName = "CollectionCountAtLeastCondition")]
    public class CollectionCountAtLeastCondition : BaseCondition
    {
        [SerializeField, Tooltip("The collection whose cardinality is checked.")]
        private CollectionDef _collection;
        [SerializeField, Tooltip("Minimum element count that satisfies this condition. 0 = always satisfied.")]
        private int _threshold;

        public CollectionDef Collection { get => _collection; set => _collection = value; }
        public int Threshold { get => _threshold; set => _threshold = value; }

        public override bool Evaluate(BaseContext context)
        {
            if (context == null) return _threshold <= 0;
            string key = _collection != null ? (string)_collection : string.Empty;
            return context.CollectionCount(key) >= _threshold;
        }
    }
}
