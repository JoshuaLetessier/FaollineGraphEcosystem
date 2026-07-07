using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Empties a context collection. No-op when unconfigured or the collection is already absent.
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Actions/Clear Collection", fileName = "ClearCollectionAction")]
    public class ClearCollectionAction : BaseAction
    {
        [SerializeField, Tooltip("The collection to clear.")]
        private CollectionDef _collection;

        public CollectionDef Collection { get => _collection; set => _collection = value; }

        public override void Execute(BaseContext context)
        {
            if (context == null || _collection == null) return;
            string key = (string)_collection;
            if (string.IsNullOrEmpty(key)) return;
            context.ClearCollection(key);
        }
    }
}
