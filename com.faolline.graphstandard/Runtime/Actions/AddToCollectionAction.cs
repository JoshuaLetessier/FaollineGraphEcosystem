using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphStandard
{
    /// <summary>
    /// Node action that records a configured <see cref="Value"/> into the context collection at
    /// <see cref="CollectionKey"/> (a graphcore string-set). Attach to any node's on-enter or on-exit list to
    /// mark, for example, that a step was reached. Idempotent (the underlying set holds each value once) and a
    /// graceful no-op when the key or value is empty, so a half-configured asset never throws at runtime.
    /// <para>
    /// This is the universal write half of the reactive-hosting pattern: write ids into a shared completed-set
    /// here, and let a <see cref="ReactiveEvaluator"/> over the same context derive the unlocks.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Actions/Add To Collection", fileName = "AddToCollectionAction")]
    public class AddToCollectionAction : BaseAction
    {
        [SerializeField, Tooltip("Context collection key to add the value to (a named string-set on BaseContext).")]
        private string _collectionKey;
        [SerializeField, Tooltip("The string value added to the collection. Idempotent — duplicates are ignored.")]
        private string _value;

        /// <summary>The collection key written to.</summary>
        public string CollectionKey { get => _collectionKey; set => _collectionKey = value; }

        /// <summary>The value added to the collection.</summary>
        public string Value { get => _value; set => _value = value; }

        /// <inheritdoc/>
        public override void Execute(BaseContext context)
        {
            if (context == null) return;
            if (string.IsNullOrWhiteSpace(_collectionKey) || string.IsNullOrWhiteSpace(_value))
                return;
            context.AddToCollection(_collectionKey, _value);
        }
    }
}
