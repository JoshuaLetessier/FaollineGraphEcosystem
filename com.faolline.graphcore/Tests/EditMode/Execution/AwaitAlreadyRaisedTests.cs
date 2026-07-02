using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// Opt-in "resume if already raised": an await node whose <see cref="BaseNodeData.ResumeIfSignalAlreadyRaised"/>
    /// is true does NOT park when the awaited signal is already in the context's raised-signal history on entry —
    /// it resumes immediately (respecting any ResumeConditions gate). Default (false) keeps the live-only park.
    /// Graph: start → room(await "exit") → done → end.
    /// </summary>
    public class AwaitAlreadyRaisedTests
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

        [Test]
        public void OptIn_SignalAlreadyRaisedBeforeEntry_DoesNotPark()
        {
            _room.ResumeIfSignalAlreadyRaised = true;
            _ctx.RaiseSignal("exit");                    // raised BEFORE the runner reaches the node

            _runner.Start(_graph, _ctx, _registry);
            _runner.Proceed();                            // start → room: must NOT park

            // The await is already satisfied, so the node completes like a normal pass-through node
            // (NodeReady on "room" with OnNodeCompleted fired) instead of freezing in WaitingForSignal.
            Assert.AreEqual(RunnerState.NodeReady, _runner.State,
                "With the opt-in and the signal already in history, the node does not park.");
            Assert.AreEqual("room", _runner.CurrentNode.Id);

            _runner.Proceed();                            // and the flow continues normally
            Assert.AreEqual("done", _runner.CurrentNode.Id);
        }

        [Test]
        public void Default_SignalAlreadyRaisedBeforeEntry_StillParks()
        {
            // ResumeIfSignalAlreadyRaised defaults to false → live-only behaviour preserved (back-compat).
            _ctx.RaiseSignal("exit");

            _runner.Start(_graph, _ctx, _registry);
            _runner.Proceed();

            Assert.AreEqual(RunnerState.WaitingForSignal, _runner.State);
            Assert.AreEqual("room", _runner.CurrentNode.Id);
        }

        [Test]
        public void OptIn_SignalAlreadyRaisedButGateFalse_StaysParked()
        {
            _room.ResumeIfSignalAlreadyRaised = true;
            _room.ResumeConditions.Add(NewGate(false));   // gate blocks the immediate resume
            _ctx.RaiseSignal("exit");

            _runner.Start(_graph, _ctx, _registry);
            _runner.Proceed();

            Assert.AreEqual(RunnerState.WaitingForSignal, _runner.State,
                "The already-raised shortcut still honours the ResumeConditions gate.");
            Assert.AreEqual("room", _runner.CurrentNode.Id);
        }

        [Test]
        public void OptIn_SignalNotYetRaised_ParksThenResumesNormally()
        {
            _room.ResumeIfSignalAlreadyRaised = true;     // no signal raised yet

            _runner.Start(_graph, _ctx, _registry);
            _runner.Proceed();                            // parks (nothing in history)
            Assert.AreEqual(RunnerState.WaitingForSignal, _runner.State);

            _runner.RaiseSignal("exit");                  // normal live resume
            Assert.AreEqual(RunnerState.NodeReady, _runner.State);
            Assert.AreEqual("done", _runner.CurrentNode.Id);
        }

        private class Gate : BaseCondition
        {
            public bool Open;
            public override bool Evaluate(BaseContext context) => Open;
        }
    }
}
