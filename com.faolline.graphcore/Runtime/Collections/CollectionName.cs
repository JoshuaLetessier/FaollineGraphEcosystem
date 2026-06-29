using System.Collections.Generic;
using UnityEngine;

namespace Faolline.GraphCore
{
    /// <summary>
    /// A named collection as a reusable asset — drag-drop instead of typing a string key.
    /// Prevents typos, enables rename-safe references, and is visible in the Project browser.
    /// <para>
    /// Optionally defines a set of known <see cref="Entries"/> so the inspector can show a
    /// dropdown instead of a free-text field. Entries left empty means the collection is open
    /// (any string value, useful for dynamic/runtime items).
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Collection Name", fileName = "NewCollection")]
    public class CollectionName : ScriptableObject
    {
        [SerializeField, Tooltip("The collection key used in BaseContext. Falls back to the asset name when empty.")]
        private string _key;

        [SerializeField, Tooltip("Known entries for this collection (optional). When populated, the inspector " +
            "offers a dropdown on actions/conditions that reference this collection. Leave empty for open collections.")]
        private List<string> _entries = new List<string>();

        /// <summary>The collection key string. Falls back to the asset name when empty.</summary>
        public string Key => string.IsNullOrEmpty(_key) ? base.name : _key;

        /// <summary>Known entries defined on this collection. Empty means open (any value accepted).</summary>
        public IReadOnlyList<string> Entries => _entries;

        /// <summary>True when this collection defines known entries (closed or semi-closed).</summary>
        public bool HasEntries => _entries != null && _entries.Count > 0;

        /// <summary>Returns true when <paramref name="value"/> is one of the known entries (or the list is empty = open).</summary>
        public bool IsValidEntry(string value)
            => !HasEntries || _entries.Contains(value);

        public static implicit operator string(CollectionName collection)
            => collection != null ? collection.Key : string.Empty;
    }
}
