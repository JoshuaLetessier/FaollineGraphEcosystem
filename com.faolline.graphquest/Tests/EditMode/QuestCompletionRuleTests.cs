using NUnit.Framework;

namespace Faolline.GraphQuest.Tests
{
    public sealed class QuestCompletionRuleTests : QuestTestBase
    {
        // ── AnyRequired ──────────────────────────────────────────────────

        [Test]
        public void AnyRequired_CompletesOnFirstObjective()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .CompleteOnAny()
                .AddObjective("a").CompleteWhen(Flag("a"))
                .AddObjective("b").CompleteWhen(Flag("b"))
                .Build());
            var ctx = new QuestContext();
            var ev = new QuestEvaluator(quest, ctx);

            ev.Evaluate();
            Assert.AreEqual(QuestState.Active, ev.State);

            ctx.Set<bool>("a", true);
            ev.Evaluate();
            Assert.AreEqual(QuestState.Completed, ev.State, "one required objective done ⇒ quest completed");
        }

        [Test]
        public void AnyRequired_FailsOnlyWhenAllFailed()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .CompleteOnAny()
                .AddObjective("a").CompleteWhen(Flag("a")).FailWhen(Flag("fa"))
                .AddObjective("b").CompleteWhen(Flag("b")).FailWhen(Flag("fb"))
                .Build());
            var ctx = new QuestContext();
            var ev = new QuestEvaluator(quest, ctx);

            ctx.Set<bool>("fa", true);
            ev.Evaluate();
            Assert.AreEqual(QuestState.Active, ev.State, "one failed but another still open ⇒ Active");

            ctx.Set<bool>("fb", true);
            ev.Evaluate();
            Assert.AreEqual(QuestState.Failed, ev.State, "all required failed ⇒ quest Failed");
        }

        [Test]
        public void AnyRequired_CompletionPrecedesFailOnOtherObjectives()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .CompleteOnAny()
                .AddObjective("a").CompleteWhen(Flag("a"))
                .AddObjective("b").CompleteWhen(Flag("b")).FailWhen(Flag("fb"))
                .Build());
            var ctx = new QuestContext();
            var ev = new QuestEvaluator(quest, ctx);

            ctx.Set<bool>("fb", true);
            ctx.Set<bool>("a", true);
            ev.Evaluate();
            Assert.AreEqual(QuestState.Completed, ev.State, "one completed ⇒ quest completed even though another failed");
        }

        // ── Threshold ────────────────────────────────────────────────────

        [Test]
        public void Threshold_CompletesAtExactCount()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .CompleteOnThreshold(2)
                .AddObjective("a").CompleteWhen(Flag("a"))
                .AddObjective("b").CompleteWhen(Flag("b"))
                .AddObjective("c").CompleteWhen(Flag("c"))
                .Build());
            var ctx = new QuestContext();
            var ev = new QuestEvaluator(quest, ctx);

            ctx.Set<bool>("a", true);
            ev.Evaluate();
            Assert.AreEqual(QuestState.Active, ev.State, "1/2 threshold ⇒ still Active");

            ctx.Set<bool>("c", true);
            ev.Evaluate();
            Assert.AreEqual(QuestState.Completed, ev.State, "2/2 threshold reached ⇒ Completed");
        }

        [Test]
        public void Threshold_FailsWhenThresholdBecomesUnreachable()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .CompleteOnThreshold(2)
                .AddObjective("a").CompleteWhen(Flag("a")).FailWhen(Flag("fa"))
                .AddObjective("b").CompleteWhen(Flag("b")).FailWhen(Flag("fb"))
                .AddObjective("c").CompleteWhen(Flag("c"))
                .Build());
            var ctx = new QuestContext();
            var ev = new QuestEvaluator(quest, ctx);

            ctx.Set<bool>("fa", true);
            ev.Evaluate();
            Assert.AreEqual(QuestState.Active, ev.State, "1 failed, 2 remaining ⇒ threshold still reachable");

            ctx.Set<bool>("fb", true);
            ev.Evaluate();
            Assert.AreEqual(QuestState.Failed, ev.State, "2 failed, only 1 remaining < threshold 2 ⇒ Failed");
        }

        [Test]
        public void Threshold_OptionalObjectivesIgnored()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .CompleteOnThreshold(1)
                .AddObjective("main").CompleteWhen(Flag("m"))
                .AddObjective("bonus").Optional().CompleteWhen(Flag("bonus"))
                .Build());
            var ctx = new QuestContext();
            var ev = new QuestEvaluator(quest, ctx);

            ctx.Set<bool>("m", true);
            ev.Evaluate();
            Assert.AreEqual(QuestState.Completed, ev.State);
            Assert.AreEqual(QuestState.Active, ev.GetObjectiveState("bonus"), "optional still open");
        }

        // ── Builder wiring ───────────────────────────────────────────────

        [Test]
        public void Builder_CompleteOnAny_SetsRule()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .CompleteOnAny()
                .AddObjective("a").CompleteWhen(Flag("a"))
                .Build());
            Assert.AreEqual(QuestCompletionRule.AnyRequired, quest.CompletionRule);
        }

        [Test]
        public void Builder_CompleteOnThreshold_SetsRuleAndValue()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .CompleteOnThreshold(3)
                .AddObjective("a").CompleteWhen(Flag("a"))
                .Build());
            Assert.AreEqual(QuestCompletionRule.Threshold, quest.CompletionRule);
            Assert.AreEqual(3, quest.CompletionThreshold);
        }
    }
}
