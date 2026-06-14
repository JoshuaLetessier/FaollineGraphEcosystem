using NUnit.Framework;

namespace Faolline.GraphQuest.Tests
{
    /// <summary>US2 — prerequisites gate objectives/quests (linear chains, DAG joins, quest unlock).</summary>
    public sealed class QuestPrerequisiteGatingTests : QuestTestBase
    {
        [Test]
        public void Chain_GatesUntilPrerequisiteCompletes()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .AddObjective("a").CompleteWhen(Flag("a_done"))
                .AddObjective("b").Requires("a").CompleteWhen(Flag("b_done"))
                .Build());
            var ctx = new QuestContext();
            var ev = new QuestEvaluator(quest, ctx);

            ev.Evaluate();
            Assert.AreEqual(QuestState.Active, ev.GetObjectiveState("a"));
            Assert.AreEqual(QuestState.Locked, ev.GetObjectiveState("b"), "b is gated by a");

            ctx.Set<bool>("a_done", true);
            ev.Evaluate();
            Assert.AreEqual(QuestState.Completed, ev.GetObjectiveState("a"));
            Assert.AreEqual(QuestState.Active, ev.GetObjectiveState("b"), "a complete unlocks b");
        }

        [Test]
        public void Diamond_DAG_GatesUntilBothPrerequisitesComplete()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .AddObjective("a").CompleteWhen(Flag("a"))
                .AddObjective("b").Requires("a").CompleteWhen(Flag("b"))
                .AddObjective("c").Requires("a").CompleteWhen(Flag("c"))
                .AddObjective("d").Requires("b", "c").CompleteWhen(Flag("d"))
                .Build());
            var ctx = new QuestContext();
            var ev = new QuestEvaluator(quest, ctx);

            ctx.Set<bool>("a", true);
            ev.Evaluate();
            Assert.AreEqual(QuestState.Active, ev.GetObjectiveState("b"));
            Assert.AreEqual(QuestState.Active, ev.GetObjectiveState("c"));
            Assert.AreEqual(QuestState.Locked, ev.GetObjectiveState("d"), "d needs both b and c");

            ctx.Set<bool>("b", true);
            ev.Evaluate();
            Assert.AreEqual(QuestState.Locked, ev.GetObjectiveState("d"), "only b done — still gated");

            ctx.Set<bool>("c", true);
            ev.Evaluate();
            Assert.AreEqual(QuestState.Active, ev.GetObjectiveState("d"), "both b and c done — d unlocks");
        }

        [Test]
        public void QuestUnlockCondition_LocksWholeQuest_UntilMet()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .UnlockWhen(Flag("quest_open"))
                .AddObjective("a").CompleteWhen(Flag("a_done"))
                .Build());
            var ctx = new QuestContext();
            var ev = new QuestEvaluator(quest, ctx);

            ev.Evaluate();
            Assert.AreEqual(QuestState.Locked, ev.State);
            Assert.AreEqual(QuestState.Locked, ev.GetObjectiveState("a"), "no Active objective while the quest is locked");

            ctx.Set<bool>("quest_open", true);
            ev.Evaluate();
            Assert.AreEqual(QuestState.Active, ev.State);
            Assert.AreEqual(QuestState.Active, ev.GetObjectiveState("a"));
        }
    }
}
