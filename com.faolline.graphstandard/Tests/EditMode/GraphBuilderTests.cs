using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphStandard;

namespace Faolline.GraphStandard.Tests
{
    /// <summary>US1 — the fluent code-first graph builder produces the exact intended structure and the
    /// built graph runs like a hand-assembled one.</summary>
    public class GraphBuilderTests
    {
        private sealed class NoOpAction : BaseAction { public override void Execute(BaseContext c) { } }
        private sealed class AlwaysCondition : BaseCondition { public override bool Evaluate(BaseContext c) => true; }
        private sealed class Gate : BaseCondition { public bool Open; public override bool Evaluate(BaseContext c) => Open; }

        private readonly List<Object> _so = new List<Object>();
        [TearDown] public void TearDown() { foreach (var o in _so) if (o) Object.DestroyImmediate(o); _so.Clear(); }
        private T Track<T>(T o) where T : Object { _so.Add(o); return o; }

        [Test]
        public void Build_ProducesExactStructure()
        {
            var action = Track(ScriptableObject.CreateInstance<NoOpAction>());
            var cond   = Track(ScriptableObject.CreateInstance<AlwaysCondition>());

            var b = new GraphBuilder<BaseGraph>();
            var start  = b.AddStart("Start").AsEntry();
            var doIt   = b.AddStatement("Do").OnEnter(action).When(cond).Await("go").Checkpoint();
            var wait   = b.AddStatement("Wait").Wait(2f);
            var choice = b.AddChoice("Pick");
            choice.Choice("Yes", cond); choice.Choice("No");
            var end    = b.AddEnd("End", EndReason.Completed);
            start.To(doIt); doIt.To(wait); wait.To(choice);
            b.Edge(choice, end, "Yes");
            var g = Track(b.Build());

            Assert.AreEqual(start.Node.Id, g.EntryNodeId, "entry is the AsEntry node.");
            Assert.AreEqual(5, g.Nodes.Count);
            Assert.IsTrue(g.Nodes.Any(n => n is StartNodeData) && g.Nodes.Any(n => n is EndNodeData));

            var doNode = g.Nodes.First(n => n.Title == "Do");
            Assert.AreEqual("go", doNode.AwaitSignalName);
            Assert.IsTrue(doNode.IsCheckpoint);
            Assert.Contains(action, doNode.OnEnterActions);
            Assert.Contains(cond, doNode.EntryConditions);

            Assert.AreEqual(2f, g.Nodes.First(n => n.Title == "Wait").WaitDuration, 0.001f);

            var pick = (ChoiceNodeData)g.Nodes.First(n => n.Title == "Pick");
            Assert.AreEqual(2, pick.Choices.Count);
            var yes = pick.Choices.First(ch => ch.Title == "Yes");
            Assert.IsTrue(g.Edges.Any(e => e.FromNodeId == pick.Id && e.PortName == yes.Id),
                "the 'Yes' choice edge routes by the choice's id.");
            Assert.AreEqual(4, g.Edges.Count);
        }

        [Test]
        public void Build_ReturnsRequestedGraphType()
        {
            var g = Track(new GraphBuilder<BaseGraph>().Build());
            Assert.IsInstanceOf<BaseGraph>(g);
        }

        [Test]
        public void BuiltGraph_RunsUnderRunner_LikeHandBuilt()
        {
            var b = new GraphBuilder<BaseGraph>();
            var start = b.AddStart().AsEntry();
            var stmt  = b.AddStatement("s");
            var end   = b.AddEnd();
            start.To(stmt); stmt.To(end);
            var g = Track(b.Build());

            var runner = new BaseRunner();
            EndReason? ended = null;
            runner.OnNodeCompleted += _ => runner.Proceed();
            runner.OnEnded += r => ended = r;
            runner.Start(g, new BaseContext(), new NodeExecutorRegistry());

            Assert.AreEqual(EndReason.Completed, ended);
        }

