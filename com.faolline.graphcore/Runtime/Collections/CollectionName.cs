using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Faolline.GraphCore
{
    /// <summary>
    /// A named collection as a reusable asset — drag-drop instead of typing a string key.
    /// Prevents typos, enables rename-safe references, and is visible in the Project browser.
    /// <para>
    /// <see cref="Key"/> is a stable GUID assigned once in <c>OnEnable</c> and never editable — the
    /// same stability guarantee as <see cref="BaseGraph.GraphId"/>. Renaming the asset file, or
    /// duplicating it (Ctrl+D), never changes what identifies the collection in <c>BaseContext</c>
    /// (a rename under the previous name-fallback scheme silently changed the stored bucket). Use
    /// <see cref="Title"/> for a human-readable label in editor tooling — it is purely cosmetic and
    /// never affects the stored key.
    /// </para>
    /// <para>
    /// Optionally defines a set of known <see cref="Entries"/> so the inspector can show a
    /// dropdown instead of a free-text field. Entries left empty means the collection is open
    /// (any string value, useful for dynamic/runtime items).
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Faolline/Collection Name", fileName = "NewCollection")]
    public class CollectionName : ScriptableObject, IStableGuidIdentity
    {
        // FormerlySerializedAs recovers an asset authored before this fix that had an EXPLICIT non-empty
        // _key: that string becomes the stable id as-is (no longer editable going forward). An asset that
        // relied on the old empty-key/name-fallback behaviour deserializes _id as empty and gets a fresh
        // GUID below — its old "identity" was never actually stored (it was derived from the file name at
        // runtime), so there is nothing to preserve for that case.
        [SerializeField, FormerlySerializedAs("_key"), HideInInspector] private string _id;

        [SerializeField, Tooltip("Optional display label for editor tooling. Purely cosmetic — never used as " +
            "the stored key. Falls back to the asset name when empty.")]
        private string _title;

        [SerializeField, Tooltip("Known entries for this collection (optional). When populated, the inspector " +
            "offers a dropdown on actions/conditions that reference this collection. Leave empty for open collections.")]
        private List<string> _entries = new List<string>();

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(_id))
                _id = Guid.NewGuid().ToString("D");
        }

        /// <summary>
        /// Stable GUID used as the collection key in <c>BaseContext</c>. Assigned once in
        /// <c>OnEnable</c> and never editable — renaming or duplicating the asset never changes it.
        /// </summary>
        public string Key => _id;

        // Explicit implementation: discoverable by the editor's stable-id duplicate detector with no
        // per-type code in the detector itself. Kept out of the normal public surface (Key already exposes
        // this under its own name).
        string IStableGuidIdentity.StableId => _id;
        string IStableGuidIdentity.StableIdFieldName => nameof(_id);

        /// <summary>Human-readable label for editor tooling. Falls back to the asset name when empty. Never the stored key.</summary>
        public string Title => string.IsNullOrEmpty(_title) ? name : _title;

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
