using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.Tests
{
    [TestFixture]
    public class DialogueBuilderParityTests
    {
        private sealed class NoOp : BaseAction { public override void Execute(BaseContext c) { } }
        private sealed class Gate : BaseCondition { public override bool Evaluate(BaseContext c) => true; }

        [Test]
        public void OnEnter_AddsActionsToNode()
        {
            var action = ScriptableObject.CreateInstance<NoOp>();
            try
            {
                var b = new DialogueGraphBuilder();
                var line = b.AddLine("npc", "Hello").OnEnter(action).AsEntry();
                var end = b.AddEnd();
                line.To(end);
                var g = b.Build();

                var node = g.Nodes[0];
                Assert.AreEqual(1, node.OnEnterActions.Count);
                Assert.AreSame(action, node.OnEnterActions[0]);

                Object.DestroyImmediate(g);
            }
            finally { Object.DestroyImmediate(action); }
        }

        [Test]
        public void OnExit_AddsActionsToNode()
        {
            var action = ScriptableObject.CreateInstance<NoOp>();
            try
            {
                var b = new DialogueGraphBuilder();
                var line = b.AddLine("npc", "Hello").OnExit(action).AsEntry();
                var end = b.AddEnd();
                line.To(end);
                var g = b.Build();

                var node = g.Nodes[0];
                Assert.AreEqual(1, node.OnExitActions.Count);
                Assert.AreSame(action, node.OnExitActions[0]);

                Object.DestroyImmediate(g);
            }
            finally { Object.DestroyImmediate(action); }
        }

        [Test]
        public void Checkpoint_SetsFlag()
        {
            var b = new DialogueGraphBuilder();
            var line = b.AddLine("npc", "Hello").Checkpoint().AsEntry();
            var end = b.AddEnd();
            line.To(end);
            var g = b.Build();

            Assert.IsTrue(g.Nodes[0].IsCheckpoint);
            Object.DestroyImmediate(g);
        }

        [Test]
        public void Await_SetsSignalName()
        {
            var b = new DialogueGraphBuilder();
            var line = b.AddLine("npc", "Wait...").Await("cutscene_done").AsEntry();
            var end = b.AddEnd();
            line.To(end);
            var g = b.Build();

            Assert.AreEqual("cutscene_done", g.Nodes[0].AwaitSignalName);
            Object.DestroyImmediate(g);
        }

        [Test]
        public void Wait_SetsDuration()
        {
            var b = new DialogueGraphBuilder();
            var line = b.AddLine("npc", "...").Wait(3f).AsEntry();
            var end = b.AddEnd();
            line.To(end);
            var g = b.Build();

            Assert.AreEqual(3f, g.Nodes[0].WaitDuration, 0.001f);
            Object.DestroyImmediate(g);
        }

        [Test]
        public void ResumeWhen_AddsConditions()
        {
            var gate = ScriptableObject.CreateInstance<Gate>();
            try
            {
                var b = new DialogueGraphBuilder();
                var line = b.AddLine("npc", "Wait...").Await("go").ResumeWhen(gate).AsEntry();
                var end = b.AddEnd();
                line.To(end);
                var g = b.Build();

                Assert.AreEqual(1, g.Nodes[0].ResumeConditions.Count);
                Assert.AreSame(gate, g.Nodes[0].ResumeConditions[0]);

                Object.DestroyImmediate(g);
            }
            finally { Object.DestroyImmediate(gate); }
        }

        [Test]
        public void FullChain_AllMethodsChainable()
        {
            var action = ScriptableObject.CreateInstance<NoOp>();
            var gate = ScriptableObject.CreateInstance<Gate>();
            try
            {
                var b = new DialogueGraphBuilder();
                var line = b.AddLine("npc", "Complex")
                    .OnEnter(action)
                    .OnExit(action)
                    .Checkpoint()
                    .Await("signal")
                    .ResumeWhen(gate)
                    .When(gate)
                    .AsEntry();
                var end = b.AddEnd();
                line.To(end);
                var g = b.Build();

                var node = g.Nodes[0];
                Assert.AreEqual(1, node.OnEnterActions.Count);
                Assert.AreEqual(1, node.OnExitActions.Count);
                Assert.IsTrue(node.IsCheckpoint);
                Assert.AreEqual("signal", node.AwaitSignalName);
                Assert.AreEqual(1, node.ResumeConditions.Count);
                Assert.AreEqual(1, node.EntryConditions.Count);

                Object.DestroyImmediate(g);
            }
            finally { Object.DestroyImmediate(action); Object.DestroyImmediate(gate); }
        }
    }
}
