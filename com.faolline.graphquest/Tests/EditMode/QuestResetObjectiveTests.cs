using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphQuest.Tests
{
    /// <summary>
    /// Single-objective rewind (#3, Cryptique backlog): <see cref="QuestEvaluator.ResetObjective"/> clears one
    /// objective's completed/failed/rewarded bookkeeping and disarms its timer, leaving siblings untouched.
    /// </summary>
    public sealed class QuestResetObjectiveTests : QuestTestBase
    {
        [Test]
        public void ResetObjective_ClearsCompletedObjective_BackToActive()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .AddObjective("a").CompleteWhen(Flag("a_done"))
                .Build());
            var ctx = new QuestContext();
            var ev = new QuestEvaluator(quest, ctx);

            ctx.Set<bool>("a_done", true);
            ev.Evaluate();
            Assert.AreEqual(QuestState.Completed, ev.GetObjectiveState("a"));

            ctx.Set<bool>("a_done", false);          // clear the world input so it won't re-complete
            ev.ResetObjective("a");
            Assert.AreEqual(QuestState.Active, ev.GetObjectiveState("a"));
        }

        [Test]
        public void ResetObjective_ClearsFailedObjective_BackToActive()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .AddObjective("a").CompleteWhen(Flag("a_done")).FailWhen(Flag("a_failed"))
                .Build());
            var ctx = new QuestContext();
            var ev = new QuestEvaluator(quest, ctx);

            ctx.Set<bool>("a_failed", true);
            ev.Evaluate();
            Assert.AreEqual(QuestState.Failed, ev.GetObjectiveState("a"));

            ctx.Set<bool>("a_failed", false);
            ev.ResetObjective("a");
            Assert.AreEqual(QuestState.Active, ev.GetObjectiveState("a"));
        }

        [Test]
        public void ResetObjective_OnlyAffectsNamedObjective()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .AddObjective("a").CompleteWhen(Flag("a_done"))
                .AddObjective("b").CompleteWhen(Flag("b_done"))
                .Build());
            var ctx = new QuestContext();
            var ev = new QuestEvaluator(quest, ctx);

            ctx.Set<bool>("a_done", true);
            ctx.Set<bool>("b_done", true);
            ev.Evaluate();
            Assert.AreEqual(QuestState.Completed, ev.GetObjectiveState("a"));
            Assert.AreEqual(QuestState.Completed, ev.GetObjectiveState("b"));

            ctx.Set<bool>("a_done", false);
            ev.ResetObjective("a");
            Assert.AreEqual(QuestState.Active, ev.GetObjectiveState("a"));
            Assert.AreEqual(QuestState.Completed, ev.GetObjectiveState("b"), "sibling objective is untouched");
        }

        [Test]
        public void ResetObjective_RearmsTimer()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .AddObjective("a").CompleteWhen(Flag("a_done")).WithTimeLimit(5f)
                .Build());
            var ctx = new QuestContext();
            var ev = new QuestEvaluator(quest, ctx);

            ev.Evaluate(0f);                          // arms the deadline (now + 5)
            ev.Evaluate(3f);                          // 2s left
            Assert.AreEqual(2f, ev.GetRemainingSeconds("a"), 0.001f);

            ev.ResetObjective("a");                   // disarm → full limit again
            Assert.AreEqual(5f, ev.GetRemainingSeconds("a"), 0.001f);
        }

        [Test]
        public void ResetObjective_LetsRewardFireAgain()
        {
            var reward = ScriptableObject.CreateInstance<SetBoolAction>();
            reward.Parameter = Track(ParameterName.Bool("got_reward")); reward.Value = true;
            var quest = TrackGraph(QuestBuilder.Create("q")
                .AddObjective("a").CompleteWhen(Flag("a_done")).RewardWith(reward)
                .Build());
            var ctx = new QuestContext();
            var ev = new QuestEvaluator(quest, ctx);

            int rewardCount = 0;
            ev.OnRewardFired += id => { if (id == "a") rewardCount++; };

            ctx.Set<bool>("a_done", true);
            ev.Evaluate();
            Assert.AreEqual(1, rewardCount);

            ev.ResetObjective("a");                   // clears the rewarded-guard for "a"
            ev.Evaluate();                            // a_done still true → re-completes, reward fires again
            Assert.AreEqual(2, rewardCount, "the one-shot reward can fire again after a reset");
        }

        [Test]
        public void ResetObjective_NullOrEmptyId_IsNoOp()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .AddObjective("a").CompleteWhen(Flag("a_done")).Build());
            var ev = new QuestEvaluator(quest, new QuestContext());
            Assert.DoesNotThrow(() => { ev.ResetObjective(null); ev.ResetObjective(""); });
        }
    }
}
