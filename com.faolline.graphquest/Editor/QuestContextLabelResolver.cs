using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphQuest.Editor
{
    /// <summary>
    /// Makes quest context keys readable in the Context Watch window: turns the scoped collection keys
    /// (<c>quest_completed:&lt;questId&gt;</c>, <c>quest_done</c>, the <c>quest_deadline:…</c> param) and their
    /// raw objective/quest-id entries into quest/objective titles. Registered with graphcore's
    /// <see cref="ContextKeyLabelRegistry"/> on load; the resolution logic is pure and unit-tested via the
    /// <c>internal</c> overloads (the public ones scan <see cref="QuestGraph"/> assets).
    /// </summary>
    public sealed class QuestContextLabelResolver : IContextLabelResolver
    {
        public string LabelForKey(string key) => LabelForKey(key, LoadQuests());
        public string LabelForEntry(string collectionKey, string entry) => LabelForEntry(collectionKey, entry, LoadQuests());

        // ── Pure logic (unit-tested) ──────────────────────────────────────────

        /// <summary>Pure key→label over an explicit quest set (the asset-scanning overload delegates here).</summary>
        public static string LabelForKey(string key, IReadOnlyList<QuestGraph> quests)
        {
            if (string.IsNullOrEmpty(key)) return null;

            if (key == QuestContextKeys.CompletedQuests) return "Quests completed (shared)";

            var parts = key.Split(':');
            var bucket = BucketLabel(parts[0]);

            if (parts[0] == QuestContextKeys.Deadline)
            {
                // quest_deadline:<questId>:<objectiveId>
                if (parts.Length < 3) return null;
                return $"Quest '{QuestTitle(parts[1], quests)}' · deadline · '{ObjectiveTitle(parts[1], parts[2], quests)}'";
            }

            if (bucket == null || parts.Length < 2) return null;   // not a per-quest scoped set
            return $"Quest '{QuestTitle(parts[1], quests)}' · {bucket}";
        }

        /// <summary>Pure entry→label over an explicit quest set (the asset-scanning overload delegates here).</summary>
        public static string LabelForEntry(string collectionKey, string entry, IReadOnlyList<QuestGraph> quests)
        {
            if (string.IsNullOrEmpty(collectionKey) || string.IsNullOrEmpty(entry)) return null;

            if (collectionKey == QuestContextKeys.CompletedQuests)
                return QuestTitle(entry, quests);   // entries are quest ids

            var parts = collectionKey.Split(':');
            if (BucketLabel(parts[0]) == null || parts.Length < 2) return null;

            if (entry == QuestContextKeys.QuestRewardMarker) return "(quest completion reward)";
            return ObjectiveTitle(parts[1], entry, quests);   // entries are objective ids
        }

        private static string BucketLabel(string prefix)
        {
            if (prefix == QuestContextKeys.Completed) return "completed";
            if (prefix == QuestContextKeys.Failed)    return "failed";
            if (prefix == QuestContextKeys.Rewarded)  return "rewarded";
            if (prefix == QuestContextKeys.Abandoned) return "abandoned";
            return null;
        }

        private static QuestGraph Find(string questId, IReadOnlyList<QuestGraph> quests)
            => quests?.FirstOrDefault(q => q != null && q.ResolveQuestId() == questId);

        private static string QuestTitle(string questId, IReadOnlyList<QuestGraph> quests)
        {
            var q = Find(questId, quests);
            return q != null && !string.IsNullOrEmpty(q.DisplayName) ? q.DisplayName : questId;
        }

        private static string ObjectiveTitle(string questId, string objectiveId, IReadOnlyList<QuestGraph> quests)
        {
            var q = Find(questId, quests);
            if (q != null)
                foreach (var node in q.Nodes)
                    if (node is ObjectiveNodeData obj && obj.Id == objectiveId)
                        return string.IsNullOrEmpty(obj.Title) ? objectiveId : obj.Title;
            return objectiveId;
        }

        // ── Asset-backed lookup ───────────────────────────────────────────────

        private static QuestGraph[] LoadQuests()
            => AssetDatabase.FindAssets("t:QuestGraph")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<QuestGraph>)
                .Where(q => q != null)
                .ToArray();

        [InitializeOnLoadMethod]
        private static void Register() => ContextKeyLabelRegistry.Register(new QuestContextLabelResolver());
    }
}
