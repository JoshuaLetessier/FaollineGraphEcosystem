using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphStandard;

namespace Faolline.GraphStandard.Tests
{
    [TestFixture]
    public class TimedTriggerTests
    {
        private sealed class Gate : BaseCondition
        {
            public bool Open;
            public override bool Evaluate(BaseContext c) => Open;
        }

        private sealed class Counter : BaseAction
        {
            public int Count;
            public override void Execute(BaseContext c) => Count++;
        }

        private BaseContext _ctx;
        [SetUp] public void SetUp() => _ctx = new BaseContext();

        [Test]
        public void FiresAfterDelay_WhenConditionHolds()
        {
            var gate = ScriptableObject.CreateInstance<Gate>();
            var action = ScriptableObject.CreateInstance<Counter>();
            gate.Open = true;
            try
            {
                var trigger = new TimedTrigger(_ctx);
                trigger.Add("t1", gate, 2f, action);

                trigger.Tick(1f);
                Assert.AreEqual(0, action.Count, "Should not fire before delay.");

                trigger.Tick(1.5f);
                Assert.AreEqual(1, action.Count, "Should fire once delay is reached.");
            }
            finally { Object.DestroyImmediate(gate); Object.DestroyImmediate(action); }
        }

        [Test]
        public void ResetsTimer_WhenConditionBecomesFalse()
        {
            var gate = ScriptableObject.CreateInstance<Gate>();
            var action = ScriptableObject.CreateInstance<Counter>();
            gate.Open = true;
            try
            {
                var trigger = new TimedTrigger(_ctx);
                trigger.Add("t1", gate, 3f, action);

                trigger.Tick(2f);
                Assert.AreEqual(0, action.Count);

                gate.Open = false;
                trigger.Tick(0.5f);

                gate.Open = true;
                trigger.Tick(2f);
                Assert.AreEqual(0, action.Count, "Timer should have reset — not enough time since re-arm.");

                trigger.Tick(1.5f);
                Assert.AreEqual(1, action.Count, "Should fire after full delay from re-arm.");
            }
            finally { Object.DestroyImmediate(gate); Object.DestroyImmediate(action); }
        }

        [Test]
        public void FiresOnlyOnce()
        {
            var gate = ScriptableObject.CreateInstance<Gate>();
            var action = ScriptableObject.CreateInstance<Counter>();
            gate.Open = true;
            try
            {
                var trigger = new TimedTrigger(_ctx);
                trigger.Add("t1", gate, 1f, action);

                trigger.Tick(2f);
                Assert.AreEqual(1, action.Count);

                trigger.Tick(5f);
                Assert.AreEqual(1, action.Count, "Should not fire again.");
            }
            finally { Object.DestroyImmediate(gate); Object.DestroyImmediate(action); }
        }

        [Test]
        public void MultipleTriggers_Independent()
        {
            var gate1 = ScriptableObject.CreateInstance<Gate>();
            var gate2 = ScriptableObject.CreateInstance<Gate>();
            var action1 = ScriptableObject.CreateInstance<Counter>();
            var action2 = ScriptableObject.CreateInstance<Counter>();
            gate1.Open = true;
            gate2.Open = true;
            try
            {
                var trigger = new TimedTrigger(_ctx);
                trigger.Add("fast", gate1, 1f, action1);
                trigger.Add("slow", gate2, 5f, action2);

                trigger.Tick(2f);
                Assert.AreEqual(1, action1.Count, "Fast should have fired.");
                Assert.AreEqual(0, action2.Count, "Slow should not have fired yet.");

                trigger.Tick(4f);
                Assert.AreEqual(1, action2.Count, "Slow should have fired now.");
            }
            finally
            {
                Object.DestroyImmediate(gate1); Object.DestroyImmediate(gate2);
                Object.DestroyImmediate(action1); Object.DestroyImmediate(action2);
            }
        }

        [Test]
        public void Remove_StopsTrigger()
        {
            var gate = ScriptableObject.CreateInstance<Gate>();
            var action = ScriptableObject.CreateInstance<Counter>();
            gate.Open = true;
            try
            {
                var trigger = new TimedTrigger(_ctx);
                trigger.Add("t1", gate, 2f, action);
                trigger.Tick(1f);

                trigger.Remove("t1");
                trigger.Tick(5f);
                Assert.AreEqual(0, action.Count, "Removed trigger should never fire.");
            }
            finally { Object.DestroyImmediate(gate); Object.DestroyImmediate(action); }
        }

        [Test]
        public void Reset_ReArmsAfterFired()
        {
            var gate = ScriptableObject.CreateInstance<Gate>();
            var action = ScriptableObject.CreateInstance<Counter>();
            gate.Open = true;
            try
            {
                var trigger = new TimedTrigger(_ctx);
                trigger.Add("t1", gate, 1f, action);

                trigger.Tick(2f);
                Assert.AreEqual(1, action.Count);

                trigger.Reset("t1");
                trigger.Tick(2f);
                Assert.AreEqual(2, action.Count, "Should fire again after reset.");
            }
            finally { Object.DestroyImmediate(gate); Object.DestroyImmediate(action); }
        }

        [Test]
        public void NullCondition_AlwaysArmed()
        {
            var action = ScriptableObject.CreateInstance<Counter>();
            try
            {
                var trigger = new TimedTrigger(_ctx);
                trigger.Add("t1", null, 1f, action);

                trigger.Tick(2f);
                Assert.AreEqual(1, action.Count, "Null condition should be treated as always true.");
            }
            finally { Object.DestroyImmediate(action); }
        }

        [Test]
        public void OnTriggered_EventFires()
        {
            var gate = ScriptableObject.CreateInstance<Gate>();
            var action = ScriptableObject.CreateInstance<Counter>();
            gate.Open = true;
            try
            {
                var trigger = new TimedTrigger(_ctx);
                trigger.Add("t1", gate, 1f, action);

                string firedId = null;
                trigger.OnTriggered += id => firedId = id;

                trigger.Tick(2f);
                Assert.AreEqual("t1", firedId);
            }
            finally { Object.DestroyImmediate(gate); Object.DestroyImmediate(action); }
        }
    }
}
