using NUnit.Framework;
using UnityEngine;
using Faolline.GraphSave;

namespace Faolline.GraphQuest.Tests
{
    /// <summary>Cross-quest chaining — a quest unlocks once prerequisite quests are Completed (shared context set).</summary>
    public sealed class QuestChainingTests : QuestTestBase
    {
        [Test]
        public void Quest_GatedByAnother_UnlocksWhenThatQuestCompletes()
        {
            var qA = TrackGraph(QuestBuilder.Create("A")
                .AddObjective("a").CompleteWhen(Flag("a_done")).Build());
            var qB = TrackGraph(QuestBuilder.Create("B")
                .UnlockWhen(QuestDone("A"))
                .AddObjective("b").CompleteWhen(Flag("b_done")).Build());

            var ctx = new QuestContext();
            var evA = new QuestEvaluator(qA, ctx);
            var evB = new QuestEvaluator(qB, ctx);

            evA.Evaluate();
            evB.Evaluate();
            Assert.AreEqual(QuestState.Locked, evB.State, "B is gated by A");
            Assert.AreEqual(QuestState.Locked, evB.GetObjectiveState("b"));

            ctx.Set<bool>("a_done", true);
            evA.Evaluate();   // A completes → records "A" into the shared CompletedQuests set
            evB.Evaluate();   // B sees it and unlocks
            Assert.AreEqual(QuestState.Completed, evA.State);
            Assert.AreEqual(QuestState.Active, evB.State);
            Assert.AreEqual(QuestState.Active, evB.GetObjectiveState("b"));
        }

        [Test]
        public void UnlockAfter_RequiresAllListedQuests()
        {
            var qA = TrackGraph(QuestBuilder.Create("A").AddObjective("a").CompleteWhen(Flag("a")).Build());
            var qB = TrackGraph(QuestBuilder.Create("B").AddObjective("b").CompleteWhen(Flag("b")).Build());
            var qC = TrackGraph(QuestBuilder.Create("C")
                .UnlockAfter("A", "B")
                .AddObjective("c").CompleteWhen(Flag("c")).Build());

            var ctx = new QuestContext();
            var evA = new QuestEvaluator(qA, ctx);
            var evB = new QuestEvaluator(qB, ctx);
            var evC = new QuestEvaluator(qC, ctx);

            ctx.Set<bool>("a", true);
            evA.Evaluate(); evB.Evaluate(); evC.Evaluate();
            Assert.AreEqual(QuestState.Locked, evC.State, "only A done — C needs A and B");

            ctx.Set<bool>("b", true);
            evB.Evaluate(); evC.Evaluate();
            Assert.AreEqual(QuestState.Active, evC.State, "A and B done — C unlocks");
        }

        [Test]
        public void Chaining_SurvivesSaveLoad()
        {
            var qA = TrackGraph(QuestBuilder.Create("A").AddObjective("a").CompleteWhen(Flag("a")).Build());
            var qB = TrackGraph(QuestBuilder.Create("B").UnlockWhen(QuestDone("A"))
                .AddObjective("b").CompleteWhen(Flag("b")).Build());

            var ctx = new QuestContext();
            ctx.Set<bool>("a", true);
            new QuestEvaluator(qA, ctx).Evaluate();   // A completed → "A" in CompletedQuests

            var restored = new QuestContext();
            GraphRunSnapshot.Capture(ctx, "A", null).ApplyTo(restored);

            var evB = new QuestEvaluator(qB, restored);
            evB.Evaluate();
            Assert.AreEqual(QuestState.Active, evB.State, "B unlocks after restore — chaining state persisted");
        }

        [Test]
        public void Reset_OfPrerequisite_RelocksTheChainedQuest()
        {
            var qA = TrackGraph(QuestBuilder.Create("A").AddObjective("a").CompleteWhen(Flag("a")).Build());
            var qB = TrackGraph(QuestBuilder.Create("B").UnlockWhen(QuestDone("A"))
                .AddObjective("b").CompleteWhen(Flag("b")).Build());

            var ctx = new QuestContext();
            ctx.Set<bool>("a", true);
            var evA = new QuestEvaluator(qA, ctx);
            var evB = new QuestEvaluator(qB, ctx);
            evA.Evaluate(); evB.Evaluate();
            Assert.AreEqual(QuestState.Active, evB.State);

            evA.Reset();      // un-completes A (removes it from CompletedQuests)
            evB.Evaluate();
            Assert.AreEqual(QuestState.Locked, evB.State, "resetting A re-locks B");
        }
    }
}
