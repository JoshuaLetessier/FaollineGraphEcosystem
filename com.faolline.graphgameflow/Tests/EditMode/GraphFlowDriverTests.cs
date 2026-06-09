using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Faolline.GraphCore;
using Faolline.GraphGameFlow;

namespace Faolline.GraphGameFlow.Tests
{
    /// <summary>
    /// US1 — the driver boots the Linear runner, pumps the frame tick, re-exposes lifecycle events, and
    /// supports auto/manual advance. Verified in EditMode via the driver's public methods (no PlayMode).
    /// </summary>
    public class GraphFlowDriverTests
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
        }

        private BaseGraph NewGraph(string entry)
        {
            var g = ScriptableObject.CreateInstance<BaseGraph>();
            g.EntryNodeId = entry;
            _so.Add(g);
            return g;
        }

        private static StartNodeData Start(string id) => new StartNodeData { Id = id, NodeType = StartNodeData.NodeTypeId };
        private static StatementNodeData St(string id) => new StatementNodeData { Id = id, NodeType = StatementNodeData.NodeTypeId };
        private static EndNodeData End(string id) => new EndNodeData { Id = id, NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };

        private GraphFlowDriver NewDriver(BaseGraph graph, bool autoAdvance)
        {
            var go = new GameObject("driver");
            _go.Add(go);
            var d = go.AddComponent<GraphFlowDriver>();
            d.Graph = graph;
            d.AutoAdvance = autoAdvance;
            d.SceneLoader = new StubSceneLoader();
            return d;
        }

        [Test]
        public void Boot_EntersStart_RaisesNodeEntered()
        {
            var g = NewGraph("start");
            g.AddNode(Start("start")); g.AddNode(End("end"));
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "end" });
            var d = NewDriver(g, autoAdvance: false);

            var entered = new List<string>();
            d.OnNodeEntered += n => entered.Add(n.Id);
            d.Boot();

            Assert.IsTrue(d.IsRunning);
            CollectionAssert.Contains(entered, "start");
        }

        [Test]
        public void Boot_NoGraph_WarnsAndStaysInert()
        {
            var d = NewDriver(null, autoAdvance: true);
            LogAssert.Expect(LogType.Warning, "[GraphGameFlow] GraphFlowDriver.Boot: no graph assigned; staying inert.");
            d.Boot();
            Assert.IsFalse(d.IsRunning);
        }

        [Test]
        public void Boot_NoValidStart_WarnsAndStaysInert()
        {
            var g = NewGraph("");   // empty EntryNodeId
            g.AddNode(St("a"));
            var d = NewDriver(g, autoAdvance: true);
            LogAssert.Expect(LogType.Warning, "[GraphGameFlow] GraphFlowDriver.Boot: graph has no valid start node (check EntryNodeId); staying inert.");
            d.Boot();
            Assert.IsFalse(d.IsRunning);
        }

        [Test]
        public void AutoAdvance_RunsChainToEnd()
        {
            var g = NewGraph("start");
            g.AddNode(Start("start")); g.AddNode(St("s1")); g.AddNode(End("end"));
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "s1" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "s1", ToNodeId = "end" });
            var d = NewDriver(g, autoAdvance: true);

            var entered = new List<string>();
            EndReason? ended = null;
            d.OnNodeEntered += n => entered.Add(n.Id);
            d.OnEnded += r => ended = r;
            d.Boot();

            Assert.AreEqual(new[] { "start", "s1", "end" }, entered.ToArray());
            Assert.AreEqual(EndReason.Completed, ended);
            Assert.IsFalse(d.IsRunning, "no longer running after the flow ends.");
        }

        [Test]
        public void ManualAdvance_OnlyAdvancesOnCall()
        {
            var g = NewGraph("start");
            g.AddNode(Start("start")); g.AddNode(St("s1")); g.AddNode(End("end"));
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "s1" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "s1", ToNodeId = "end" });
            var d = NewDriver(g, autoAdvance: false);

            var entered = new List<string>();
            d.OnNodeEntered += n => entered.Add(n.Id);
            d.Boot();
            Assert.AreEqual(new[] { "start" }, entered.ToArray(), "parks on start without auto-advance.");

            d.Advance();
            Assert.AreEqual(new[] { "start", "s1" }, entered.ToArray());
            d.Advance();
            CollectionAssert.Contains(entered, "end");
        }

        [Test]
        public void Tick_ForwardsTime_ResolvingAWaitNode()
        {
            var g = NewGraph("start");
            var wait = St("wait"); wait.WaitDuration = 1.0f;
            g.AddNode(Start("start")); g.AddNode(wait); g.AddNode(End("end"));
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "wait" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "wait", ToNodeId = "end" });
            var d = NewDriver(g, autoAdvance: true);

            bool ended = false;
            d.OnEnded += _ => ended = true;
            d.Boot();
            Assert.IsFalse(ended, "parked on the wait node.");

            d.Tick(0f);
            d.Tick(-5f);
            Assert.IsFalse(ended, "non-positive dt must not advance time.");

            d.Tick(0.4f);
            Assert.IsFalse(ended);
            d.Tick(0.7f);   // total 1.1 >= 1.0
            Assert.IsTrue(ended, "the wait node resolves once enough time is fed.");
        }

        [Test]
        public void RaiseSignal_WhenNotAwaiting_IsNoOp()
        {
            var g = NewGraph("start");
            g.AddNode(Start("start")); g.AddNode(End("end"));
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "end" });
            var d = NewDriver(g, autoAdvance: false);
            d.Boot();   // parked on start (NodeReady), not awaiting a signal

            Assert.DoesNotThrow(() => d.RaiseSignal("whatever"));
        }

        [Test]
        public void Advance_Tick_Signal_BeforeBoot_AreNoOps()
        {
            var g = NewGraph("start");
            g.AddNode(Start("start")); g.AddNode(End("end"));
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "end" });
            var d = NewDriver(g, autoAdvance: false);

            Assert.DoesNotThrow(() => { d.Advance(); d.Tick(1f); d.RaiseSignal("x"); });
            Assert.IsFalse(d.IsRunning);
        }

        [Test]
        public void Stop_DetachesFromRunner_NoFurtherCallbacks()
        {
            // OnDestroy calls Stop(); Stop() is the EditMode-callable seam (Unity does not invoke
            // OnDestroy in edit mode). The real-destroy path is covered by the PlayMode tests.
            var g = NewGraph("start");
            g.AddNode(Start("start")); g.AddNode(St("s1")); g.AddNode(End("end"));
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "s1" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "s1", ToNodeId = "end" });
            var d = NewDriver(g, autoAdvance: false);
            int entered = 0;
            d.OnNodeEntered += _ => entered++;
            d.Boot();
            int afterBoot = entered;
            var runner = d.Runner;

            d.Stop();

            Assert.IsFalse(d.IsRunning);
            Assert.DoesNotThrow(() => runner.Proceed());
            Assert.AreEqual(afterBoot, entered, "no driver callback fires after Stop().");
        }

        [Test]
        public void WaitingState_ReportsParkedAwaitSignal()
        {
            var g = NewGraph("start");
            var gate = St("gate"); gate.AwaitSignalName = "go";
            g.AddNode(Start("start")); g.AddNode(gate); g.AddNode(End("end"));
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "gate" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "gate", ToNodeId = "end" });
            var d = NewDriver(g, autoAdvance: true);

            Assert.IsFalse(d.IsWaitingForSignal, "not waiting before boot.");
            Assert.AreEqual("", d.CurrentAwaitSignal);

            d.Boot();   // start → gate (await "go"), parks
            Assert.IsTrue(d.IsWaitingForSignal);
            Assert.AreEqual("go", d.CurrentAwaitSignal);

            d.RaiseSignal("go");   // gate resumes → end → OnEnded
            Assert.IsFalse(d.IsWaitingForSignal, "not waiting after the flow ends.");
            Assert.AreEqual("", d.CurrentAwaitSignal);
        }

        [Test]
        public void OnWaitingForTime_FiresForTimedNode()
        {
            var g = NewGraph("start");
            var wait = St("wait"); wait.WaitDuration = 1.5f;
            g.AddNode(Start("start")); g.AddNode(wait); g.AddNode(End("end"));
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "wait" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "wait", ToNodeId = "end" });
            var d = NewDriver(g, autoAdvance: true);

            BaseNodeData timed = null; float secs = -1f;
            d.OnWaitingForTime += (n, s) => { timed = n; secs = s; };

            d.Boot();   // start → wait node (WaitingForTime) → OnWaitingForTime fires

            Assert.IsNotNull(timed, "OnWaitingForTime must fire when a timed node is entered.");
            Assert.AreEqual("wait", timed.Id);
            Assert.AreEqual(1.5f, secs, 0.001f);
        }
    }
}
