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
    /// this asset, or of a category's group, means "log everything" — the same non-destructive
    /// default every other settings asset in this ecosystem uses.
    ///
    /// Verbosity is set per GROUP (the prefix before the first '.' in a category, e.g. "GraphCore"
    /// for "GraphCore.Context") rather than per category: silence or enable a whole lib in one place,
    /// and any category discovered later under that group inherits the same default automatically. A
    /// per-category override exists only for the rare case where one category needs to diverge from
    /// its group — and is dropped automatically once it stops diverging, so the exception list never
    /// accumulates stale entries.
    /// </summary>
    public class GraphLoggingSettings : ScriptableObject
    {
        [Serializable]
        public sealed class GroupEntry
        {
            public string Prefix;
            public bool DefaultInfoEnabled = true;
            public bool DefaultWarningEnabled = true;
            public List<string> KnownCategories = new List<string>();
        }

        [Serializable]
        public sealed class CategoryOverride
        {
            public string Category;
            public bool InfoEnabled;
            public bool WarningEnabled;
        }

        [SerializeField]
        private List<GroupEntry> _groups = new List<GroupEntry>();

        [SerializeField]
        private List<CategoryOverride> _overrides = new List<CategoryOverride>();

        public IReadOnlyList<GroupEntry> Groups => _groups;
        public IReadOnlyList<CategoryOverride> Overrides => _overrides;

        public bool IsInfoEnabled(string category)
        {
            var over = FindOverride(category);
            if (over != null) return over.InfoEnabled;
            return FindGroup(GroupPrefixOf(category))?.DefaultInfoEnabled ?? true;
        }

        public bool IsWarningEnabled(string category)
        {
            var over = FindOverride(category);
            if (over != null) return over.WarningEnabled;
            return FindGroup(GroupPrefixOf(category))?.DefaultWarningEnabled ?? true;
        }

        /// <summary>The group key a category rolls up under: the segment before its first '.', or the whole string if there is none.</summary>
        public static string GroupPrefixOf(string category)
        {
            if (string.IsNullOrEmpty(category)) return category;
            var dot = category.IndexOf('.');
            return dot > 0 ? category.Substring(0, dot) : category;
        }

        private GroupEntry FindGroup(string prefix)
        {
            foreach (var group in _groups)
                if (group.Prefix == prefix) return group;
            return null;
        }

        private CategoryOverride FindOverride(string category)
        {
            foreach (var over in _overrides)
                if (over.Category == category) return over;
            return null;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Registers a category's group the first time it is seen (default: both levels enabled), so
        /// the settings inspector grows organically as packages adopt <see cref="Logging"/> — no
        /// upfront registry required. No-op outside the Editor.
        /// </summary>
        public void EnsureCategoryKnown(string category)
        {
            if (string.IsNullOrEmpty(category)) return;

            var prefix = GroupPrefixOf(category);
            var group = FindGroup(prefix);
            if (group == null)
            {
                group = new GroupEntry { Prefix = prefix };
                _groups.Add(group);
                EditorUtility.SetDirty(this);
            }

            if (!group.KnownCategories.Contains(category))
            {
                group.KnownCategories.Add(category);
                EditorUtility.SetDirty(this);
            }
        }

        /// <summary>Sets a group's default Info toggle, pruning any per-category override that no longer diverges from it.</summary>
        public void SetGroupInfoEnabled(string prefix, bool enabled)
        {
            var group = FindGroup(prefix);
            if (group == null) return;
            group.DefaultInfoEnabled = enabled;
            PruneNonDivergingOverrides(prefix);
            EditorUtility.SetDirty(this);
        }

        /// <summary>Sets a group's default Warning toggle, pruning any per-category override that no longer diverges from it.</summary>
        public void SetGroupWarningEnabled(string prefix, bool enabled)
        {
            var group = FindGroup(prefix);
            if (group == null) return;
            group.DefaultWarningEnabled = enabled;
            PruneNonDivergingOverrides(prefix);
            EditorUtility.SetDirty(this);
        }

        /// <summary>Overrides one category's Info toggle away from its group default, or clears the override once it matches again.</summary>
        public void SetCategoryInfoEnabled(string category, bool enabled) =>
            ApplyCategoryOverride(category, enabled, IsWarningEnabled(category));

        /// <summary>Overrides one category's Warning toggle away from its group default, or clears the override once it matches again.</summary>
        public void SetCategoryWarningEnabled(string category, bool enabled) =>
            ApplyCategoryOverride(category, IsInfoEnabled(category), enabled);

        private void ApplyCategoryOverride(string category, bool infoEnabled, bool warningEnabled)
        {
            var group = FindGroup(GroupPrefixOf(category));
            var matchesGroupDefault = group != null
                && group.DefaultInfoEnabled == infoEnabled
                && group.DefaultWarningEnabled == warningEnabled;

            var existing = FindOverride(category);
            if (matchesGroupDefault)
            {
                if (existing != null) _overrides.Remove(existing);
            }
            else if (existing != null)
            {
                existing.InfoEnabled = infoEnabled;
                existing.WarningEnabled = warningEnabled;
            }
            else
            {
                _overrides.Add(new CategoryOverride { Category = category, InfoEnabled = infoEnabled, WarningEnabled = warningEnabled });
            }

            EditorUtility.SetDirty(this);
        }

        private void PruneNonDivergingOverrides(string prefix)
        {
            var group = FindGroup(prefix);
            if (group == null) return;
            _overrides.RemoveAll(o =>
                GroupPrefixOf(o.Category) == prefix
                && o.InfoEnabled == group.DefaultInfoEnabled
                && o.WarningEnabled == group.DefaultWarningEnabled);
        }
#endif
    }
}
