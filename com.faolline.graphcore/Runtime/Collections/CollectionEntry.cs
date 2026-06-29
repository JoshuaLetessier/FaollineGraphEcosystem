using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// A named entry that can be added to / removed from a context collection. Drag-drop instead
    /// of typing a raw string value. Subclass to carry domain-specific data (sprite, description,
    /// stats, …) — the collection stores only the <see cref="Key"/> string; the consumer resolves
    /// the full asset from its own registry when needed.
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Collection Entry", fileName = "NewEntry")]
    public class CollectionEntry : ScriptableObject
    {
        [SerializeField, Tooltip("The string key stored in the context collection. Falls back to the asset name when empty.")]
        private string _key;

        /// <summary>The key string stored in context collections. Falls back to the asset name when empty.</summary>
        public string Key => string.IsNullOrEmpty(_key) ? base.name : _key;

        public static implicit operator string(CollectionEntry entry)
            => entry != null ? entry.Key : string.Empty;
    }
}
