using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphCore.Tests
{
    [TestFixture]
    public class GraphValidatorTests
    {
        private BaseGraph _graph;
        private readonly System.Collections.Generic.List<Object> _tracked = new System.Collections.Generic.List<Object>();

        [TearDown]
        public void TearDown()
        {
            if (_graph != null) Object.DestroyImmediate(_graph);
            foreach (var o in _tracked) if (o != null) Object.DestroyImmediate(o);
            _tracked.Clear();
        }

        private T Track<T>(T o) where T : Object { _tracked.Add(o); return o; }

        private BaseGraph NewGraph() => _graph = ScriptableObject.CreateInstance<BaseGraph>();

        private static StartNodeData Start(string id) => new StartNodeData { Id = id, NodeType = StartNodeData.NodeTypeId };
        private static EndNodeData End(string id) => new EndNodeData { Id = id, NodeType = EndNodeData.NodeTypeId };
        private static ChoiceNodeData Choice(string id) => new ChoiceNodeData { Id = id, NodeType = ChoiceNodeData.NodeTypeId };
        private static BaseEdgeData Edge(string from, string to, string port = "out")
            => new BaseEdgeData { Id = from + "->" + to, FromNodeId = from, ToNodeId = to, PortName = port };

        private static bool HasError(GraphValidationReport r, string contains)
            => r.Issues.Any(i => i.Severity == GraphIssueSeverity.Error && i.Message.Contains(contains));
        private static bool HasWarning(GraphValidationReport r, string contains)
            => r.Issues.Any(i => i.Severity == GraphIssueSeverity.Warning && i.Message.Contains(contains));

        [Test]
        public void ValidGraph_HasNoErrors()
        {
            var g = NewGraph();
            g.AddNode(Start("s")); g.AddNode(End("e"));
            g.EntryNodeId = "s";
            g.AddEdge(Edge("s", "e"));

            var report = GraphValidator.Validate(g);
            Assert.IsFalse(report.HasErrors, "A Start→End graph must be error-free.");
        }

        [Test]
        public void NoStart_IsError()
        {
            var g = NewGraph();
            g.AddNode(End("e"));
            Assert.IsTrue(HasError(GraphValidator.Validate(g), "No Start node"));
        }

        [Test]
        public void MultipleStarts_IsError()
        {
            var g = NewGraph();
            g.AddNode(Start("s1")); g.AddNode(Start("s2")); g.AddNode(End("e"));
            g.EntryNodeId = "s1";
            g.AddEdge(Edge("s1", "e")); g.AddEdge(Edge("s2", "e"));
            Assert.IsTrue(HasError(GraphValidator.Validate(g), "Start nodes"));
        }

        [Test]
        public void EntryNodeIdMismatch_IsError()
        {
            var g = NewGraph();
            g.AddNode(Start("s")); g.AddNode(End("e"));
            g.EntryNodeId = "ghost";
            g.AddEdge(Edge("s", "e"));
            Assert.IsTrue(HasError(GraphValidator.Validate(g), "matches no node"));
        }

        [Test]
        public void DanglingEdge_IsError()
        {
            var g = NewGraph();
            g.AddNode(Start("s")); g.AddNode(End("e"));
            g.EntryNodeId = "s";
            g.AddEdge(Edge("s", "ghost"));
            Assert.IsTrue(HasError(GraphValidator.Validate(g), "non-existent node"));
        }

        [Test]
        public void IsolatedNode_IsWarning()
        {
            var g = NewGraph();
            g.AddNode(Start("s")); g.AddNode(End("e")); g.AddNode(End("lonely"));
            g.EntryNodeId = "s";
            g.AddEdge(Edge("s", "e"));
            Assert.IsTrue(HasWarning(GraphValidator.Validate(g), "Isolated node"));
        }

        [Test]
        public void IsolatedGraphLink_IsNotWarning()
        {
            // GraphLink is a non-executing annotation — being unconnected is normal, so it must NOT warn.
            var g = NewGraph();
            g.AddNode(Start("s")); g.AddNode(End("e"));
            g.AddNode(new GraphLinkNodeData { Id = "note", NodeType = GraphLinkNodeData.NodeTypeId });
            g.EntryNodeId = "s";
            g.AddEdge(Edge("s", "e"));
            Assert.IsFalse(HasWarning(GraphValidator.Validate(g), "Isolated node"),
                "a disconnected GraphLink annotation must not raise the isolated-node warning");
        }

        [Test]
        public void ChoiceWithoutOptions_IsError()
        {
            var g = NewGraph();
            g.AddNode(Start("s")); g.AddNode(Choice("c")); g.AddNode(End("e"));
            g.EntryNodeId = "s";
            g.AddEdge(Edge("s", "c"));
            Assert.IsTrue(HasError(GraphValidator.Validate(g), "has no options"));
        }

        [Test]
        public void ChoiceOptionWithoutEdge_IsError()
        {
            var g = NewGraph();
            var c = Choice("c");
            c.Choices.Add(new BaseChoice { Id = "a", Title = "A" });
            c.Choices.Add(new BaseChoice { Id = "b", Title = "B" });
            g.AddNode(Start("s")); g.AddNode(c); g.AddNode(End("e"));
            g.EntryNodeId = "s";
            g.AddEdge(Edge("s", "c"));
            g.AddEdge(Edge("c", "e", "a")); // option "a" wired, "b" left dangling

            var report = GraphValidator.Validate(g);
            Assert.IsTrue(report.Issues.Any(i => i.Severity == GraphIssueSeverity.Error && i.Message.Contains("no outgoing edge")),
                "An option without an outgoing edge must be an error.");
        }

        [Test]
        public void ChoiceWithAllOptionsWired_HasNoOptionEdgeError()
        {
            var g = NewGraph();
            var c = Choice("c");
            c.Choices.Add(new BaseChoice { Id = "a", Title = "A" });
            g.AddNode(Start("s")); g.AddNode(c); g.AddNode(End("e"));
            g.EntryNodeId = "s";
            g.AddEdge(Edge("s", "c"));
            g.AddEdge(Edge("c", "e", "a"));

            var report = GraphValidator.Validate(g);
            Assert.IsFalse(report.Issues.Any(i => i.Message.Contains("no outgoing edge")));
        }

        [Test]
        public void NullGraph_IsError()
        {
            Assert.IsTrue(HasError(GraphValidator.Validate(null), "null"));
        }

        // ── Sub-graph signal isolation (#5) ───────────────────────────────────

        // A target graph whose single statement node awaits <awaitSignal>, optionally raising <raiseSignal>
        // on enter (pass null to raise nothing → the await is external).
        private BaseGraph TargetAwaiting(string awaitSignal, string raiseSignal = null)
        {
            var g = Track(ScriptableObject.CreateInstance<BaseGraph>());
            var node = new StatementNodeData
            {
                Id = "await", NodeType = StatementNodeData.NodeTypeId, AwaitSignalName = awaitSignal
            };
            if (!string.IsNullOrEmpty(raiseSignal))
            {
                var sig = Track(ScriptableObject.CreateInstance<SignalName>()); sig.name = raiseSignal;
                var raise = Track(ScriptableObject.CreateInstance<RaiseSignalAction>()); raise.Signal = sig;
                node.OnEnterActions.Add(raise);
            }
            g.AddNode(node);
            g.EntryNodeId = "await";
            return g;
        }

        private BaseGraph HostWithSubGraph(BaseGraph target, bool inherit, bool scope)
        {
            var g = NewGraph();
            g.AddNode(Start("s"));
            g.AddNode(new SubGraphNodeData
            {
                Id = "sub", NodeType = SubGraphNodeData.NodeTypeId,
                TargetGraph = target, InheritParentContext = inherit, OpensScope = scope
            });
            g.EntryNodeId = "s";
            g.AddEdge(Edge("s", "sub"));
            return g;
        }

        [Test]
        public void FreshContextSubgraph_AwaitingExternalSignal_IsWarning()
        {
            var host = HostWithSubGraph(TargetAwaiting("ext"), inherit: false, scope: false);
            Assert.IsTrue(HasWarning(GraphValidator.Validate(host), "can never cross into the fresh context"));
        }

        [Test]
        public void InheritingSubgraph_AwaitingExternalSignal_NoWarning()
        {
            var host = HostWithSubGraph(TargetAwaiting("ext"), inherit: true, scope: false);
            Assert.IsFalse(HasWarning(GraphValidator.Validate(host), "fresh context"),
                "inheriting the parent context lets the signal reach the sub-graph");
        }

        [Test]
        public void ScopedSubgraph_AwaitingExternalSignal_NoWarning()
        {
            var host = HostWithSubGraph(TargetAwaiting("ext"), inherit: false, scope: true);
            Assert.IsFalse(HasWarning(GraphValidator.Validate(host), "fresh context"),
                "a scoped sub-graph reads through to parent values/signals");
        }

        [Test]
        public void FreshContextSubgraph_AwaitingSelfRaisedSignal_NoWarning()
        {
            // The sub-graph raises the very signal it awaits, so a fresh context is self-sufficient.
            var host = HostWithSubGraph(TargetAwaiting("loop", raiseSignal: "loop"), inherit: false, scope: false);
            Assert.IsFalse(HasWarning(GraphValidator.Validate(host), "fresh context"),
                "a self-contained signal loop needs no parent context");
        }

        // ── Unconditioned-edge shadowing (#8) ─────────────────────────────────

        private static StatementNodeData St(string id) => new StatementNodeData { Id = id, NodeType = StatementNodeData.NodeTypeId };
        private BaseEdgeData Gated(string from, string to, string port)
        {
            var e = Edge(from, to, port);
            e.Condition = Track(ScriptableObject.CreateInstance<AlwaysTrueCondition>());
            return e;
        }

        [Test]
        public void UnconditionedEdgeBeforeConditioned_IsWarning()
        {
            var g = NewGraph();
            g.AddNode(Start("s")); g.AddNode(St("r")); g.AddNode(End("a")); g.AddNode(End("b"));
            g.EntryNodeId = "s";
            g.AddEdge(Edge("s", "r"));
            g.AddEdge(Edge("r", "a", "1"));          // unconditioned, FIRST → shadows "b"
            g.AddEdge(Gated("r", "b", "2"));
            Assert.IsTrue(HasWarning(GraphValidator.Validate(g), "unreachable"));
        }

        [Test]
        public void UnconditionedEdgeLast_NoShadowWarning()
        {
            var g = NewGraph();
            g.AddNode(Start("s")); g.AddNode(St("r")); g.AddNode(End("a")); g.AddNode(End("b"));
            g.EntryNodeId = "s";
            g.AddEdge(Edge("s", "r"));
            g.AddEdge(Gated("r", "a", "1"));
            g.AddEdge(Edge("r", "b", "2"));          // unconditioned, LAST → valid default/else branch
            Assert.IsFalse(HasWarning(GraphValidator.Validate(g), "unreachable"),
                "an unconditioned edge placed last is the valid default branch");
        }

        [Test]
        public void ChoiceNode_UnconditionedEdges_NoShadowWarning()
        {
            // Choice edges route by port (ChooseById), so edge order does not shadow anything.
            var g = NewGraph();
            var c = Choice("c");
            c.Choices.Add(new BaseChoice { Id = "a" });
            c.Choices.Add(new BaseChoice { Id = "b" });
            g.AddNode(Start("s")); g.AddNode(c); g.AddNode(End("ea")); g.AddNode(End("eb"));
            g.EntryNodeId = "s";
            g.AddEdge(Edge("s", "c"));
            g.AddEdge(Edge("c", "ea", "a"));
            g.AddEdge(Edge("c", "eb", "b"));
            Assert.IsFalse(HasWarning(GraphValidator.Validate(g), "unreachable"),
                "choice edges route by port id, not condition order");
        }
    }
}
