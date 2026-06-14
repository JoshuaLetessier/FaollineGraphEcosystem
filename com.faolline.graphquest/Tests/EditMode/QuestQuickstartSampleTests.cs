using NUnit.Framework;

namespace Faolline.GraphQuest.Tests
{
    /// <summary>End-to-end walk of the README/quickstart "rescue" quest (chain + optional + DAG + rewards).</summary>
    public sealed class QuestQuickstartSampleTests : QuestTestBase
    {
        [Test]
        public void RescueQuest_WalksFromStartToCompletion()
        {
            var potion = Counter();
            var freedom = Counter();

            var rescue = TrackGraph(QuestBuilder.Create("rescue")
                .AddObjective("find_clue").CompleteWhen(Flag("found_clue"))
                .AddObjective("pick_lock").Requires("find_clue").CompleteWhen(Flag("lock_open"))
                .AddObjective("gather_herbs").Optional().CompleteWhen(Flag("has_herbs")).RewardWith(potion)
                .AddObjective("escape").Requires("pick_lock").CompleteWhen(Flag("outside"))
                .RewardQuestWith(freedom)
                .Build());

            var ctx = new QuestContext();
            var quest = new QuestEvaluator(rescue, ctx);

            quest.Evaluate();
            Assert.AreEqual(QuestState.Active, quest.GetObjectiveState("find_clue"));
            Assert.AreEqual(QuestState.Locked, quest.GetObjectiveState("pick_lock"));
            Assert.AreEqual(QuestState.Active, quest.GetObjectiveState("gather_herbs"));
            Assert.AreEqual(QuestState.Active, quest.State);

            ctx.Set<bool>("found_clue", true);
            quest.Evaluate();
            Assert.AreEqual(QuestState.Completed, quest.GetObjectiveState("find_clue"));
            Assert.AreEqual(QuestState.Active, quest.GetObjectiveState("pick_lock"));

            ctx.Set<bool>("lock_open", true);
            ctx.Set<bool>("outside", true);
            quest.Evaluate();
            Assert.AreEqual(QuestState.Completed, quest.GetObjectiveState("escape"));
            Assert.AreEqual(QuestState.Completed, quest.State, "all required objectives done (herbs is optional)");
            Assert.AreEqual(1, freedom.Count, "quest reward fired once");
            Assert.AreEqual(0, potion.Count, "optional herbs not gathered ⇒ its reward never fired");

            // The optional objective can still be completed after the quest finished.
            ctx.AddToCollection("herbs", "a");
            ctx.Set<bool>("has_herbs", true);
            quest.Evaluate();
            Assert.AreEqual(QuestState.Completed, quest.GetObjectiveState("gather_herbs"));
            Assert.AreEqual(1, potion.Count, "optional reward fires once when finally completed");
        }
    }
}
