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
        public void Edge_ToForeignNode_Throws()
        {
            var b = new GraphBuilder<BaseGraph>();
            var a = b.AddStatement("a");
            var foreign = new GraphBuilder<BaseGraph>().AddStatement("foreign");
            Assert.Throws<System.ArgumentException>(() => b.Edge(a, foreign));
        }
    }
}
