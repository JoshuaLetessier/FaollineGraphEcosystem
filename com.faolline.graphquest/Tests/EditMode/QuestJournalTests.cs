using System.Linq;
using NUnit.Framework;

namespace Faolline.GraphQuest.Tests
{
    /// <summary>The journal data layer: objective/quest metadata + GetObjectives()/progress for a quest-log UI.</summary>
    public sealed class QuestJournalTests : QuestTestBase
    {
        [Test]
        public void Builder_SetsObjectiveAndQuestMetadata()
        {
            var quest = TrackGraph(QuestBuilder.Create("rescue")
                .Named("Rescue Aldric").Describe("Get out of the keep.")
                .AddObjective("find").Named("Find the clue").Describe("Search the desk.").CompleteWhen(Flag("found"))
                .Build());

            Assert.AreEqual("Rescue Aldric", quest.DisplayName);
            Assert.AreEqual("Get out of the keep.", quest.Description);

            var obj = quest.Nodes[0] as ObjectiveNodeData;
            Assert.IsNotNull(obj);
            Assert.AreEqual("Find the clue", obj.Title);
            Assert.AreEqual("Search the desk.", obj.Description);
        }

        [Test]
        public void QuestDisplayName_FallsBackToId_WhenUnset()
        {
            var quest = TrackGraph(QuestBuilder.Create("rescue")
                .AddObjective("find").CompleteWhen(Flag("found")).Build());
            Assert.AreEqual("rescue", quest.DisplayName);
        }

        [Test]
        public void GetObjectives_ReturnsViews_WithLabelFallback_AndCurrentState()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .AddObjective("a").Named("Do A").CompleteWhen(Flag("a"))
                .AddObjective("b").CompleteWhen(Flag("b"))   // no Named ⇒ label falls back to id
                .Build());
            var ctx = new QuestContext();
            ctx.Set<bool>("a", true);
            var ev = new QuestEvaluator(quest, ctx);
            ev.Evaluate();

            var views = ev.GetObjectives();
            Assert.AreEqual(2, views.Count);

            var a = views.First(v => v.Id == "a");
            Assert.AreEqual("Do A", a.DisplayName);
            Assert.AreEqual(QuestState.Completed, a.State);
            Assert.IsTrue(a.Required);

            var b = views.First(v => v.Id == "b");
            Assert.AreEqual("b", b.DisplayName, "no title ⇒ display name falls back to the id");
            Assert.AreEqual(QuestState.Active, b.State);
        }

        [Test]
        public void RequiredProgress_CountsOnlyRequiredCompleted()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .AddObjective("r1").CompleteWhen(Flag("r1"))
                .AddObjective("r2").CompleteWhen(Flag("r2"))
                .AddObjective("opt").Optional().CompleteWhen(Flag("opt"))
                .Build());
            var ctx = new QuestContext();
            var ev = new QuestEvaluator(quest, ctx);

            ev.Evaluate();
            Assert.AreEqual(2, ev.RequiredTotal, "optional objective is not counted in the denominator");
            Assert.AreEqual(0, ev.RequiredCompleted);

            ctx.Set<bool>("r1", true);
            ctx.Set<bool>("opt", true);   // optional completion must not move the required progress
            ev.Evaluate();
            Assert.AreEqual(1, ev.RequiredCompleted);

            ctx.Set<bool>("r2", true);
            ev.Evaluate();
            Assert.AreEqual(2, ev.RequiredCompleted);
            Assert.AreEqual(QuestState.Completed, ev.State);
        }
    }
}
