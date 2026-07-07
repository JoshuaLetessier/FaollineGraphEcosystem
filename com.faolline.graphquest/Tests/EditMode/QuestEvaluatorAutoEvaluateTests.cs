using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphQuest;

namespace Faolline.GraphQuest.Tests
{
    [TestFixture]
    public class QuestEvaluatorAutoEvaluateTests
    {
        // Governed parameters key on a GUID (islands): a condition and the ctx.Set that satisfies it must use the
        // SAME ParameterName instance. Tracked and destroyed per-test.
        private readonly List<Object> _created = new List<Object>();
        private T Track<T>(T o) where T : Object { if (o != null) _created.Add(o); return o; }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private QuestGraph QuestCompletingOn(ParameterName flag)
        {
            var completeCond = ScriptableObject.CreateInstance<BoolCondition>();
            completeCond.Parameter = flag;
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
            var taskDone = Track(ParameterName.Bool("task_done"));
            var quest = QuestCompletingOn(taskDone);
            var ctx = new BaseContext();
            try
            {
                var eval = new QuestEvaluator(quest, ctx);
                eval.EnableAutoEvaluate();

                QuestState? reported = null;
                eval.OnObjectiveStateChanged += (id, state) => { if (id == "obj_a") reported = state; };

                ctx.Set<bool>(taskDone, true);

                Assert.AreEqual(QuestState.Completed, reported);
            }
            finally { Object.DestroyImmediate(quest); }
        }

        [Test]
        public void EnableAutoEvaluate_FiresOnCollectionChange()
        {
            // CollectionName/CollectionEntry.Key is a stable GUID (not a name-fallback string), so read
            // .Key back below instead of the (purely cosmetic) asset names.
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

                ctx.AddToCollection(itemsCol.Key, keyEntry.Key);

                Assert.AreEqual(QuestState.Completed, reported);
            }
            finally { Object.DestroyImmediate(quest); }
        }

        [Test]
        public void DisableAutoEvaluate_StopsAutoEvaluation()
        {
            var taskDone = Track(ParameterName.Bool("task_done"));
            var quest = QuestCompletingOn(taskDone);
            var ctx = new BaseContext();
            try
            {
                var eval = new QuestEvaluator(quest, ctx);
                eval.EnableAutoEvaluate();
                eval.DisableAutoEvaluate();

                int changeCount = 0;
                eval.OnObjectiveStateChanged += (_, __) => changeCount++;

                ctx.Set<bool>(taskDone, true);

                Assert.AreEqual(0, changeCount, "Should not auto-evaluate after disable.");
                Assert.IsFalse(eval.IsAutoEvaluateEnabled);
            }
            finally { Object.DestroyImmediate(quest); }
        }

        [Test]
        public void EnableAutoEvaluate_Twice_IsIdempotent()
        {
            var taskDone = Track(ParameterName.Bool("task_done"));
            var quest = QuestCompletingOn(taskDone);
            var ctx = new BaseContext();
            try
            {
                var eval = new QuestEvaluator(quest, ctx);
                eval.EnableAutoEvaluate();
                eval.EnableAutoEvaluate();

                int changeCount = 0;
                eval.OnObjectiveStateChanged += (_, __) => changeCount++;

                ctx.Set<bool>(taskDone, true);

                Assert.AreEqual(1, changeCount, "Should fire once, not twice.");
            }
            finally { Object.DestroyImmediate(quest); }
        }

        [Test]
        public void EnableAutoEvaluate_FiresOnSignalRaised()
        {
            var signal = SignalName.Create("boss_defeated");   // asset signal keys on its GUID (islands)
            var completeCond = ScriptableObject.CreateInstance<SignalRaisedCondition>();
            completeCond.Signal = signal;

            var quest = QuestBuilder.Create("signal_test")
                .AddObjective("obj_sig")
                    .Named("Beat the boss")
                    .CompleteWhen(completeCond)
                .Build();
            var ctx = new BaseContext();
            try
            {
                var eval = new QuestEvaluator(quest, ctx);
                eval.EnableAutoEvaluate();

                QuestState? reported = null;
                eval.OnObjectiveStateChanged += (id, state) => { if (id == "obj_sig") reported = state; };

                ctx.RaiseSignal(signal);

                Assert.AreEqual(QuestState.Completed, reported,
                    "A signal-only quest must auto-evaluate when a signal is raised (SignalRaisedCondition).");
            }
            finally { Object.DestroyImmediate(quest); }
        }

        [Test]
        public void DisableAutoEvaluate_StopsSignalAutoEvaluation()
        {
            var signal = SignalName.Create("ping");   // asset signal keys on its GUID (islands)
            var completeCond = ScriptableObject.CreateInstance<SignalRaisedCondition>();
            completeCond.Signal = signal;

            var quest = QuestBuilder.Create("signal_off_test")
                .AddObjective("obj_sig")
                    .CompleteWhen(completeCond)
                .Build();
            var ctx = new BaseContext();
            try
            {
                var eval = new QuestEvaluator(quest, ctx);
                eval.EnableAutoEvaluate();
                eval.DisableAutoEvaluate();

                int changeCount = 0;
                eval.OnObjectiveStateChanged += (_, __) => changeCount++;

                ctx.RaiseSignal(signal);

                Assert.AreEqual(0, changeCount, "Should not auto-evaluate on a signal after disable.");
            }
            finally { Object.DestroyImmediate(quest); }
        }

        [Test]
        public void AutoEvaluate_DoesNotTickTimers()
        {
            var timedDone = Track(ParameterName.Bool("timed_done"));
            var completeCond = ScriptableObject.CreateInstance<BoolCondition>();
            completeCond.Parameter = timedDone;
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

                ctx.Set<int>("unrelated", 42);   // raw-island: unrelated to the timed_done parameter

                var state = eval.GetObjectiveState("timed_obj");
                Assert.AreEqual(QuestState.Active, state,
                    "Auto-evaluate uses Evaluate() not Evaluate(now), so timers should not arm/expire.");
            }
            finally { Object.DestroyImmediate(quest); }
        }

        [Test]
        public void AutoEvaluate_ReEntrancyGuard_CoalescesIntoSingleReEvaluate()
        {
            var done = Track(ParameterName.Bool("done"));
            var rewarded = Track(ParameterName.Bool("rewarded"));
            var completeCond = ScriptableObject.CreateInstance<BoolCondition>();
            completeCond.Parameter = done;
            completeCond.ExpectedValue = true;

            var rewardAction = ScriptableObject.CreateInstance<SetBoolAction>();
            rewardAction.Parameter = rewarded;
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

                ctx.Set<bool>(done, true);

                Assert.AreEqual(QuestState.Completed, eval.GetObjectiveState("obj"));
                Assert.IsTrue(ctx.TryGet<bool>(rewarded, out var v) && v,
                    "Reward should have fired despite re-entrancy (reward sets a param during evaluate).");
            }
            finally { Object.DestroyImmediate(quest); }
        }
    }
}
