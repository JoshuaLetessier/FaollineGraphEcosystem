using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// P5 — host-fed time wait: a node with WaitDuration holds (WaitingForTime); Tick(dt) advances it when
    /// the cumulative fed time reaches the duration. Pause = no/zero tick; signal wait takes precedence;
    /// step-back re-arms; no wait ⇒ unchanged. Graph: start → wait(2s) → done → end.
    /// </summary>
    public class TimeWaitRunnerTests
    {
        private BaseGraph            _graph;
        private BaseContext          _ctx;
        private NodeExecutorRegistry _registry;
        private BaseRunner           _runner;
        private readonly List<UnityEngine.Object> _so = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            _graph    = ScriptableObject.CreateInstance<BaseGraph>();
            _ctx      = new BaseContext();
            _registry = new NodeExecutorRegistry();
            _runner   = new BaseRunner();

            var start = new StartNodeData     { Id = "start", NodeType = StartNodeData.NodeTypeId };
            var wait  = new StatementNodeData { Id = "wait",  NodeType = StatementNodeData.NodeTypeId, WaitDuration = 2f };
            var done  = new StatementNodeData { Id = "done",  NodeType = StatementNodeData.NodeTypeId };
            var end   = new EndNodeData       { Id = "end",   NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };
            _graph.AddNode(start); _graph.AddNode(wait); _graph.AddNode(done); _graph.AddNode(end);
            _graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "start", ToNodeId = "wait" });
            _graph.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "wait",  ToNodeId = "done" });
            _graph.AddEdge(new BaseEdgeData { Id = "e3", FromNodeId = "done",  ToNodeId = "end"  });
            _graph.EntryNodeId = "start";
            _so.Add(_graph);
        }

        [TearDown]
        public void TearDown() { foreach (var s in _so) Object.DestroyImmediate(s); _so.Clear(); }

        [Test]
        public void EnteringTimedNode_Holds_FiresOnWaitingForTime_NotCompleted()
        {
            _runner.Start(_graph, _ctx, _registry);
            BaseNodeData waited = null; float secs = 0f; bool completed = false;
            _runner.OnNodeCompleted += _ => completed = true;
            _runner.OnWaitingForTime += (n, s) => { waited = n; secs = s; };

            _runner.Proceed();   // start → wait, holds

            Assert.AreEqual(RunnerState.WaitingForTime, _runner.State);
            Assert.AreEqual("wait", _runner.CurrentNode.Id);
            Assert.AreEqual("wait", waited?.Id);
            Assert.AreEqual(2f, secs);
            Assert.IsFalse(completed);
        }

        [Test]
        public void Tick_AccumulatesThenAdvances()
        {
            _runner.Start(_graph, _ctx, _registry); _runner.Proceed();
            _runner.Tick(1f);
            Assert.AreEqual(RunnerState.WaitingForTime, _runner.State);
            _runner.Tick(1f);
            Assert.AreEqual(RunnerState.NodeReady, _runner.State);
            Assert.AreEqual("done", _runner.CurrentNode.Id);
        }

        [Test]
        public void Tick_Overshoot_Advances()
        {
            _runner.Start(_graph, _ctx, _registry); _runner.Proceed();
            _runner.Tick(5f);
            Assert.AreEqual("done", _runner.CurrentNode.Id);
        }

        [Test]
        public void Tick_ZeroOrNegative_NeverAdvances()
        {
            _runner.Start(_graph, _ctx, _registry); _runner.Proceed();
            _runner.Tick(0f); _runner.Tick(0f); _runner.Tick(-3f);
            Assert.AreEqual(RunnerState.WaitingForTime, _runner.State);
        }

        [Test]
        public void Tick_WhenNotWaiting_IsNoOp()
        {
            _runner.Start(_graph, _ctx, _registry);          // at start, NodeReady
            _runner.Tick(10f);
            Assert.AreEqual(RunnerState.NodeReady, _runner.State);
            Assert.AreEqual("start", _runner.CurrentNode.Id);
        }

        [Test]
        public void ProceedAndChooseById_WhileTimeWaiting_AreNoOps()
        {
            _runner.Start(_graph, _ctx, _registry); _runner.Proceed();
            _runner.Proceed(); _runner.ChooseById("e2");
            Assert.AreEqual(RunnerState.WaitingForTime, _runner.State);
            Assert.AreEqual("wait", _runner.CurrentNode.Id);
        }

        [Test]
        public void AwaitSignal_TakesPrecedence_OverDuration()
        {
            var g = ScriptableObject.CreateInstance<BaseGraph>(); _so.Add(g);
            var s    = new StartNodeData     { Id = "s",    NodeType = StartNodeData.NodeTypeId };
            var both = new StatementNodeData { Id = "both", NodeType = StatementNodeData.NodeTypeId, WaitDuration = 2f, AwaitSignalName = "go" };
            var e    = new EndNodeData       { Id = "e",    NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };
            g.AddNode(s); g.AddNode(both); g.AddNode(e);
            g.AddEdge(new BaseEdgeData { Id = "a", FromNodeId = "s", ToNodeId = "both" });
            g.AddEdge(new BaseEdgeData { Id = "b", FromNodeId = "both", ToNodeId = "e" });
            g.EntryNodeId = "s";

            var r = new BaseRunner();
            r.Start(g, new BaseContext(), new NodeExecutorRegistry());
            r.Proceed();                                     // s → both
            Assert.AreEqual(RunnerState.WaitingForSignal, r.State, "Signal wait wins over duration.");
            r.Tick(10f);                                     // time ignored while signal-waiting
            Assert.AreEqual(RunnerState.WaitingForSignal, r.State);
            r.RaiseSignal("go");
            Assert.AreEqual("e", r.CurrentNode.Id);
        }

        [Test]
        public void NoWaitDuration_BehavesLikeBefore()
        {
            var g = ScriptableObject.CreateInstance<BaseGraph>(); _so.Add(g);
            var s = new StartNodeData     { Id = "s", NodeType = StartNodeData.NodeTypeId };
            var m = new StatementNodeData { Id = "m", NodeType = StatementNodeData.NodeTypeId };   // WaitDuration 0
            var e = new EndNodeData       { Id = "e", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };
            g.AddNode(s); g.AddNode(m); g.AddNode(e);
            g.AddEdge(new BaseEdgeData { Id = "a", FromNodeId = "s", ToNodeId = "m" });
            g.AddEdge(new BaseEdgeData { Id = "b", FromNodeId = "m", ToNodeId = "e" });
            g.EntryNodeId = "s";

            var r = new BaseRunner();
            EndReason? ended = null; r.OnEnded += x => ended = x;
            r.Start(g, new BaseContext(), new NodeExecutorRegistry());
            r.Proceed();                                     // s → m, NO hold
            Assert.AreEqual(RunnerState.NodeReady, r.State);
            r.Proceed(); r.Proceed();                        // m → e → Ended
            Assert.AreEqual(RunnerState.Ended, r.State);
            Assert.AreEqual(EndReason.Completed, ended);
        }

        [Test]
        public void StepBack_ReArmsCountdown()
        {
            _runner.Start(_graph, _ctx, _registry); _runner.Proceed();   // holds at wait
            _runner.Tick(2f);                                            // advances to done
            Assert.AreEqual("done", _runner.CurrentNode.Id);

            _runner.GoBack();                                            // back into the timed node
            Assert.AreEqual(RunnerState.WaitingForTime, _runner.State);
            Assert.AreEqual("wait", _runner.CurrentNode.Id);

            _runner.Tick(1f);                                            // re-armed: needs the full 2s again
            Assert.AreEqual(RunnerState.WaitingForTime, _runner.State);
            _runner.Tick(1f);
            Assert.AreEqual("done", _runner.CurrentNode.Id);
        }
    }
}
