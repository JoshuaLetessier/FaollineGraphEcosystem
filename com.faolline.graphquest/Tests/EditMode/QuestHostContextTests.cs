using System;
using NUnit.Framework;
using Faolline.GraphCore;

namespace Faolline.GraphQuest.Tests
{
    /// <summary>US5 — the evaluator runs against any BaseContext (a host's), and the runtime has no host coupling.</summary>
    public sealed class QuestHostContextTests : QuestTestBase
    {
        [Test]
        public void Evaluator_RunsAgainst_AnExternalBaseContext()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .AddObjective("a").CompleteWhen(Flag("a_done")).Build());

            // A plain BaseContext owned/mutated by external (host) code — not a QuestContext.
            var hostContext = new BaseContext();
            var ev = new QuestEvaluator(quest, hostContext);

            ev.Evaluate();
            Assert.AreEqual(QuestState.Active, ev.GetObjectiveState("a"));

            hostContext.Set<bool>("a_done", true);
            ev.Evaluate();
            Assert.AreEqual(QuestState.Completed, ev.GetObjectiveState("a"),
                "quests track a host-owned context's changes");
        }

        [Test]
        public void RuntimeAssembly_HasNoGameflowOrGraphsaveReference()
        {
            var referenced = typeof(QuestEvaluator).Assembly.GetReferencedAssemblies();
            foreach (var asm in referenced)
            {
                StringAssert.DoesNotStartWith("com.faolline.graphgameflow", asm.Name,
                    "graphquest runtime must not reference gameflow");
                StringAssert.DoesNotStartWith("com.faolline.graphsave", asm.Name,
                    "graphquest runtime must not reference graphsave");
            }
        }
    }
}
