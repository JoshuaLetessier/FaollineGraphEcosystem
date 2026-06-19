using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphCore.Tests
{
    /// <summary>A `GraphLinkNodeData` renders as a distinct, labelled `GraphLinkNodeView` in ANY lib editor
    /// (via BaseGraphView), showing the target's kind + name, or "(missing target)" when unset.</summary>
    public class GraphLinkNodeViewTests
    {
        private sealed class FakeGraph : BaseGraph { }

        private sealed class StubGraphView : BaseGraphView
        {
            // GraphLink is intercepted by BaseGraphView before CreateNodeView, so these are never called here.
            protected override BaseNodeView CreateNodeView(BaseNodeData node) => null;
            protected override BaseEdgeView CreateEdgeView(BaseEdgeData edge) => null;
        }

        private static GraphLinkNodeView FindLink(BaseGraphView view)
        {
            GraphLinkNodeView found = null;
            view.nodes.ForEach(n => { if (n is GraphLinkNodeView gl) found = gl; });
            return found;
        }

        [Test]
        public void GraphLink_RendersAsGraphLinkNodeView_WithKindAndName()
        {
            var target = ScriptableObject.CreateInstance<FakeGraph>();
            target.name = "RelicQuest";
            var graph = ScriptableObject.CreateInstance<BaseGraph>();
            graph.AddNode(new GraphLinkNodeData { Id = "link", NodeType = GraphLinkNodeData.NodeTypeId, TargetGraph = target });
            var view = new StubGraphView();
            try
            {
                view.LoadGraph(graph);
                var link = FindLink(view);
                Assert.IsNotNull(link, "the GraphLink node renders as a GraphLinkNodeView.");
                Assert.IsTrue(link.title.Contains("RelicQuest"), "the label shows the target's name. Was: " + link.title);
                Assert.IsTrue(link.title.Contains("Fake"), "the label shows the target's kind (FakeGraph -> Fake). Was: " + link.title);
            }
            finally { Object.DestroyImmediate(target); Object.DestroyImmediate(graph); }
        }

        [Test]
        public void GraphLink_NullTarget_RendersMissingLabel_NoError()
        {
            var graph = ScriptableObject.CreateInstance<BaseGraph>();
            graph.AddNode(new GraphLinkNodeData { Id = "link", NodeType = GraphLinkNodeData.NodeTypeId, TargetGraph = null });
            var view = new StubGraphView();
            try
            {
                Assert.DoesNotThrow(() => view.LoadGraph(graph));
                var link = FindLink(view);
                Assert.IsNotNull(link);
                Assert.IsTrue(link.title.Contains("missing target"), "a null target renders a clear label. Was: " + link.title);
            }
            finally { Object.DestroyImmediate(graph); }
        }
    }
}
