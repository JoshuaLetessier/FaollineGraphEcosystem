using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphTest.Tests
{
    /// <summary>
    /// FR-013 — exercises the P1 signal capability end-to-end in the sandbox: an awaiting node that holds
    /// and resumes on a host signal, broadcast to multiple subscribers, and a payload-reading condition
    /// that branches the resume.
    /// </summary>
    [TestFixture]
    public class SignalExerciseTests
    {
        private readonly List<Object> _so = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _so) Object.DestroyImmediate(o);
            _so.Clear();
        }

        [Test]
        public void AwaitNode_HoldsThenResumes_OnHostSignal()
        {
            var graph = ScriptableObject.CreateInstance<TestGraph>();
            _so.Add(graph);

            var start = new StartNodeData     { Id = "start", NodeType = StartNodeData.NodeTypeId };
            var wait  = new StatementNodeData { Id = "wait",  NodeType = StatementNodeData.NodeTypeId, AwaitSignalName = "doorOpened" };
            var end   = new EndNodeData       { Id = "end",   NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };
            graph.AddNode(start);
            graph.AddNode(wait);
            graph.AddNode(end);
            graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "start", ToNodeId = "wait" });
            graph.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "wait",  ToNodeId = "end"  });
            graph.EntryNodeId = "start";

            var runner = new BaseRunner();
            runner.Start(graph, new BaseContext(), new NodeExecutorRegistry());
            runner.Proceed();                               // start → wait (holds)

            Assert.AreEqual(RunnerState.WaitingForSignal, runner.State);

            runner.RaiseSignal("doorOpened");               // wait → end (NodeReady)
            Assert.AreEqual("end", runner.CurrentNode.Id);
            runner.Proceed();                               // end → Ended
            Assert.AreEqual(RunnerState.Ended, runner.State);
        }

        [Test]
        public void Signal_Broadcasts_ToMultipleSubscribers()
        {
            var ctx = new BaseContext();
            int a = 0, b = 0;
            ctx.OnSignal("itemCollected", _ => a++);
            ctx.OnSignal("itemCollected", _ => b++);

            ctx.RaiseSignal<string>("itemCollected", "key");

            Assert.AreEqual(1, a);
            Assert.AreEqual(1, b);
        }

        [Test]
        public void PayloadCondition_BranchesResume_OnSignalPayload()
        {
            var graph = ScriptableObject.CreateInstance<TestGraph>();
            _so.Add(graph);

            var cond = ScriptableObject.CreateInstance<TestSignalPayloadCondition>();
            cond.SignalName = "pick";
            cond.ExpectedPayload = "key";
            _so.Add(cond);

            var start = new StartNodeData     { Id = "start", NodeType = StartNodeData.NodeTypeId };
            var wait  = new StatementNodeData { Id = "wait",  NodeType = StatementNodeData.NodeTypeId, AwaitSignalName = "pick" };
            var keyN  = new StatementNodeData { Id = "key",   NodeType = StatementNodeData.NodeTypeId };
            var other = new StatementNodeData { Id = "other", NodeType = StatementNodeData.NodeTypeId };
            graph.AddNode(start);
            graph.AddNode(wait);
            graph.AddNode(keyN);
            graph.AddNode(other);
            graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "start", ToNodeId = "wait" });
            // Gated edge listed FIRST so SelectEdge prefers it when the payload matches.
            graph.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "wait", ToNodeId = "key",   Condition = cond });
            graph.AddEdge(new BaseEdgeData { Id = "e3", FromNodeId = "wait", ToNodeId = "other" });
            graph.EntryNodeId = "start";

            var runner = new BaseRunner();
            runner.Start(graph, new BaseContext(), new NodeExecutorRegistry());
            runner.Proceed();                               // holds at wait

            runner.RaiseSignal<string>("pick", "key");      // payload matches → gated edge wins

            Assert.AreEqual("key", runner.CurrentNode.Id);
        }
    }
}
