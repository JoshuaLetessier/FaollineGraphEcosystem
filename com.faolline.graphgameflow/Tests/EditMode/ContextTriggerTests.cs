using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphGameFlow;

namespace Faolline.GraphGameFlow.Tests
{
    [TestFixture]
    public class ContextTriggerTests
    {
        private readonly List<Object> _so = new List<Object>();
        private readonly List<GameObject> _go = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var g in _go) if (g) Object.DestroyImmediate(g);
            _go.Clear();
            foreach (var o in _so) if (o) Object.DestroyImmediate(o);
            _so.Clear();
            // Clear static driver reference
            if (GraphFlowDriver.Active != null)
            {
                GraphFlowDriver.Active.Stop();
                Object.DestroyImmediate(GraphFlowDriver.Active.gameObject);
            }
        }

        private GraphFlowDriver CreateActiveDriver()
        {
            var g = ScriptableObject.CreateInstance<GameFlowGraph>();
            var start = new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId };
            var wait = new StatementNodeData { Id = "w", NodeType = StatementNodeData.NodeTypeId, AwaitSignalName = "hold" };
            var end = new EndNodeData { Id = "e", NodeType = EndNodeData.NodeTypeId };
            g.AddNode(start); g.AddNode(wait); g.AddNode(end);
            g.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "w", PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "w", ToNodeId = "e", PortName = "out" });
            g.EntryNodeId = "s";
            _so.Add(g);

            var go = new GameObject("Driver");
            _go.Add(go);
            var driver = go.AddComponent<GraphFlowDriver>();
            driver.Graph = g;
            driver.AutoAdvance = true;
            driver.BootOnStart = false;
            driver.PersistAcrossScenes = false;

            // Manually set Active (normally done by Awake with PersistAcrossScenes)
            typeof(GraphFlowDriver).GetProperty("Active")
                .SetValue(null, driver);

            var ctx = new GameFlowContext { SceneLoader = new StubSceneLoader() };
            driver.Boot(ctx, new NodeExecutorRegistry());
            return driver;
        }

        [Test]
        public void Fire_ExecutesActionsOnContext()
        {
            var driver = CreateActiveDriver();
            var testFlag = VariableDef.Bool("test_flag"); _so.Add(testFlag);
            var action = ScriptableObject.CreateInstance<SetBoolAction>();
            action.Variable = testFlag; action.Value = true;
            _so.Add(action);

            var triggerGo = new GameObject("Trigger");
            _go.Add(triggerGo);
            var trigger = triggerGo.AddComponent<ContextTrigger>();
            SetActions(trigger, action);

            trigger.Fire();

            Assert.IsTrue(driver.Context.TryGet<bool>(testFlag, out var v) && v);
        }

        [Test]
        public void Fire_RaisesSignal()
        {
            var driver = CreateActiveDriver();

            var triggerGo = new GameObject("Trigger");
            _go.Add(triggerGo);
            var trigger = triggerGo.AddComponent<ContextTrigger>();
            var sig = SetSignal(trigger, "my_signal");

            string received = null;
            driver.Context.OnSignal(sig, args => received = args.Name);   // asset → GUID (islands)

            trigger.Fire();

            Assert.AreEqual((string)sig, received);   // the raised GUID
        }

        [Test]
        public void FireOnce_PreventsDoubleFire()
        {
            var driver = CreateActiveDriver();
            var counter = VariableDef.Int("counter"); _so.Add(counter);
            var action = ScriptableObject.CreateInstance<AddIntAction>();
            action.Variable = counter; action.Value = 1;
            _so.Add(action);

            var triggerGo = new GameObject("Trigger");
            _go.Add(triggerGo);
            var trigger = triggerGo.AddComponent<ContextTrigger>();
            SetActions(trigger, action);
            SetFireOnce(trigger, true);

            trigger.Fire();
            trigger.Fire();
            trigger.Fire();

            Assert.IsTrue(driver.Context.TryGet<int>(counter, out var v));
            Assert.AreEqual(1, v, "Should only fire once.");
            Assert.IsTrue(trigger.HasFired);
        }

        [Test]
        public void ResetTrigger_AllowsRefire()
        {
            var driver = CreateActiveDriver();
            var counter = VariableDef.Int("counter"); _so.Add(counter);
            var action = ScriptableObject.CreateInstance<AddIntAction>();
            action.Variable = counter; action.Value = 1;
            _so.Add(action);

            var triggerGo = new GameObject("Trigger");
            _go.Add(triggerGo);
            var trigger = triggerGo.AddComponent<ContextTrigger>();
            SetActions(trigger, action);
            SetFireOnce(trigger, true);

            trigger.Fire();
            trigger.ResetTrigger();
            trigger.Fire();

            Assert.IsTrue(driver.Context.TryGet<int>(counter, out var v));
            Assert.AreEqual(2, v);
        }

        [Test]
        public void Fire_ActivatesAndDeactivatesGameObjects()
        {
            CreateActiveDriver();

            var toActivate = new GameObject("Puzzle") { };
            toActivate.SetActive(false);
            _go.Add(toActivate);

            var toDeactivate = new GameObject("Interactable") { };
            toDeactivate.SetActive(true);
            _go.Add(toDeactivate);

            var triggerGo = new GameObject("Trigger");
            _go.Add(triggerGo);
            var trigger = triggerGo.AddComponent<ContextTrigger>();
            SetActivate(trigger, toActivate);
            SetDeactivate(trigger, toDeactivate);

            trigger.Fire();

            Assert.IsTrue(toActivate.activeSelf, "Should be activated.");
            Assert.IsFalse(toDeactivate.activeSelf, "Should be deactivated.");
        }

        [Test]
        public void Fire_WithoutDriver_LogsWarning()
        {
            // Clear Active driver
            typeof(GraphFlowDriver).GetProperty("Active").SetValue(null, null);

            var triggerGo = new GameObject("Trigger");
            _go.Add(triggerGo);
            var trigger = triggerGo.AddComponent<ContextTrigger>();

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
                $"[GraphGameFlow] ContextTrigger '{triggerGo.name}': no active GraphFlowDriver; ignored.");
            trigger.Fire();
        }

        [Test]
        public void Guard_BlocksFireWhenFalse()
        {
            var driver = CreateActiveDriver();
            var guardedFlag = VariableDef.Bool("guarded_flag"); _so.Add(guardedFlag);
            var hasKey = VariableDef.Bool("has_key"); _so.Add(hasKey);
            var action = ScriptableObject.CreateInstance<SetBoolAction>();
            action.Variable = guardedFlag; action.Value = true;
            _so.Add(action);

            var guard = ScriptableObject.CreateInstance<BoolCondition>();
            guard.Variable = hasKey; guard.ExpectedValue = true;
            _so.Add(guard);

            var triggerGo = new GameObject("Trigger");
            _go.Add(triggerGo);
            var trigger = triggerGo.AddComponent<ContextTrigger>();
            SetActions(trigger, action);
            SetGuard(trigger, guard);
            SetFireOnce(trigger, false);

            trigger.Fire();
            Assert.IsFalse(driver.Context.TryGet<bool>(guardedFlag, out var v1) && v1,
                "Guard is false (has_key not set) — action should not execute.");

            driver.Context.Set<bool>(hasKey, true);
            trigger.Fire();
            Assert.IsTrue(driver.Context.TryGet<bool>(guardedFlag, out var v2) && v2,
                "Guard is now true — action should execute.");
        }

        [Test]
        public void Guard_DoesNotConsumeFireOnce_WhenBlocked()
        {
            var driver = CreateActiveDriver();
            var count = VariableDef.Int("count"); _so.Add(count);
            var ready = VariableDef.Bool("ready"); _so.Add(ready);
            var action = ScriptableObject.CreateInstance<AddIntAction>();
            action.Variable = count; action.Value = 1;
            _so.Add(action);

            var guard = ScriptableObject.CreateInstance<BoolCondition>();
            guard.Variable = ready; guard.ExpectedValue = true;
            _so.Add(guard);

            var triggerGo = new GameObject("Trigger");
            _go.Add(triggerGo);
            var trigger = triggerGo.AddComponent<ContextTrigger>();
            SetActions(trigger, action);
            SetGuard(trigger, guard);
            SetFireOnce(trigger, true);

            trigger.Fire();
            Assert.IsFalse(trigger.HasFired, "Guard blocked — HasFired should still be false.");

            driver.Context.Set<bool>(ready, true);
            trigger.Fire();
            Assert.IsTrue(trigger.HasFired);
            Assert.IsTrue(driver.Context.TryGet<int>(count, out var v) && v == 1);
        }

        // ── Reflection helpers to set serialized fields in tests ─────────────

        private static void SetActions(ContextTrigger trigger, params BaseAction[] actions)
        {
            var field = typeof(ContextTrigger).GetField("_actions",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(trigger, new List<BaseAction>(actions));
        }

        private static SignalDef SetSignal(ContextTrigger trigger, string signal)
        {
            var sig = SignalDef.Create(signal);   // asset signal keys on its GUID (islands)
            var field = typeof(ContextTrigger).GetField("_signal",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(trigger, sig);
            return sig;
        }

        private static void SetFireOnce(ContextTrigger trigger, bool value)
        {
            var field = typeof(ContextTrigger).GetField("_fireOnce",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(trigger, value);
        }

        private static void SetActivate(ContextTrigger trigger, params GameObject[] objects)
        {
            var field = typeof(ContextTrigger).GetField("_activate",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(trigger, new List<GameObject>(objects));
        }

        private static void SetDeactivate(ContextTrigger trigger, params GameObject[] objects)
        {
            var field = typeof(ContextTrigger).GetField("_deactivate",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(trigger, new List<GameObject>(objects));
        }

        private static void SetGuard(ContextTrigger trigger, BaseCondition guard)
        {
            var field = typeof(ContextTrigger).GetField("_guard",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(trigger, guard);
        }
    }
}