        [Test]
        public void ResumeWhen_AttachesGate_AndReArmsResume()
        {
            var gate = Track(ScriptableObject.CreateInstance<Gate>());   // starts closed
            var b = new GraphBuilder<BaseGraph>();
            var start = b.AddStart().AsEntry();
            var room  = b.AddStatement("room").Await("exit").ResumeWhen(gate);
            var end   = b.AddEnd();
            start.To(room); room.To(end);
            var g = Track(b.Build());

            Assert.Contains(gate, g.Nodes.First(n => n.Title == "room").ResumeConditions,
                "ResumeWhen appends to the node's ResumeConditions.");

            var runner = new BaseRunner();
            runner.Start(g, new BaseContext(), new NodeExecutorRegistry());
            runner.Proceed();                                   // start → room, parks (await "exit")
            Assert.AreEqual(RunnerState.WaitingForSignal, runner.State);

            runner.RaiseSignal("exit");                         // gate closed → ignored, stays parked
            Assert.AreEqual(RunnerState.WaitingForSignal, runner.State);
            Assert.AreEqual(room.Node.Id, runner.CurrentNode.Id);

            gate.Open = true;
            runner.RaiseSignal("exit");                         // gate open → resumes off room
            Assert.AreNotEqual(RunnerState.WaitingForSignal, runner.State);
            Assert.AreNotEqual(room.Node.Id, runner.CurrentNode?.Id);
        }

        [Test]
        public void Id_SetsStableNodeId_UsedByEdges()
        {
            var b = new GraphBuilder<BaseGraph>();
            var start = b.AddStart().AsEntry();
            var hub   = b.AddStatement("Hub").Id("room_hub");
            var end   = b.AddEnd();
            start.To(hub); hub.To(end);
            var g = Track(b.Build());

            Assert.AreEqual("room_hub", hub.Node.Id, "Id() overrides the auto-GUID.");
            Assert.IsTrue(g.Nodes.Any(n => n.Id == "room_hub"), "the stable id is addressable on the built graph.");
            Assert.IsTrue(g.Edges.Any(e => e.FromNodeId == "room_hub"), "edges wired after Id() use the stable id.");
            Assert.IsTrue(g.Edges.Any(e => e.ToNodeId == "room_hub"));
        }

        [Test]
        public void Edge_ToForeignNode_Throws()
        {
            var b = new GraphBuilder<BaseGraph>();
            var a = b.AddStatement("a");
            var foreign = new GraphBuilder<BaseGraph>().AddStatement("foreign");
            Assert.Throws<System.ArgumentException>(() => b.Edge(a, foreign));
        }

        [Test]
        public void Edge_WithCondition_SetsConditionOnEdge()
        {
            var cond = Track(ScriptableObject.CreateInstance<Gate>());
            var b = new GraphBuilder<BaseGraph>();
            var start = b.AddStart().AsEntry();
            var a = b.AddStatement("A");
            var end = b.AddEnd();
            start.To(a);
            b.Edge(a, end, "out", cond);
            var g = Track(b.Build());

            var edge = g.Edges.First(e => e.FromNodeId == a.Node.Id && e.ToNodeId == end.Node.Id);
            Assert.AreSame(cond, edge.Condition, "Edge condition should be set by the overload.");
        }

        [Test]
        public void To_WithCondition_SetsConditionOnEdge()
        {
            var cond = Track(ScriptableObject.CreateInstance<Gate>());
            var b = new GraphBuilder<BaseGraph>();
            var start = b.AddStart().AsEntry();
            var a = b.AddStatement("A");
            var end = b.AddEnd();
            start.To(a);
            a.To(end, "out", cond);
            var g = Track(b.Build());

            var edge = g.Edges.First(e => e.FromNodeId == a.Node.Id && e.ToNodeId == end.Node.Id);
            Assert.AreSame(cond, edge.Condition);
        }

        [Test]
        public void Edge_WithCondition_RunnerRespectsGate()
        {
            var gate = Track(ScriptableObject.CreateInstance<Gate>());
            var b = new GraphBuilder<BaseGraph>();
            var start = b.AddStart().AsEntry();
            var hub = b.AddStatement("Hub");
            var gated = b.AddStatement("Gated");
            var fallback = b.AddEnd("Fallback");
            start.To(hub);
            b.Edge(hub, gated, "out", gate);
            hub.To(fallback);
            var g = Track(b.Build());

            var runner = new BaseRunner();
            string reached = null;
            runner.OnNodeEntered += n => reached = n.Title;
            runner.OnNodeCompleted += n => runner.Proceed();
            runner.Start(g, new BaseContext(), new NodeExecutorRegistry());

            Assert.AreEqual("Fallback", reached,
                "Gate is closed, so the runner should skip the gated edge and take the fallback.");
        }
    }
}
