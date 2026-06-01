using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// Tests for node groups: data layer (GraphGroupData / BaseGraph) and view behaviour
    /// (GroupSelection, collapse hiding nodes, delete keeping nodes, remove-from-group).
    /// </summary>
    [TestFixture]
    public class BaseGraphGroupTests
    {
        private class StubNodeData : BaseNodeData { }

        private class StubNodeView : BaseNodeView
        {
            public StubNodeView(BaseNodeData data) { Initialize(data); }
            protected override void OnBuildView() { }
        }

        private class TestableGraphView : BaseGraphView
        {
            protected override BaseNodeView CreateNodeView(BaseNodeData node) => new StubNodeView(node);
            protected override BaseEdgeView CreateEdgeView(BaseEdgeData edge) => null;

            public BaseNodeView AddNode(string id, Vector2 pos)
            {
                var node = new StubNodeData { Id = id, NodeType = "test" };
                AddNodeToCanvas(node, pos);
                foreach (var n in nodes)
                    if (n is BaseNodeView nv && nv.NodeData != null && nv.NodeData.Id == id)
                        return nv;
                return null;
            }
        }

        private BaseGraph _graph;
        private TestableGraphView _view;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<BaseGraph>();
            _view = new TestableGraphView();
            _view.LoadGraph(_graph);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        // ── Data layer ─────────────────────────────────────────────────────────────

        [Test]
        public void GraphGroupData_Defaults_AreSane()
        {
            var g = new GraphGroupData();
            Assert.AreEqual("Group", g.Title);
            Assert.IsFalse(g.IsCollapsed);
            Assert.IsNotNull(g.NodeIds);
            Assert.AreEqual(0, g.NodeIds.Count);
        }

        [Test]
        public void BaseGraph_AddGroup_RemoveGroup()
        {
            var g = new GraphGroupData { Id = "grp1" };
            _graph.AddGroup(g);
            Assert.AreEqual(1, _graph.Groups.Count);

            _graph.RemoveGroup(g);
            Assert.AreEqual(0, _graph.Groups.Count);
        }

        [Test]
        public void GraphGroupData_SerializesViaJsonUtility()
        {
            var g = new GraphGroupData { Id = "g", Title = "Act 1", IsCollapsed = true };
            g.NodeIds.Add("n1"); g.NodeIds.Add("n2");

            var json   = JsonUtility.ToJson(g);
            var loaded  = JsonUtility.FromJson<GraphGroupData>(json);

            Assert.AreEqual("Act 1", loaded.Title);
            Assert.IsTrue(loaded.IsCollapsed);
            Assert.AreEqual(2, loaded.NodeIds.Count);
        }

        // ── GroupSelection ─────────────────────────────────────────────────────────

        [Test]
        public void GroupSelection_CreatesGroup_WithSelectedNodeIds()
        {
            var a = _view.AddNode("a", new Vector2(0, 0));
            var b = _view.AddNode("b", new Vector2(100, 0));
            _view.AddToSelection(a);
            _view.AddToSelection(b);

            _view.GroupSelection(Vector2.zero);

            Assert.AreEqual(1, _graph.Groups.Count, "A group must be added to the graph.");
            var data = _graph.Groups[0];
            CollectionAssert.Contains(data.NodeIds, "a");
            CollectionAssert.Contains(data.NodeIds, "b");
        }

        [Test]
        public void GroupSelection_NoSelection_CreatesEmptyGroup()
        {
            _view.GroupSelection(new Vector2(10, 10));
            Assert.AreEqual(1, _graph.Groups.Count);
            Assert.AreEqual(0, _graph.Groups[0].NodeIds.Count);
        }

        // ── Collapse ───────────────────────────────────────────────────────────────

        [Test]
        public void ToggleCollapse_TogglesDataState_AndHidesNodeViews()
        {
            var a = _view.AddNode("a", Vector2.zero);
            _view.AddToSelection(a);
            _view.GroupSelection(Vector2.zero);
            var groupView = _view.GroupViewsForTest[0];

            Assert.IsTrue(_view.IsNodeViewVisibleForTest("a"), "Node visible before collapse.");

            groupView.ToggleCollapse();
            Assert.IsTrue(groupView.GroupData.IsCollapsed, "Data flagged collapsed.");
            Assert.IsFalse(_view.IsNodeViewVisibleForTest("a"), "Node hidden when collapsed.");

            groupView.ToggleCollapse();
            Assert.IsFalse(groupView.GroupData.IsCollapsed, "Data flagged expanded.");
            Assert.IsTrue(_view.IsNodeViewVisibleForTest("a"), "Node visible again when expanded.");
        }

        // ── Delete keeps nodes ───────────────────────────────────────────────────────

        [Test]
        public void DeletingGroup_KeepsContainedNodes()
        {
            var a = _view.AddNode("a", Vector2.zero);
            var b = _view.AddNode("b", new Vector2(100, 0));
            _view.AddToSelection(a);
            _view.AddToSelection(b);
            _view.GroupSelection(Vector2.zero);
            var groupView = _view.GroupViewsForTest[0];

            Assert.AreEqual(2, _graph.Nodes.Count);

            // Simulate GraphView delete: group + its contained node views are all in the list.
            var toRemove = new List<GraphElement> { groupView, a, b };
            _view.HandleRemovalsForTest(toRemove);

            Assert.AreEqual(0, _graph.Groups.Count, "Group data removed.");
            Assert.AreEqual(2, _graph.Nodes.Count, "Contained nodes must survive group deletion.");

            // The protected node views must have been pulled out of the removal list.
            CollectionAssert.DoesNotContain(toRemove, a);
            CollectionAssert.DoesNotContain(toRemove, b);
        }

        [Test]
        public void DeletingNode_Directly_StillRemovesIt()
        {
            var a = _view.AddNode("a", Vector2.zero);

            var toRemove = new List<GraphElement> { a };
            _view.HandleRemovalsForTest(toRemove);

            Assert.AreEqual(0, _graph.Nodes.Count, "A directly-deleted node (no group) must be removed.");
        }

        // ── Remove from group ────────────────────────────────────────────────────────

        [Test]
        public void RemoveContainedNode_DropsItFromGroupMembership()
        {
            var a = _view.AddNode("a", Vector2.zero);
            _view.AddToSelection(a);
            _view.GroupSelection(Vector2.zero);
            var groupView = _view.GroupViewsForTest[0];

            groupView.RemoveContainedNode(a);

            CollectionAssert.DoesNotContain(groupView.GroupData.NodeIds, "a");
            Assert.AreEqual(1, _graph.Nodes.Count, "Node itself must remain in the graph.");
        }
    }
}
