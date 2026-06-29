using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphQuest;

namespace Faolline.GraphQuest.Tests
{
    [TestFixture]
    public class QuestEvaluatorAutoEvaluateTests
    {
        private static QuestGraph SimpleQuest()
        {
            var completeCond = ScriptableObject.CreateInstance<BoolCondition>();
            completeCond.ParameterKey = "task_done";
            completeCond.ExpectedValue = true;

            return QuestBuilder.Create("auto_test")
                .Named("Auto Test")
                .AddObjective("obj_a")
                    .Named("Do the thing")
                    .CompleteWhen(completeCond)
                .Build();
        }

        [Test]
        public void EnableAutoEvaluate_FiresOnParameterChange()
        {
            var quest = SimpleQuest();
            var ctx = new BaseContext();
            try
            {
                var eval = new QuestEvaluator(quest, ctx);
                eval.EnableAutoEvaluate();

                QuestState? reported = null;
                eval.OnObjectiveStateChanged += (id, state) => { if (id == "obj_a") reported = state; };

                ctx.Set<bool>("task_done", true);

                Assert.AreEqual(QuestState.Completed, reported);
            }
            finally { Object.DestroyImmediate(quest); }
        }

        [Test]
        public void EnableAutoEvaluate_FiresOnCollectionChange()
        {
            var itemsCol = ScriptableObject.CreateInstance<CollectionName>(); itemsCol.name = "items";
            var keyEntry = ScriptableObject.CreateInstance<CollectionEntry>(); keyEntry.name = "key";
            var containsCond = ScriptableObject.CreateInstance<Faolline.GraphStandard.CollectionContainsCondition>();
            containsCond.Collection = itemsCol;
            containsCond.Entry = keyEntry;

            var quest = QuestBuilder.Create("coll_test")
                .AddObjective("obj_coll")
                    .CompleteWhen(containsCond)
                .Build();
            var ctx = new BaseContext();
            try
            {
                var eval = new QuestEvaluator(quest, ctx);
                eval.EnableAutoEvaluate();

                QuestState? reported = null;
                eval.OnObjectiveStateChanged += (id, state) => { if (id == "obj_coll") reported = state; };

                ctx.AddToCollection("items", "key");

                Assert.AreEqual(QuestState.Completed, reported);
            }
            finally { Object.DestroyImmediate(quest); }
        }

        [Test]
        public void DisableAutoEvaluate_StopsAutoEvaluation()
        {
            var quest = SimpleQuest();
            var ctx = new BaseContext();
            try
            {
                var eval = new QuestEvaluator(quest, ctx);
                eval.EnableAutoEvaluate();
                eval.DisableAutoEvaluate();

                int changeCount = 0;
                eval.OnObjectiveStateChanged += (_, __) => changeCount++;

                ctx.Set<bool>("task_done", true);

                Assert.AreEqual(0, changeCount, "Should not auto-evaluate after disable.");
                Assert.IsFalse(eval.IsAutoEvaluateEnabled);
            }
            finally { Object.DestroyImmediate(quest); }
        }

        [Test]
        public void EnableAutoEvaluate_Twice_IsIdempotent()
        {
            var quest = SimpleQuest();
            var ctx = new BaseContext();
            try
            {
                var eval = new QuestEvaluator(quest, ctx);
                eval.EnableAutoEvaluate();
                eval.EnableAutoEvaluate();

                int changeCount = 0;
                eval.OnObjectiveStateChanged += (_, __) => changeCount++;

                ctx.Set<bool>("task_done", true);

                Assert.AreEqual(1, changeCount, "Should fire once, not twice.");
            }
            finally { Object.DestroyImmediate(quest); }
        }

        [Test]
        public void AutoEvaluate_DoesNotTickTimers()
        {
            var completeCond = ScriptableObject.CreateInstance<BoolCondition>();
            completeCond.ParameterKey = "timed_done";
            completeCond.ExpectedValue = true;

            var quest = QuestBuilder.Create("timer_test")
                .AddObjective("timed_obj")
                    .Named("Timed")
                    .CompleteWhen(completeCond)
                    .WithTimeLimit(5f)
                .Build();
            var ctx = new BaseContext();
            try
            {
                var eval = new QuestEvaluator(quest, ctx);
                eval.EnableAutoEvaluate();

                ctx.Set<int>("unrelated", 42);

                var state = eval.GetObjectiveState("timed_obj");
                Assert.AreEqual(QuestState.Active, state,
                    "Auto-evaluate uses Evaluate() not Evaluate(now), so timers should not arm/expire.");
            }
            finally { Object.DestroyImmediate(quest); }
        }

        [Test]
        public void AutoEvaluate_ReEntrancyGuard_CoalescesIntoSingleReEvaluate()
        {
            var completeCond = ScriptableObject.CreateInstance<BoolCondition>();
            completeCond.ParameterKey = "done";
            completeCond.ExpectedValue = true;

            var rewardAction = ScriptableObject.CreateInstance<SetBoolAction>();
            rewardAction.ParameterKey = "rewarded";
            rewardAction.Value = true;

            var quest = QuestBuilder.Create("reentry_test")
                .AddObjective("obj")
                    .CompleteWhen(completeCond)
                    .RewardWith(rewardAction)
                .Build();
            var ctx = new BaseContext();
            try
            {
                var eval = new QuestEvaluator(quest, ctx);
                eval.EnableAutoEvaluate();

                ctx.Set<bool>("done", true);

                Assert.AreEqual(QuestState.Completed, eval.GetObjectiveState("obj"));
                Assert.IsTrue(ctx.TryGet<bool>("rewarded", out var v) && v,
                    "Reward should have fired despite re-entrancy (reward sets a param during evaluate).");
            }
            finally { Object.DestroyImmediate(quest); }
        }
    }
}
