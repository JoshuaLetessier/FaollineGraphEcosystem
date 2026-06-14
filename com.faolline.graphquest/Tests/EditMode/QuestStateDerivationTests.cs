using NUnit.Framework;

namespace Faolline.GraphQuest.Tests
{
    /// <summary>US1 — objective/quest states are derived from the context (Active/Completed/Failed, determinism).</summary>
    public sealed class QuestStateDerivationTests : QuestTestBase
    {
        [Test]
        public void Objective_IsActive_UntilCompletionHolds_ThenCompleted()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .AddObjective("a").CompleteWhen(Flag("a_done")).Build());
            var ctx = new QuestContext();
            var ev = new QuestEvaluator(quest, ctx);

            ev.Evaluate();
            Assert.AreEqual(QuestState.Active, ev.GetObjectiveState("a"));
            Assert.AreEqual(QuestState.Active, ev.State);

            ctx.Set<bool>("a_done", true);
            ev.Evaluate();
            Assert.AreEqual(QuestState.Completed, ev.GetObjectiveState("a"));
            Assert.AreEqual(QuestState.Completed, ev.State, "single required objective completed ⇒ quest completed");
        }

        [Test]
        public void FailCondition_Precedes_Completion()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .AddObjective("a").CompleteWhen(Flag("done")).FailWhen(Flag("fail")).Build());
            var ctx = new QuestContext();
            ctx.Set<bool>("done", true);
            ctx.Set<bool>("fail", true);
            var ev = new QuestEvaluator(quest, ctx);

            ev.Evaluate();
            Assert.AreEqual(QuestState.Failed, ev.GetObjectiveState("a"), "fail precedes complete");
            Assert.AreEqual(QuestState.Failed, ev.State, "a required objective failed ⇒ quest failed");
        }

        [Test]
        public void Quest_Completed_WhenAllRequiredCompleted()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .AddObjective("a").CompleteWhen(Flag("a"))
                .AddObjective("b").CompleteWhen(Flag("b"))
                .Build());
            var ctx = new QuestContext();
            var ev = new QuestEvaluator(quest, ctx);

            ctx.Set<bool>("a", true);
            ev.Evaluate();
            Assert.AreEqual(QuestState.Active, ev.State, "one required objective still open");

            ctx.Set<bool>("b", true);
            ev.Evaluate();
            Assert.AreEqual(QuestState.Completed, ev.State);
        }

        [Test]
        public void OptionalObjective_DoesNotBlockQuestCompletion()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .AddObjective("main").CompleteWhen(Flag("main_done"))
                .AddObjective("bonus").Optional().CompleteWhen(Flag("bonus_done"))
                .Build());
            var ctx = new QuestContext();
            ctx.Set<bool>("main_done", true);
            var ev = new QuestEvaluator(quest, ctx);

            ev.Evaluate();
            Assert.AreEqual(QuestState.Completed, ev.State, "optional objective open must not block the quest");
            Assert.AreEqual(QuestState.Active, ev.GetObjectiveState("bonus"));
        }

        [Test]
        public void Reevaluate_WithNoChange_IsDeterministic_AndRaisesNoDuplicateEvents()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .AddObjective("a").CompleteWhen(Flag("a_done")).Build());
            var ctx = new QuestContext();
            var ev = new QuestEvaluator(quest, ctx);

            int objEvents = 0;
            ev.OnObjectiveStateChanged += (_, __) => objEvents++;

            ev.Evaluate();                 // a -> Active (1)
            int afterFirst = objEvents;
            ev.Evaluate();                 // unchanged
            Assert.AreEqual(afterFirst, objEvents, "no duplicate events on an unchanged re-evaluation");
            Assert.AreEqual(QuestState.Active, ev.GetObjectiveState("a"), "state is identical across passes");
        }
    }
}
