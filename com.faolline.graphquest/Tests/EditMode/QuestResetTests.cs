using NUnit.Framework;

namespace Faolline.GraphQuest.Tests
{
    /// <summary>Replay — Reset() clears this quest's scoped progress (and lets one-shot rewards fire again).</summary>
    public sealed class QuestResetTests : QuestTestBase
    {
        [Test]
        public void Reset_ClearsProgress_AndAllowsRewardToFireAgain()
        {
            var reward = Counter();
            var quest = TrackGraph(QuestBuilder.Create("q")
                .AddObjective("a").CompleteWhen(Flag("a_done")).RewardWith(reward).Build());
            var ctx = new QuestContext();
            ctx.Set<bool>("a_done", true);
            var ev = new QuestEvaluator(quest, ctx);

            ev.Evaluate();
            Assert.AreEqual(QuestState.Completed, ev.GetObjectiveState("a"));
            Assert.AreEqual(1, reward.Count);

            ev.Reset();
            Assert.AreEqual(QuestState.Active, ev.GetObjectiveState("a"),
                "after reset the completed-set is cleared ⇒ back to Active");

            ev.Evaluate(); // a_done still holds ⇒ completes again
            Assert.AreEqual(QuestState.Completed, ev.GetObjectiveState("a"));
            Assert.AreEqual(2, reward.Count, "reset cleared the rewarded-set ⇒ the one-shot reward fires again");
        }

        [Test]
        public void Reset_OfOneQuest_DoesNotAffectAnother_SharingTheContext()
        {
            var q1 = TrackGraph(QuestBuilder.Create("q1").AddObjective("x").CompleteWhen(Flag("x")).Build());
            var q2 = TrackGraph(QuestBuilder.Create("q2").AddObjective("y").CompleteWhen(Flag("y")).Build());
            var ctx = new QuestContext();
            ctx.Set<bool>("x", true);
            ctx.Set<bool>("y", true);
            var e1 = new QuestEvaluator(q1, ctx);
            var e2 = new QuestEvaluator(q2, ctx);
            e1.Evaluate();
            e2.Evaluate();
            Assert.AreEqual(QuestState.Completed, e1.GetObjectiveState("x"));
            Assert.AreEqual(QuestState.Completed, e2.GetObjectiveState("y"));

            e1.Reset();
            Assert.AreEqual(QuestState.Active, e1.GetObjectiveState("x"), "q1 reset");
            Assert.AreEqual(QuestState.Completed, e2.GetObjectiveState("y"), "q2 progress is untouched (scoped keys)");
        }
    }
}
