using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Faolline.GraphCore
{
    /// <summary>
    /// A named entry that can be added to / removed from a context collection. Drag-drop instead
    /// of typing a raw string value. Subclass to carry domain-specific data (sprite, description,
    /// stats, …) — the collection stores only the <see cref="Key"/> string; the consumer resolves
    /// the full asset from its own registry when needed.
    /// <para>
    /// <see cref="Key"/> is a stable GUID assigned once in <c>OnEnable</c> and never editable — the
    /// same stability guarantee as <see cref="BaseGraph.GraphId"/>. Renaming the asset file, or
    /// duplicating it (Ctrl+D), never changes what gets stored in a context collection (a rename
    /// under the previous name-fallback scheme silently changed the stored value). Use
    /// <see cref="Title"/> for a human-readable label in editor tooling — it is purely cosmetic and
    /// never affects the stored key.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Collection Entry", fileName = "NewEntry")]
    public class CollectionEntry : ScriptableObject
    {
        // FormerlySerializedAs recovers an asset authored before this fix that had an EXPLICIT non-empty
        // _key: that string becomes the stable id as-is (no longer editable going forward). An asset that
        // relied on the old empty-key/name-fallback behaviour deserializes _id as empty and gets a fresh
        // GUID below — its old "identity" was never actually stored (it was derived from the file name at
        // runtime), so there is nothing to preserve for that case.
        [SerializeField, FormerlySerializedAs("_key"), HideInInspector] private string _id;

        [SerializeField, Tooltip("Optional display label for editor tooling (Context Watch, etc.). Purely " +
            "cosmetic — never used as the stored key. Falls back to the asset name when empty.")]
        private string _title;

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(_id))
                _id = Guid.NewGuid().ToString("D");
        }

        /// <summary>
        /// Stable GUID stored in context collections. Assigned once in <c>OnEnable</c> and never
        /// editable — renaming or duplicating the asset never changes it.
        /// </summary>
        public string Key => _id;

        /// <summary>Human-readable label for editor tooling. Falls back to the asset name when empty. Never the stored key.</summary>
        public string Title => string.IsNullOrEmpty(_title) ? name : _title;

        public static implicit operator string(CollectionEntry entry)
            => entry != null ? entry.Key : string.Empty;
    }
}
