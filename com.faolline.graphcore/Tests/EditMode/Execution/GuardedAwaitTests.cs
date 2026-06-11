using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// Slice 8 — guarded await: a parked await node may carry ResumeConditions; a matching RaiseSignal resumes
    /// only if all pass, otherwise the raise is ignored and the node stays parked (re-arm). Empty conditions ⇒
    /// current behavior. Graph: start → room(await "exit") → done → end.
    /// </summary>
    public class GuardedAwaitTests
    {
        private BaseGraph            _graph;
        private BaseContext          _ctx;
        private NodeExecutorRegistry _registry;
        private BaseRunner           _runner;
        private StatementNodeData    _room;
        private readonly List<UnityEngine.Object> _so = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            _graph    = ScriptableObject.CreateInstance<BaseGraph>();
            _ctx      = new BaseContext();
            _registry = new NodeExecutorRegistry();
            _runner   = new BaseRunner();

            var start = new StartNodeData     { Id = "start", NodeType = StartNodeData.NodeTypeId };
            _room     = new StatementNodeData { Id = "room",  NodeType = StatementNodeData.NodeTypeId, AwaitSignalName = "exit" };
            var done  = new StatementNodeData { Id = "done",  NodeType = StatementNodeData.NodeTypeId };
            var end   = new EndNodeData       { Id = "end",   NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };

            _graph.AddNode(start);
            _graph.AddNode(_room);
            _graph.AddNode(done);
            _graph.AddNode(end);
            _graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "start", ToNodeId = "room" });
            _graph.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "room",  ToNodeId = "done" });
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

        private Gate NewGate(bool open)
        {
            var g = ScriptableObject.CreateInstance<Gate>();
            g.Open = open;
            _so.Add(g);
            return g;
        }

        private void ParkAtRoom()
        {
            _runner.Start(_graph, _ctx, _registry);   // start (NodeReady)
            _runner.Proceed();                          // start → room, holds (await "exit")
            Assert.AreEqual(RunnerState.WaitingForSignal, _runner.State);
            Assert.AreEqual("room", _runner.CurrentNode.Id);
        }

        // ── US1: the gate ──────────────────────────────────────────────────────

        [Test]
        public void Resume_Blocked_WhenConditionFalse_StaysParked()
        {
            _room.ResumeConditions.Add(NewGate(false));
            ParkAtRoom();

            _runner.RaiseSignal("exit");   // gate false → ignored

            Assert.AreEqual(RunnerState.WaitingForSignal, _runner.State);
            Assert.AreEqual("room", _runner.CurrentNode.Id);
        }

        [Test]
        public void Resume_ReArms_WhenConditionBecomesTrue()
        {
            var gate = NewGate(false);
            _room.ResumeConditions.Add(gate);
            ParkAtRoom();

            _runner.RaiseSignal("exit");   // ignored (false)
            Assert.AreEqual(RunnerState.WaitingForSignal, _runner.State);

            gate.Open = true;
            _runner.RaiseSignal("exit");   // now resumes

            Assert.AreEqual(RunnerState.NodeReady, _runner.State);
            Assert.AreEqual("done", _runner.CurrentNode.Id);
        }

        [Test]
        public void Resume_RequiresAllConditions_AND()
        {
            _room.ResumeConditions.Add(NewGate(true));
            var second = NewGate(false);
            _room.ResumeConditions.Add(second);
            ParkAtRoom();

            _runner.RaiseSignal("exit");   // one false → blocked
            Assert.AreEqual(RunnerState.WaitingForSignal, _runner.State);

            second.Open = true;
            _runner.RaiseSignal("exit");   // both true → resume
            Assert.AreEqual("done", _runner.CurrentNode.Id);
        }

        [Test]
        public void Resume_NullConditionEntry_IsSkipped_NotAFailedGate()
        {
            _room.ResumeConditions.Add(null);            // skipped (warning), not a block
            _room.ResumeConditions.Add(NewGate(true));
            ParkAtRoom();

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[GraphCore\].*[Nn]ull"));
            _runner.RaiseSignal("exit");

            Assert.AreEqual(RunnerState.NodeReady, _runner.State);
            Assert.AreEqual("done", _runner.CurrentNode.Id);
        }

        // ── US2: back-compat ───────────────────────────────────────────────────

        [Test]
        public void Resume_NoConditions_ResumesImmediately()
        {
            ParkAtRoom();                                 // _room has no ResumeConditions
            _runner.RaiseSignal("exit");
            Assert.AreEqual(RunnerState.NodeReady, _runner.State);
            Assert.AreEqual("done", _runner.CurrentNode.Id);
        }

        [Test]
        public void Resume_WrongName_Ignored_RegardlessOfConditions()
        {
            _room.ResumeConditions.Add(NewGate(true));
            ParkAtRoom();
            _runner.RaiseSignal("nope");
            Assert.AreEqual(RunnerState.WaitingForSignal, _runner.State);
            Assert.AreEqual("room", _runner.CurrentNode.Id);
        }

        private class Gate : BaseCondition
        {
            public bool Open;
            public override bool Evaluate(BaseContext context) => Open;
        }
    }
}
