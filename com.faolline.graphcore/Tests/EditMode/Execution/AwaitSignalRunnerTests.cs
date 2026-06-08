using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// US2 — await/resume: a node with a non-empty AwaitSignalName holds execution on entry; a matching
    /// BaseRunner.RaiseSignal resumes via normal edge selection; a non-matching name keeps waiting; manual
    /// Proceed/ChooseById are inert while waiting; delivery happens even when nothing is waiting.
    /// Graph: start → wait(await "go") → done → end.
    /// </summary>
    public class AwaitSignalRunnerTests
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
            var wait  = new StatementNodeData { Id = "wait",  NodeType = StatementNodeData.NodeTypeId, AwaitSignalName = "go" };
            var done  = new StatementNodeData { Id = "done",  NodeType = StatementNodeData.NodeTypeId };
            var end   = new EndNodeData       { Id = "end",   NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };

            _graph.AddNode(start);
            _graph.AddNode(wait);
            _graph.AddNode(done);
            _graph.AddNode(end);
            _graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "start", ToNodeId = "wait" });
            _graph.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "wait",  ToNodeId = "done" });
            _graph.AddEdge(new BaseEdgeData { Id = "e3", FromNodeId = "done",  ToNodeId = "end"  });
            _graph.EntryNodeId = "start";
            _so.Add(_graph);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var so in _so) UnityEngine.Object.DestroyImmediate(so);
            _so.Clear();
        }

        [Test]
        public void EnteringAwaitNode_Holds_AndFiresOnWaitingForSignal_NotCompleted()
        {
            BaseNodeData waited = null;
            string waitedName = null;
            _runner.Start(_graph, _ctx, _registry);             // at start (NodeReady)

            bool completedFired = false;
            _runner.OnNodeCompleted += _ => completedFired = true;
            _runner.OnWaitingForSignal += (n, s) => { waited = n; waitedName = s; };

            _runner.Proceed();                                   // start → wait, holds

            Assert.AreEqual(RunnerState.WaitingForSignal, _runner.State);
            Assert.AreEqual("wait", _runner.CurrentNode.Id);
            Assert.AreEqual("wait", waited?.Id);
            Assert.AreEqual("go", waitedName);
            Assert.IsFalse(completedFired, "An awaiting node must not raise OnNodeCompleted.");
        }

        [Test]
        public void RaiseSignal_Matching_Resumes_ToNextNode()
        {
            _runner.Start(_graph, _ctx, _registry);
            _runner.Proceed();                                   // holds at wait
            Assert.AreEqual(RunnerState.WaitingForSignal, _runner.State);

            _runner.RaiseSignal("go");                           // resume: wait → done

            Assert.AreEqual(RunnerState.NodeReady, _runner.State);
            Assert.AreEqual("done", _runner.CurrentNode.Id);
        }

        [Test]
        public void RaiseSignal_Matching_ResumeThenProceed_Ends()
        {
            EndReason? ended = null;
            _runner.OnEnded += r => ended = r;
            _runner.Start(_graph, _ctx, _registry);
            _runner.Proceed();                                   // holds at wait
            _runner.RaiseSignal("go");                           // → done (NodeReady)
            _runner.Proceed();                                   // done → end (NodeReady)
            _runner.Proceed();                                   // end → Ended

            Assert.AreEqual(RunnerState.Ended, _runner.State);
            Assert.AreEqual(EndReason.Completed, ended);
        }

        [Test]
        public void RaiseSignal_NonMatching_KeepsWaiting()
        {
            _runner.Start(_graph, _ctx, _registry);
            _runner.Proceed();                                   // holds at wait

            _runner.RaiseSignal("nope");

            Assert.AreEqual(RunnerState.WaitingForSignal, _runner.State);
            Assert.AreEqual("wait", _runner.CurrentNode.Id);
        }

        [Test]
        public void ProceedAndChooseById_WhileWaiting_AreNoOps()
        {
            _runner.Start(_graph, _ctx, _registry);
            _runner.Proceed();                                   // holds at wait

            _runner.Proceed();
            _runner.ChooseById("e2");

            Assert.AreEqual(RunnerState.WaitingForSignal, _runner.State);
            Assert.AreEqual("wait", _runner.CurrentNode.Id);
        }

        [Test]
        public void RaiseSignal_DeliversToSubscribers_EvenWhenNothingWaiting()
        {
            int hits = 0;
            _ctx.OnSignal("go", _ => hits++);
            _runner.Start(_graph, _ctx, _registry);              // at start, not waiting

            _runner.RaiseSignal("go");                           // delivers; start has no await ⇒ no resume

            Assert.AreEqual(1, hits);
            Assert.AreEqual(RunnerState.NodeReady, _runner.State);
            Assert.AreEqual("start", _runner.CurrentNode.Id);
        }
    }
}
