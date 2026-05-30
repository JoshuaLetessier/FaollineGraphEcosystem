using System.Collections.Generic;
using NUnit.Framework;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// Tests for BaseGraphView.NodeSelected and BaseGraphView.SelectionCleared events.
    /// All tests use the override-based selection detection (AddToSelection/RemoveFromSelection/ClearSelection).
    /// </summary>
    [TestFixture]
    public class BaseGraphViewSelectionTests
    {
        private class StubNodeData : BaseNodeData { }

        private class StubNodeView : BaseNodeView
        {
            public StubNodeView(BaseNodeData data) { Initialize(data); }
            protected override void OnBuildView() { }
        }

        private class StubGraphView : BaseGraphView
        {
            protected override BaseNodeView CreateNodeView(BaseNodeData node) => new StubNodeView(node);
            protected override BaseEdgeView CreateEdgeView(BaseEdgeData edge) => null;
        }

        private StubGraphView _view;

        [SetUp]
        public void SetUp() => _view = new StubGraphView();

        [Test]
        public void NodeSelected_fires_when_single_BaseNodeView_selected()
        {
            var data = new StubNodeData { Id = "n1", NodeType = "test" };
            var nodeView = new StubNodeView(data);

            BaseNodeData firedWith = null;
            _view.NodeSelected += d => firedWith = d;

            _view.AddToSelection(nodeView);

            Assert.AreEqual(data, firedWith,
                "NodeSelected must fire with the selected node's data when exactly one node is selected");
        }

        [Test]
        public void SelectionCleared_fires_when_selection_emptied()
        {
            var data = new StubNodeData { Id = "n1", NodeType = "test" };
            var nodeView = new StubNodeView(data);
            _view.AddToSelection(nodeView);

            bool cleared = false;
            _view.SelectionCleared += () => cleared = true;

            _view.ClearSelection();

            Assert.IsTrue(cleared, "SelectionCleared must fire when ClearSelection is called");
        }

        [Test]
        public void SelectionCleared_fires_when_two_nodes_selected()
        {
            var dataA = new StubNodeData { Id = "a", NodeType = "test" };
            var dataB = new StubNodeData { Id = "b", NodeType = "test" };
            var viewA = new StubNodeView(dataA);
            var viewB = new StubNodeView(dataB);

            _view.AddToSelection(viewA);

            bool cleared = false;
            bool selected = false;
            _view.SelectionCleared += () => cleared = true;
            _view.NodeSelected += _ => selected = true;

            _view.AddToSelection(viewB);

            Assert.IsTrue(cleared, "SelectionCleared must fire when two nodes are in the selection");
            Assert.IsFalse(selected, "NodeSelected must NOT fire when two nodes are selected");
        }

        [Test]
        public void NodeSelected_fires_with_correct_data_after_swap()
        {
            var dataA = new StubNodeData { Id = "a", NodeType = "test" };
            var dataB = new StubNodeData { Id = "b", NodeType = "test" };
            var viewA = new StubNodeView(dataA);
            var viewB = new StubNodeView(dataB);

            var fired = new List<BaseNodeData>();
            _view.NodeSelected += d => fired.Add(d);

            _view.AddToSelection(viewA);
            _view.ClearSelection();
            _view.AddToSelection(viewB);

            Assert.AreEqual(2, fired.Count,
                "NodeSelected must fire once per single-node selection (twice total after swap)");
            Assert.AreEqual(dataA, fired[0], "First NodeSelected must carry dataA");
            Assert.AreEqual(dataB, fired[1], "Second NodeSelected must carry dataB");
        }
    }
}
