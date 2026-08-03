using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// <see cref="GraphValidator"/>'s SubGraph rule consults <see cref="GraphValidatorExtensionRegistry"/>
    /// generically — it must fire when ANY registered extension flags the target, and stay silent when
    /// zero extensions are registered (the "graphgameflow not installed" case).
    /// </summary>
    public class GraphValidatorSubGraphExtensionTests
    {
        private sealed class FakeExtension : IGraphValidatorExtension
        {
            public BaseGraph FlaggedTarget;
            public string Message;
            public string CheckSubGraphTarget(BaseGraph targetGraph)
                => targetGraph == FlaggedTarget ? Message : null;
        }

        private readonly List<Object> _tracked = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var ext in new List<IGraphValidatorExtension>(GraphValidatorExtensionRegistry.Extensions))
                GraphValidatorExtensionRegistry.Unregister(ext);
            foreach (var o in _tracked) if (o != null) Object.DestroyImmediate(o);
            _tracked.Clear();
        }

        private T Track<T>(T o) where T : Object { _tracked.Add(o); return o; }

        private static bool HasWarning(GraphValidationReport r, string contains)
            => r.Issues.Any(i => i.Severity == GraphIssueSeverity.Warning && i.Message.Contains(contains));

        private BaseGraph BuildGraphWithSubGraph(BaseGraph target)
        {
            var g = Track(ScriptableObject.CreateInstance<BaseGraph>());
            g.AddNode(new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId });
            g.AddNode(new SubGraphNodeData { Id = "sub", NodeType = SubGraphNodeData.NodeTypeId, TargetGraph = target });
            g.AddNode(new EndNodeData { Id = "e", NodeType = EndNodeData.NodeTypeId });
            g.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "sub", PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "sub", ToNodeId = "e", PortName = "out" });
            g.EntryNodeId = "s";
            return g;
        }

        [Test]
        public void NoExtensionsRegistered_NoWarning()
        {
            var target = Track(ScriptableObject.CreateInstance<BaseGraph>());
            var g = BuildGraphWithSubGraph(target);

            var report = GraphValidator.Validate(g);

            Assert.IsFalse(HasWarning(report, "chapter"), "with zero registered extensions, the rule is inert.");
        }

        [Test]
        public void RegisteredExtensionFlagsTarget_ProducesWarning()
        {
            var target = Track(ScriptableObject.CreateInstance<BaseGraph>());
            var g = BuildGraphWithSubGraph(target);
            GraphValidatorExtensionRegistry.Register(new FakeExtension
            {
                FlaggedTarget = target,
                Message = "chapter-root crossing detected"
            });

            var report = GraphValidator.Validate(g);

            Assert.IsTrue(HasWarning(report, "chapter-root crossing detected"),
                "a registered extension's non-empty result becomes a Warning issue, verbatim.");
        }

        [Test]
        public void RegisteredExtensionDoesNotFlagTarget_NoWarning()
        {
            var target = Track(ScriptableObject.CreateInstance<BaseGraph>());
            var otherGraph = Track(ScriptableObject.CreateInstance<BaseGraph>());
            var g = BuildGraphWithSubGraph(target);
            GraphValidatorExtensionRegistry.Register(new FakeExtension { FlaggedTarget = otherGraph, Message = "should not appear" });

            var report = GraphValidator.Validate(g);

            Assert.IsFalse(HasWarning(report, "should not appear"));
        }
    }
}
