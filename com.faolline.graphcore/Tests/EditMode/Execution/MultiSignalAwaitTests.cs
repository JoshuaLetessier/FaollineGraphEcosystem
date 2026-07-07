using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// Multi-signal await (#12 / Option A): a node can wait for several signals as a logical OR — it resumes
    /// on the FIRST awaited signal that passes ResumeConditions. Asset signals key on their GUID (islands),
    /// so an await and its raise pair the SAME asset instance; the raw-string channel (AwaitSignalName /
    /// AwaitSignalNamesExtra + RaiseSignal(literal)) is separate and keys on literals.
    /// Graph: start → room(await A|B) → done → end.
    /// </summary>
    public class MultiSignalAwaitTests
    {
        private BaseGraph            _graph;
        private BaseContext          _ctx;
        private NodeExecutorRegistry _registry;
        private BaseRunner           _runner;
        private StatementNodeData    _room;
        private SignalDef           _sigA, _sigB;
        private readonly List<UnityEngine.Object> _so = new List<UnityEngine.Object>();

        private SignalDef Sig(string name)
        {
            var s = SignalDef.Create(name); _so.Add(s);
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
            _sigA = Sig("A");
            _sigB = Sig("B");
            _room.AwaitSignals.Add(_sigA);
            _room.AwaitSignals.Add(_sigB);
            _runner.Start(_graph, _ctx, _registry);
            _runner.Proceed();                       // start → room, parks (await A|B)
            Assert.AreEqual(RunnerState.WaitingForSignal, _runner.State);
            Assert.AreEqual("room", _runner.CurrentNode.Id);
        }

        [Test]
        public void AwaitSignalNames_UnionsPrimaryAndAssetExtras_Deduped()
        {
            var sigB = Sig("B");
            _room.AwaitSignalName = "raw_primary";     // primary (raw literal channel)
            _room.AwaitSignals.Add(sigB);
            _room.AwaitSignals.Add(sigB);              // same asset twice → collapsed on its GUID
            CollectionAssert.AreEqual(new[] { "raw_primary", sigB.Key }, _room.AwaitSignalNames.ToArray());
        }

        [Test]
        public void AwaitSignalNames_UnionsRawAndAsset_DedupWithinEachKind()
        {
            var sigB = Sig("B");
            _room.AwaitSignalName = "A";               // primary (raw literal)
            _room.AwaitSignals.Add(sigB);              // asset extra → GUID
            _room.AwaitSignalNamesExtra.Add("C");      // string extra
            _room.AwaitSignalNamesExtra.Add("A");      // duplicate of the raw primary → collapsed (both literals)
            // A raw "A" and an asset display-named "A" would NOT dedup — different channels, different keys.
            CollectionAssert.AreEqual(new[] { "A", sigB.Key, "C" }, _room.AwaitSignalNames.ToArray());
        }

        [Test]
        public void ResumesOnStringExtraSignal()
        {
            _room.AwaitSignalName = "A";               // all raw literal channel
            _room.AwaitSignalNamesExtra.Add("B");
            _runner.Start(_graph, _ctx, _registry);
            _runner.Proceed();                       // start → room, parks (await A|B)
            Assert.AreEqual(RunnerState.WaitingForSignal, _runner.State);

            _runner.RaiseSignal("B");                // the raw string extra resumes
            Assert.AreEqual(RunnerState.NodeReady, _runner.State);
            Assert.AreEqual("done", _runner.CurrentNode.Id);
        }

        [Test]
        public void ResumesOnFirstSignal()
        {
            ParkAwaitingAOrB();
            _runner.RaiseSignal(_sigA);
            Assert.AreEqual(RunnerState.NodeReady, _runner.State);
            Assert.AreEqual("done", _runner.CurrentNode.Id);
        }

        [Test]
        public void ResumesOnOtherSignal()
        {
            ParkAwaitingAOrB();
            _runner.RaiseSignal(_sigB);                // the OTHER awaited signal also resumes
            Assert.AreEqual(RunnerState.NodeReady, _runner.State);
            Assert.AreEqual("done", _runner.CurrentNode.Id);
        }

        [Test]
        public void NonAwaitedSignal_KeepsWaiting()
        {
            ParkAwaitingAOrB();
            _runner.RaiseSignal("C");                  // a raw literal is not one of the awaited GUIDs
            Assert.AreEqual(RunnerState.WaitingForSignal, _runner.State);
            Assert.AreEqual("room", _runner.CurrentNode.Id);
        }

        [Test]
        public void RawRaise_DoesNotWakeAssetAwait_Islands()
        {
            // Islands: a raw literal matching an asset's DISPLAY name does not wake an asset await.
            ParkAwaitingAOrB();
            _runner.RaiseSignal("A");                  // the display name of _sigA, but a raw literal
            Assert.AreEqual(RunnerState.WaitingForSignal, _runner.State,
                "a raw literal must not cross into the GUID-keyed asset channel");
        }

        [Test]
        public void ResumeConditions_GateEveryAwaitedSignal()
        {
            var gate = ScriptableObject.CreateInstance<Gate>(); gate.Open = false; _so.Add(gate);
            _room.ResumeConditions.Add(gate);
            ParkAwaitingAOrB();

            _runner.RaiseSignal(_sigA);                // gate false → ignored
            Assert.AreEqual(RunnerState.WaitingForSignal, _runner.State);

            gate.Open = true;
            _runner.RaiseSignal(_sigB);                // now any awaited signal resumes
            Assert.AreEqual("done", _runner.CurrentNode.Id);
        }

        [Test]
        public void ContextRaise_AlsoResumes_ForAnyAwaitedName()
        {
            ParkAwaitingAOrB();
            _ctx.RaiseSignal(_sigB);                   // raised directly on the context (not via runner)
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
