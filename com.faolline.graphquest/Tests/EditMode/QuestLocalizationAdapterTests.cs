using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Faolline.GraphLocalization;
using Faolline.GraphQuest.Editor;

namespace Faolline.GraphQuest.Tests
{
    /// <summary>
    /// The localization adapter must emit quest keys under the SAME effective id the evaluator queries. A quest
    /// authored with no explicit QuestId (relying on the GraphId fallback) previously emitted nothing while the
    /// evaluator looked up quest_&lt;GraphId&gt; → a permanent audit miss. #5 dogfood finding.
    /// </summary>
    public sealed class QuestLocalizationAdapterTests : QuestTestBase
    {
        private static System.Collections.Generic.List<string> ExtractKeys(QuestGraph quest, out string effectiveId)
        {
            effectiveId = quest.ResolveQuestId();
            var adapter = new QuestGraphLocalizationAdapter();
            var entry = new LocalizationGraphEntry { GraphGuid = quest.GraphId, GraphName = "Q" };
            var mi = typeof(QuestGraphLocalizationAdapter)
                .GetMethod("ExtractGraphKeys", BindingFlags.NonPublic | BindingFlags.Instance);
            mi.Invoke(adapter, new object[] { quest, entry });
            return entry.Keys.Select(k => k.Key).ToList();
        }

        [Test]
        public void ResolveQuestId_FallsBackToGraphId_WhenQuestIdEmpty()
        {
            var withId = TrackGraph(QuestBuilder.Create("rescue").AddObjective("o").CompleteWhen(Flag("x")).Build());
            Assert.AreEqual("rescue", withId.ResolveQuestId());

            var noId = TrackGraph(QuestBuilder.Create("").AddObjective("o").CompleteWhen(Flag("x")).Build());
            Assert.IsEmpty(noId.QuestId);
            Assert.AreEqual(noId.GraphId, noId.ResolveQuestId(), "empty QuestId → the stable GraphId");
        }

        [Test]
        public void Adapter_EmptyQuestId_EmitsQuestKeyUnderGraphId()
        {
            var quest = TrackGraph(QuestBuilder.Create("")
                .Named("Rescue Aldric").Describe("Save him.")
                .AddObjective("find").Named("Find the clue").CompleteWhen(Flag("x"))
                .Build());

            var keys = ExtractKeys(quest, out var effectiveId);

            Assert.AreEqual(quest.GraphId, effectiveId);
            Assert.Contains(QuestLocalizationKeys.ForQuest(effectiveId), keys,
                "the quest name key must use the effective (GraphId) id the evaluator queries");
            Assert.Contains(QuestLocalizationKeys.ForQuestDescription(effectiveId), keys,
                "the quest description key must also use the effective id");
        }

        [Test]
        public void Adapter_ExplicitQuestId_EmitsQuestKeyUnderThatId()
        {
            var quest = TrackGraph(QuestBuilder.Create("rescue")
                .Named("Rescue Aldric")
                .AddObjective("find").Named("Find the clue").CompleteWhen(Flag("x"))
                .Build());

            var keys = ExtractKeys(quest, out _);

            Assert.Contains(QuestLocalizationKeys.ForQuest("rescue"), keys);
        }
    }
}
