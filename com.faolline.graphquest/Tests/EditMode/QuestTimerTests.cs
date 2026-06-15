using NUnit.Framework;

namespace Faolline.GraphQuest.Tests
{
    /// <summary>Time-limited objectives — fail on deadline, succeed if completed in time, ignored without a clock.</summary>
    public sealed class QuestTimerTests : QuestTestBase
    {
        [Test]
        public void TimedObjective_Fails_WhenDeadlinePasses()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .AddObjective("defuse").WithTimeLimit(10f).CompleteWhen(Flag("defused")).Build());
            var ctx = new QuestContext();
            var ev = new QuestEvaluator(quest, ctx);

            ev.Evaluate(0f);   // arms the deadline at t=10
            Assert.AreEqual(QuestState.Active, ev.GetObjectiveState("defuse"));
            ev.Evaluate(5f);
            Assert.AreEqual(QuestState.Active, ev.GetObjectiveState("defuse"));
            ev.Evaluate(10f);  // deadline reached
            Assert.AreEqual(QuestState.Failed, ev.GetObjectiveState("defuse"));
        }

        [Test]
        public void TimedObjective_Completing_BeforeDeadline_Succeeds()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .AddObjective("defuse").WithTimeLimit(10f).CompleteWhen(Flag("defused")).Build());
            var ctx = new QuestContext();
            var ev = new QuestEvaluator(quest, ctx);

            ev.Evaluate(0f);
            ctx.Set<bool>("defused", true);
            ev.Evaluate(9f);
            Assert.AreEqual(QuestState.Completed, ev.GetObjectiveState("defuse"));
            ev.Evaluate(20f);  // past the deadline, but already completed
            Assert.AreEqual(QuestState.Completed, ev.GetObjectiveState("defuse"));
        }

        [Test]
        public void Evaluate_WithoutClock_DoesNotEnforceTimers()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .AddObjective("defuse").WithTimeLimit(10f).CompleteWhen(Flag("defused")).Build());
            var ctx = new QuestContext();
            var ev = new QuestEvaluator(quest, ctx);

            ev.Evaluate();
            ev.Evaluate();
            Assert.AreEqual(QuestState.Active, ev.GetObjectiveState("defuse"), "no clock ⇒ the timer never fires");
        }

        [Test]
        public void GetRemainingSeconds_CountsDown()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .AddObjective("defuse").WithTimeLimit(10f).CompleteWhen(Flag("defused")).Build());
            var ctx = new QuestContext();
            var ev = new QuestEvaluator(quest, ctx);

            Assert.AreEqual(10f, ev.GetRemainingSeconds("defuse"), 0.001f, "full limit before armed");
            ev.Evaluate(0f);
            Assert.AreEqual(10f, ev.GetRemainingSeconds("defuse"), 0.001f);
            ev.Evaluate(4f);
            Assert.AreEqual(6f, ev.GetRemainingSeconds("defuse"), 0.001f);
            Assert.AreEqual(float.PositiveInfinity, ev.GetRemainingSeconds("unknown"), "no/absent limit ⇒ infinite");
        }

        [Test]
        public void Reset_RearmsTimer()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .AddObjective("defuse").WithTimeLimit(10f).CompleteWhen(Flag("defused")).Build());
            var ctx = new QuestContext();
            var ev = new QuestEvaluator(quest, ctx);

            ev.Evaluate(0f);
            ev.Evaluate(10f);
            Assert.AreEqual(QuestState.Failed, ev.GetObjectiveState("defuse"));

            ev.Reset();
            ev.Evaluate(10f);   // re-armed at now=10 ⇒ new deadline 20
            Assert.AreEqual(QuestState.Active, ev.GetObjectiveState("defuse"), "reset re-arms the timer fresh");
            ev.Evaluate(20f);
            Assert.AreEqual(QuestState.Failed, ev.GetObjectiveState("defuse"), "new deadline at t=20");
        }
    }
}
