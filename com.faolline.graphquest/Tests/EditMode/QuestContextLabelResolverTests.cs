using System.Collections.Generic;
using NUnit.Framework;
using Faolline.GraphQuest.Editor;

namespace Faolline.GraphQuest.Tests
{
    /// <summary>
    /// Context Watch readability (#11): quest scoped keys and their raw objective/quest-id entries resolve to
    /// quest/objective titles. Exercises the pure resolver logic over an explicit in-memory quest set.
    /// </summary>
    public sealed class QuestContextLabelResolverTests : QuestTestBase
    {
        private IReadOnlyList<QuestGraph> Quests()
        {
            var q = TrackGraph(QuestBuilder.Create("rescue")
                .Named("Rescue Aldric")
                .AddObjective("find").Named("Find the clue").CompleteWhen(Flag("x"))
                .Build());
            return new List<QuestGraph> { q };
        }

        [Test]
        public void KeyLabel_CompletedSet_ShowsQuestTitleAndBucket()
        {
            var key = QuestContextKeys.CompletedSet("rescue");   // quest_completed:rescue
            Assert.AreEqual("Quest 'Rescue Aldric' · completed",
                QuestContextLabelResolver.LabelForKey(key, Quests()));
        }

        [Test]
        public void KeyLabel_CompletedQuestsShared_HasFixedLabel()
        {
            Assert.AreEqual("Quests completed (shared)",
                QuestContextLabelResolver.LabelForKey(QuestContextKeys.CompletedQuests, Quests()));
        }

        [Test]
        public void KeyLabel_DeadlineParam_ShowsQuestAndObjective()
        {
            var key = QuestContextKeys.DeadlineKey("rescue", "find");   // quest_deadline:rescue:find
            Assert.AreEqual("Quest 'Rescue Aldric' · deadline · 'Find the clue'",
                QuestContextLabelResolver.LabelForKey(key, Quests()));
        }

        [Test]
        public void KeyLabel_NonQuestKey_IsNull()
        {
            Assert.IsNull(QuestContextLabelResolver.LabelForKey("player_hp", Quests()));
        }

        [Test]
        public void KeyLabel_UnknownQuestId_FallsBackToRawId()
        {
            Assert.AreEqual("Quest 'ghost' · completed",
                QuestContextLabelResolver.LabelForKey(QuestContextKeys.CompletedSet("ghost"), Quests()));
        }

        [Test]
        public void EntryLabel_ObjectiveId_ResolvesToTitle()
        {
            Assert.AreEqual("Find the clue",
                QuestContextLabelResolver.LabelForEntry(QuestContextKeys.CompletedSet("rescue"), "find", Quests()));
        }

        [Test]
        public void EntryLabel_QuestRewardMarker_IsLabeled()
        {
            Assert.AreEqual("(quest completion reward)",
                QuestContextLabelResolver.LabelForEntry(QuestContextKeys.RewardedSet("rescue"),
                    QuestContextKeys.QuestRewardMarker, Quests()));
        }

        [Test]
        public void EntryLabel_InSharedCompletedQuests_ResolvesQuestTitle()
        {
            Assert.AreEqual("Rescue Aldric",
                QuestContextLabelResolver.LabelForEntry(QuestContextKeys.CompletedQuests, "rescue", Quests()));
        }

        [Test]
        public void EntryLabel_UnknownObjective_FallsBackToRawEntry()
        {
            Assert.AreEqual("mystery",
                QuestContextLabelResolver.LabelForEntry(QuestContextKeys.CompletedSet("rescue"), "mystery", Quests()));
        }
    }
}
