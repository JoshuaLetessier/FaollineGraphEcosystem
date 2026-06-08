using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// FR-012 — history/step-back: returning to an awaiting node via GoBack re-arms its wait (the runner
    /// re-enters WaitingForSignal), and the re-armed wait resolves again on a matching signal. Re-arming
    /// is automatic (re-entry re-detects AwaitSignalName); no signal data is captured in the snapshot.
    /// Graph: start → wait(await "go") → done → end.
    /// </summary>
    public class SignalHistoryTests
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
        public void GoBack_IntoAwaitingNode_ReArmsTheWait()
        {
            _runner.Start(_graph, _ctx, _registry);
            _runner.Proceed();                 // start → wait (holds)
            _runner.RaiseSignal("go");         // wait → done (NodeReady)
            Assert.AreEqual("done", _runner.CurrentNode.Id);

            _runner.GoBack();                  // back into the awaiting node

            Assert.AreEqual(RunnerState.WaitingForSignal, _runner.State);
            Assert.AreEqual("wait", _runner.CurrentNode.Id);
        }

        [Test]
        public void ReArmedWait_ResolvesAgain_OnMatchingSignal()
        {
            _runner.Start(_graph, _ctx, _registry);
            _runner.Proceed();                 // holds at wait
            _runner.RaiseSignal("go");         // → done
            _runner.GoBack();                  // re-arm wait
            Assert.AreEqual(RunnerState.WaitingForSignal, _runner.State);

            _runner.RaiseSignal("go");         // resolves again

            Assert.AreEqual(RunnerState.NodeReady, _runner.State);
            Assert.AreEqual("done", _runner.CurrentNode.Id);
        }
    }
}
