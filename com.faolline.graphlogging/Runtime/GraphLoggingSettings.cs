using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Faolline.GraphLogging
{
    /// <summary>
    /// Project-wide logging configuration asset. One per project, stored in Resources. Absence of
    /// this asset, or of a given category within it, means "log everything" — the same non-destructive
    /// default every other settings asset in this ecosystem uses.
    /// </summary>
    public class GraphLoggingSettings : ScriptableObject
    {
        [Serializable]
        public sealed class CategoryEntry
        {
            public string Category;
            public bool InfoEnabled = true;
            public bool WarningEnabled = true;
        }

        [SerializeField]
        private List<CategoryEntry> _categories = new List<CategoryEntry>();

        public IReadOnlyList<CategoryEntry> Categories => _categories;

        public bool IsInfoEnabled(string category) => Find(category)?.InfoEnabled ?? true;
        public bool IsWarningEnabled(string category) => Find(category)?.WarningEnabled ?? true;

        private CategoryEntry Find(string category)
        {
            foreach (var entry in _categories)
                if (entry.Category == category) return entry;
            return null;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Registers a category the first time it is seen (default: both levels enabled), so the
        /// settings inspector grows organically as packages adopt <see cref="GraphLogging"/> — no
        /// upfront registry required. No-op outside the Editor.
        /// </summary>
        public void EnsureCategoryKnown(string category)
        {
            if (string.IsNullOrEmpty(category) || Find(category) != null) return;
            _categories.Add(new CategoryEntry { Category = category });
            EditorUtility.SetDirty(this);
        }
#endif
    }
}
