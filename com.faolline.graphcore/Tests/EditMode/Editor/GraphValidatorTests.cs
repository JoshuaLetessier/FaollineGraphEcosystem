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

        [TearDown]
        public void TearDown()
        {
            if (_graph != null) Object.DestroyImmediate(_graph);
        }

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
    }
}
