using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Empties the context collection at <see cref="CollectionKey"/>.
    /// No-op when the key is empty or the collection is already absent.
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Actions/Clear Collection", fileName = "ClearCollectionAction")]
    public class ClearCollectionAction : BaseAction
    {
        [SerializeField, Tooltip("Context collection key to clear.")]
        private string _collectionKey;

        public string CollectionKey { get => _collectionKey; set => _collectionKey = value; }

        public override void Execute(BaseContext context)
        {
            if (context == null || string.IsNullOrWhiteSpace(_collectionKey)) return;
            context.ClearCollection(_collectionKey);
        }
    }
}
