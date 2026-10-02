using System.Collections.Generic;
using UnityEditor;

namespace Faolline.GraphImport.Editor
{
    /// <summary>
    /// Aligns EXISTING speakers with a speaker tables mapping: the mapping wins. Run after the plan is applied, over
    /// every speaker key the imported dialogues reference — including dialogues whose asset collided and was
    /// skipped, which is the normal case on a re-import (assets are never overwritten), so a group changed in the
    /// project's spreadsheet still reaches its speaker. A speaker the mapping does not list keeps its group.
    /// </summary>
    public static class SpeakerGroupApplier
    {
        /// <summary>
        /// For each distinct key in <paramref name="speakerKeys"/> that has an existing <c>Speaker</c> asset AND a
        /// mapping row whose group differs from the speaker's, sets the group and records the change. Saves once.
        /// </summary>
        public static IReadOnlyList<SpeakerGroupChange> Apply(IEnumerable<string> speakerKeys, SpeakerGroupMapping mapping)
        {
            var changes = new List<SpeakerGroupChange>();
            if (speakerKeys == null || mapping == null) return changes;

            var seen = new HashSet<string>();
            foreach (var key in speakerKeys)
            {
                if (string.IsNullOrEmpty(key) || !seen.Add(key)) continue;
                if (!mapping.TryGetGroup(key, out var group)) continue;

                var speaker = ProjectAssetResolver.FindExistingSpeaker(key);
                if (speaker == null) continue;

                var current = (speaker.LocalizationGroup ?? string.Empty).Trim();
                if (current == group) continue;

                speaker.LocalizationGroup = group;
                EditorUtility.SetDirty(speaker);
                changes.Add(new SpeakerGroupChange(key, AssetDatabase.GetAssetPath(speaker), current, group));
            }

            if (changes.Count > 0) AssetDatabase.SaveAssets();
            return changes;
        }
    }
}
