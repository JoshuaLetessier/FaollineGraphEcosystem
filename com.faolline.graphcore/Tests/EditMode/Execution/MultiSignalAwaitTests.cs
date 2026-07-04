using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// Multi-signal await (#12 / Option A): a node can wait for several signals as a logical OR — it resumes
    /// on the FIRST awaited signal that passes ResumeConditions. Graph: start → room(await A|B) → done → end.
    /// </summary>
    public class MultiSignalAwaitTests
    {
        private BaseGraph            _graph;
        private BaseContext          _ctx;
        private NodeExecutorRegistry _registry;
        private BaseRunner           _runner;
        private StatementNodeData    _room;
        private readonly List<UnityEngine.Object> _so = new List<UnityEngine.Object>();

        private SignalName Sig(string name)
        {
            var s = ScriptableObject.CreateInstance<SignalName>(); s.name = name; _so.Add(s);
            return s;
        }

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<BaseGraph>(); _so.Add(_graph);
            _ctx = new BaseContext();
            _registry = new NodeExecutorRegistry();
            _runner = new BaseRunner();

            var start = new StartNodeData     { Id = "start", NodeType = StartNodeData.NodeTypeId };
            _room     = new StatementNodeData { Id = "room",  NodeType = StatementNodeData.NodeTypeId };
            var done  = new StatementNodeData { Id = "done",  NodeType = StatementNodeData.NodeTypeId };
            var end   = new EndNodeData       { Id = "end",   NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };

            _graph.AddNode(start); _graph.AddNode(_room); _graph.AddNode(done); _graph.AddNode(end);
            _graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "start", ToNodeId = "room" });
            _graph.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "room",  ToNodeId = "done" });
            _graph.AddEdge(new BaseEdgeData { Id = "e3", FromNodeId = "done",  ToNodeId = "end"  });
            _graph.EntryNodeId = "start";
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _so) UnityEngine.Object.DestroyImmediate(o);
            _so.Clear();
        }

        private void ParkAwaitingAOrB()
        {
            _room.AwaitSignals.Add(Sig("A"));
            _room.AwaitSignals.Add(Sig("B"));
            _runner.Start(_graph, _ctx, _registry);
            _runner.Proceed();                       // start → room, parks (await A|B)
            Assert.AreEqual(RunnerState.WaitingForSignal, _runner.State);
            Assert.AreEqual("room", _runner.CurrentNode.Id);
        }

        [Test]
        public void AwaitSignalNames_UnionsPrimaryAndExtras_Deduped()
        {
            _room.AwaitSignalName = "A";               // primary (raw)
            _room.AwaitSignals.Add(Sig("B"));
            _room.AwaitSignals.Add(Sig("A"));          // duplicate of primary → collapsed
            CollectionAssert.AreEqual(new[] { "A", "B" }, _room.AwaitSignalNames.ToArray());
        }

        [Test]
        public void AwaitSignalNames_IncludesStringExtras_Deduped()
        {
            _room.AwaitSignalName = "A";               // primary (raw)
            _room.AwaitSignals.Add(Sig("B"));          // asset extra
            _room.AwaitSignalNamesExtra.Add("C");      // string extra (code-first)
            _room.AwaitSignalNamesExtra.Add("A");      // duplicate of primary → collapsed
            CollectionAssert.AreEqual(new[] { "A", "B", "C" }, _room.AwaitSignalNames.ToArray());
        }

        [Test]
        public void ResumesOnStringExtraSignal()
        {
            _room.AwaitSignalName = "A";
            _room.AwaitSignalNamesExtra.Add("B");
            _runner.Start(_graph, _ctx, _registry);
            _runner.Proceed();                       // start → room, parks (await A|B)
            Assert.AreEqual(RunnerState.WaitingForSignal, _runner.State);

            _runner.RaiseSignal("B");                // the string extra resumes, same as an asset extra
            Assert.AreEqual(RunnerState.NodeReady, _runner.State);
            Assert.AreEqual("done", _runner.CurrentNode.Id);
        }

        [Test]
        public void ResumesOnFirstSignal()
        {
            ParkAwaitingAOrB();
            _runner.RaiseSignal("A");
            Assert.AreEqual(RunnerState.NodeReady, _runner.State);
            Assert.AreEqual("done", _runner.CurrentNode.Id);
        }

        [Test]
        public void ResumesOnOtherSignal()
        {
            ParkAwaitingAOrB();
            _runner.RaiseSignal("B");                  // the OTHER awaited signal also resumes
            Assert.AreEqual(RunnerState.NodeReady, _runner.State);
            Assert.AreEqual("done", _runner.CurrentNode.Id);
        }

        [Test]
        public void NonAwaitedSignal_KeepsWaiting()
        {
            ParkAwaitingAOrB();
            _runner.RaiseSignal("C");
            Assert.AreEqual(RunnerState.WaitingForSignal, _runner.State);
            Assert.AreEqual("room", _runner.CurrentNode.Id);
        }

        [Test]
        public void ResumeConditions_GateEveryAwaitedSignal()
        {
            var gate = ScriptableObject.CreateInstance<Gate>(); gate.Open = false; _so.Add(gate);
            _room.ResumeConditions.Add(gate);
            ParkAwaitingAOrB();

            _runner.RaiseSignal("A");                  // gate false → ignored
            Assert.AreEqual(RunnerState.WaitingForSignal, _runner.State);

            gate.Open = true;
            _runner.RaiseSignal("B");                  // now any awaited signal resumes
            Assert.AreEqual("done", _runner.CurrentNode.Id);
        }

        [Test]
        public void ContextRaise_AlsoResumes_ForAnyAwaitedName()
        {
            ParkAwaitingAOrB();
            _ctx.RaiseSignal("B");                     // raised directly on the context (not via runner)
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
