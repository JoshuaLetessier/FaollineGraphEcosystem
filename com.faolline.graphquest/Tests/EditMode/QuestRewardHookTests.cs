using NUnit.Framework;

namespace Faolline.GraphQuest.Tests
{
    /// <summary>US3 — reward hooks fire exactly once on the completed transition.</summary>
    public sealed class QuestRewardHookTests : QuestTestBase
    {
        [Test]
        public void ObjectiveReward_FiresExactlyOnce_OnCompletion()
        {
            var reward = Counter();
            var quest = TrackGraph(QuestBuilder.Create("q")
                .AddObjective("a").CompleteWhen(Flag("a_done")).RewardWith(reward).Build());
            var ctx = new QuestContext();
            var ev = new QuestEvaluator(quest, ctx);

            ev.Evaluate();
            Assert.AreEqual(0, reward.Count, "not completed yet");

            ctx.Set<bool>("a_done", true);
            ev.Evaluate();
            Assert.AreEqual(1, reward.Count, "fires once on completion");

            ev.Evaluate();
            ev.Evaluate();
            Assert.AreEqual(1, reward.Count, "does not re-fire on subsequent passes");
        }

        [Test]
        public void QuestReward_FiresOnce_WhenLastRequiredObjectiveCompletes()
        {
            var questReward = Counter();
            var quest = TrackGraph(QuestBuilder.Create("q")
                .AddObjective("a").CompleteWhen(Flag("a"))
                .AddObjective("b").CompleteWhen(Flag("b"))
                .RewardQuestWith(questReward)
                .Build());
            var ctx = new QuestContext();
            var ev = new QuestEvaluator(quest, ctx);

            ctx.Set<bool>("a", true);
            ev.Evaluate();
            Assert.AreEqual(0, questReward.Count, "quest not complete yet");

            ctx.Set<bool>("b", true);
            ev.Evaluate();
            Assert.AreEqual(1, questReward.Count, "quest reward fires once on quest completion");

            ev.Evaluate();
            Assert.AreEqual(1, questReward.Count, "no re-fire");
        }

        [Test]
        public void OnRewardFired_RaisedWithId()
        {
            var reward = Counter();
            var quest = TrackGraph(QuestBuilder.Create("q")
                .AddObjective("a").CompleteWhen(Flag("a_done")).RewardWith(reward).Build());
            var ctx = new QuestContext();
            var ev = new QuestEvaluator(quest, ctx);

            string fired = null;
            ev.OnRewardFired += id => fired = id;

            ctx.Set<bool>("a_done", true);
            ev.Evaluate();
            Assert.AreEqual("a", fired);
        }

        [Test]
        public void ObjectiveWithNoReward_CompletesWithoutError()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .AddObjective("a").CompleteWhen(Flag("a_done")).Build());
            var ctx = new QuestContext();
            var ev = new QuestEvaluator(quest, ctx);

            ctx.Set<bool>("a_done", true);
            Assert.DoesNotThrow(() => ev.Evaluate());
            Assert.AreEqual(QuestState.Completed, ev.GetObjectiveState("a"));
        }
    }
}
