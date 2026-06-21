using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.Tests
{
    [TestFixture]
    public class DialoguePlayerSignalTests
    {
        private static DialogueGraph AwaitSignalGraph()
        {
            var g = ScriptableObject.CreateInstance<DialogueGraph>();
            var s = new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId };
            var l = new DialogueLineNodeData
            {
                Id = "l", NodeType = DialogueLineNodeData.NodeTypeId,
                SpeakerKey = "npc", AwaitSignalName = "proceed"
            };
            var e = new EndNodeData { Id = "e", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };
            g.AddNode(s); g.AddNode(l); g.AddNode(e);
            g.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "l", PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "l", ToNodeId = "e", PortName = "out" });
            g.EntryNodeId = "s";
            return g;
        }

        private static DialogueGraph WaitDurationGraph()
        {
            var g = ScriptableObject.CreateInstance<DialogueGraph>();
            var s = new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId };
            var l = new DialogueLineNodeData
            {
                Id = "l", NodeType = DialogueLineNodeData.NodeTypeId,
                SpeakerKey = "npc", WaitDuration = 2f
            };
            var e = new EndNodeData { Id = "e", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };
            g.AddNode(s); g.AddNode(l); g.AddNode(e);
            g.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "l", PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "l", ToNodeId = "e", PortName = "out" });
            g.EntryNodeId = "s";
            return g;
        }

        [Test]
        public void AwaitSignal_EmitsLineStep_BeforeHolding()
        {
            var graph = AwaitSignalGraph();
            try
            {
                var player = new DialoguePlayer(graph, titleFallback: true);
                LineStep line = null;
                player.OnLine += s => line = s;
                player.Start();

                Assert.IsNotNull(line, "OnLine should fire for a line with AwaitSignalName.");
                Assert.AreEqual("l", line.NodeId);
                Assert.IsTrue(player.IsWaitingForSignal);
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void AwaitSignal_RaisesOnWaitingForSignal_WithStep()
        {
            var graph = AwaitSignalGraph();
            try
            {
                var player = new DialoguePlayer(graph, titleFallback: true);
                LineStep waitStep = null;
                string waitSignal = null;
                player.OnWaitingForSignal += (step, sig) => { waitStep = step; waitSignal = sig; };
                player.Start();

                Assert.IsNotNull(waitStep);
                Assert.AreEqual("proceed", waitSignal);
                Assert.AreEqual("l", waitStep.NodeId);
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void RaiseSignal_ResumesAndEnds()
        {
            var graph = AwaitSignalGraph();
            try
            {
                var player = new DialoguePlayer(graph, titleFallback: true);
                EndStep end = null;
                player.OnEnded += s => end = s;
                player.Start();

                Assert.IsTrue(player.IsWaitingForSignal);
                player.RaiseSignal("proceed");

                Assert.IsNotNull(end, "Dialogue should end after signal resumes.");
                Assert.AreEqual(EndReason.Completed, end.EndReason);
                Assert.IsFalse(player.IsWaitingForSignal);
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void RaiseSignal_WrongName_StaysWaiting()
        {
            var graph = AwaitSignalGraph();
            try
            {
                var player = new DialoguePlayer(graph, titleFallback: true);
                player.Start();

                player.RaiseSignal("wrong_signal");
                Assert.IsTrue(player.IsWaitingForSignal);
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void WaitDuration_EmitsLineStep_BeforeHolding()
        {
            var graph = WaitDurationGraph();
            try
            {
                var player = new DialoguePlayer(graph, titleFallback: true);
                LineStep line = null;
                player.OnLine += s => line = s;
                player.Start();

                Assert.IsNotNull(line, "OnLine should fire for a line with WaitDuration.");
                Assert.AreEqual("l", line.NodeId);
                Assert.IsTrue(player.IsWaitingForTime);
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void WaitDuration_RaisesOnWaitingForTime_WithStep()
        {
            var graph = WaitDurationGraph();
            try
            {
                var player = new DialoguePlayer(graph, titleFallback: true);
                LineStep waitStep = null;
                float waitDuration = 0f;
                player.OnWaitingForTime += (step, dur) => { waitStep = step; waitDuration = dur; };
                player.Start();

                Assert.IsNotNull(waitStep);
                Assert.AreEqual(2f, waitDuration, 0.001f);
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void Tick_ResumesAfterDuration()
        {
            var graph = WaitDurationGraph();
            try
            {
                var player = new DialoguePlayer(graph, titleFallback: true);
                EndStep end = null;
                player.OnEnded += s => end = s;
                player.Start();

                Assert.IsTrue(player.IsWaitingForTime);
                player.Tick(1f);
                Assert.IsTrue(player.IsWaitingForTime, "Should still be waiting after 1s of 2s.");
                player.Tick(1.5f);

                Assert.IsNotNull(end, "Dialogue should end after enough time.");
                Assert.IsFalse(player.IsWaitingForTime);
            }
            finally { Object.DestroyImmediate(graph); }
        }
    }
}
