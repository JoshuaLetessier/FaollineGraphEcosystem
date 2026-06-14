using NUnit.Framework;
using UnityEngine;
using Faolline.GraphSave;

namespace Faolline.GraphQuest.Tests
{
    /// <summary>US4 — quest progress round-trips through a graphsave context snapshot (state in collections).</summary>
    public sealed class QuestPersistenceTests : QuestTestBase
    {
        [Test]
        public void Progress_RoundTrips_ThroughGraphSaveSnapshot()
        {
            var reward = Counter();
            var quest = TrackGraph(QuestBuilder.Create("q")
                .AddObjective("a").CompleteWhen(Flag("a_done")).RewardWith(reward)
                .AddObjective("b").Requires("a").CompleteWhen(Flag("b_done"))
                .Build());

            // Play to a partial state: a completed (reward fired), b active.
            var ctx = new QuestContext();
            ctx.Set<bool>("a_done", true);
            var ev = new QuestEvaluator(quest, ctx);
            ev.Evaluate();
            Assert.AreEqual(QuestState.Completed, ev.GetObjectiveState("a"));
            Assert.AreEqual(QuestState.Active, ev.GetObjectiveState("b"));
            Assert.AreEqual(1, reward.Count);

            // SAVE: capture + JSON round-trip (what a store does), then restore into a fresh context.
            var snap = GraphRunSnapshot.Capture(ctx, quest.QuestId, null);
            var back = JsonUtility.FromJson<GraphRunSnapshot>(JsonUtility.ToJson(snap));
            var restored = new QuestContext();
            back.ApplyTo(restored);

            var ev2 = new QuestEvaluator(quest, restored);
            ev2.Evaluate();

            Assert.AreEqual(QuestState.Completed, ev2.GetObjectiveState("a"), "completed state restored");
            Assert.AreEqual(QuestState.Active, ev2.GetObjectiveState("b"), "active state restored");
            Assert.AreEqual(1, reward.Count, "already-fired reward must not fire again after restore");
        }

        [Test]
        public void FailedState_RoundTrips()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .AddObjective("a").CompleteWhen(Flag("done")).FailWhen(Flag("fail"))
                .Build());
            var ctx = new QuestContext();
            ctx.Set<bool>("fail", true);
            var ev = new QuestEvaluator(quest, ctx);
            ev.Evaluate();
            Assert.AreEqual(QuestState.Failed, ev.GetObjectiveState("a"));

            var snap = GraphRunSnapshot.Capture(ctx, quest.QuestId, null);
            var restored = new QuestContext();
            snap.ApplyTo(restored);
            var ev2 = new QuestEvaluator(quest, restored);
            ev2.Evaluate();
            Assert.AreEqual(QuestState.Failed, ev2.GetObjectiveState("a"), "failed state restored");
        }
    }
}
