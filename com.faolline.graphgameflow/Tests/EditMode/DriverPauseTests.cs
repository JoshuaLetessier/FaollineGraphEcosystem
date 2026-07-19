using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphGameFlow;

namespace Faolline.GraphGameFlow.Tests
{
    /// <summary>
    /// <see cref="GraphFlowDriver.Paused"/> stops the flow's TIME (a parked timed wait holds) without
    /// blocking deliberate calls — a signal raised mid-pause still resumes a parked await. This is the
    /// loading-screen contract: <c>AsyncSceneLoader.PauseDriverWhileLoading</c> relies on exactly these two
    /// properties (verified over real frames in PlayMode's <c>AdditiveSceneFlowTests</c>).
    /// </summary>
    public class DriverPauseTests
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

        private static StartNodeData Start(string id) => new StartNodeData { Id = id, NodeType = StartNodeData.NodeTypeId };
        private static StatementNodeData St(string id) => new StatementNodeData { Id = id, NodeType = StatementNodeData.NodeTypeId };
        private static EndNodeData End(string id) => new EndNodeData { Id = id, NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };

        private GraphFlowDriver NewDriver(BaseGraph graph)
        {
            var go = new GameObject("driver");
            _go.Add(go);
            var d = go.AddComponent<GraphFlowDriver>();
            d.Graph = graph;
            d.AutoAdvance = true;
            d.SceneLoader = new StubSceneLoader();
            return d;
        }

        private BaseGraph WaitGraph(float seconds)
        {
            var g = ScriptableObject.CreateInstance<BaseGraph>(); _so.Add(g);
            g.EntryNodeId = "start";
            var wait = St("wait"); wait.WaitDuration = seconds;
            g.AddNode(Start("start")); g.AddNode(wait); g.AddNode(End("end"));
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "wait" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "wait", ToNodeId = "end" });
            return g;
        }

        [Test]
        public void Paused_GatesTick_TimedWaitHolds_ThenResumesOnUnpause()
        {
            var d = NewDriver(WaitGraph(1.0f));
            d.Boot();   // start → wait node (WaitingForTime)
            Assert.IsTrue(d.IsWaitingForTime);
            Assert.AreEqual(1.0f, d.WaitRemaining, 0.001f);

            d.Paused = true;
            d.Tick(0.4f);
            d.Tick(0.4f);
            Assert.AreEqual(1.0f, d.WaitRemaining, 0.001f, "time must not advance while paused.");
            Assert.IsTrue(d.IsWaitingForTime, "still parked on the timed node.");

            d.Paused = false;
            d.Tick(0.4f);
            Assert.AreEqual(0.6f, d.WaitRemaining, 0.001f, "time resumes where it left off after unpause.");

            d.Tick(0.7f);   // total 1.1 ≥ 1.0 → resolves → end
            Assert.IsFalse(d.IsWaitingForTime);
            Assert.IsFalse(d.IsRunning, "the flow completed after the wait resolved.");
        }

        [Test]
        public void Paused_DoesNotBlock_SignalResume()
        {
            // Only TIME stops: a completion signal raised mid-pause (the AsyncSceneLoader pattern) must
            // still resume a parked await and let the flow advance.
            var g = ScriptableObject.CreateInstance<BaseGraph>(); _so.Add(g);
            g.EntryNodeId = "start";
            var gate = St("gate"); gate.AwaitSignalName = "scene-ready";
            g.AddNode(Start("start")); g.AddNode(gate); g.AddNode(End("end"));
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "gate" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "gate", ToNodeId = "end" });
            var d = NewDriver(g);

            bool ended = false;
            d.OnEnded += _ => ended = true;

            d.Boot();
            Assert.IsTrue(d.IsWaitingForSignal && d.CurrentAwaitSignal == "scene-ready");

            d.Paused = true;
            d.RaiseSignal("scene-ready");

            Assert.IsTrue(ended, "the signal resumed the await and completed the flow while paused.");
        }
    }
}
