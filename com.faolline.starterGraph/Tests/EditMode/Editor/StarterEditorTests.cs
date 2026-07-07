using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.Experimental.GraphView;
using Faolline.GraphCore;
using Faolline.StarterGraph.Editor;

namespace Faolline.StarterGraph.Tests
{
    /// <summary>US2 — node views, graph-view dispatch + context menu, inspector sections.</summary>
    [TestFixture]
    public class StarterEditorTests
    {
        private StarterGraph _graph;
        private StarterGraphView _view;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<StarterGraph>();
            _view = new StarterGraphView();
            _view.LoadGraph(_graph);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        private void Add(BaseNodeData node)
        {
            var m = typeof(StarterGraphView).GetMethod("AddNodeToCanvas",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            m.Invoke(_view, new object[] { node, Vector2.zero });
        }

        // ── Node views ─────────────────────────────────────────────────────────
        [Test]
        public void ChoiceNodeView_OneInput_OnePortPerChoice_RoutedById()
        {
            var node = new ChoiceNodeData { Id = "c", NodeType = ChoiceNodeData.NodeTypeId };
            node.Choices.Add(new StarterChoice { Id = "a", Label = "A" });
            node.Choices.Add(new StarterChoice { Id = "b", Label = "B" });
            var view = new ChoiceNodeView(node);
            Assert.AreEqual(1, view.inputContainer.Children().OfType<Port>().Count());
            CollectionAssert.AreEquivalent(new[] { "a", "b" }, view.OutputPorts.Select(p => p.portName));
        }

        [Test]
        public void SubGraphNodeView_HasInAndOut()
        {
            var view = new SubGraphNodeView(new SubGraphNodeData { Id = "sg", NodeType = SubGraphNodeData.NodeTypeId });
            Assert.AreEqual(1, view.inputContainer.Children().OfType<Port>().Count());
            Assert.AreEqual(1, view.outputContainer.Children().OfType<Port>().Count());
        }

        // ── Graph view dispatch ────────────────────────────────────────────────
        [Test]
        public void AddNodes_AllTypes_AreDispatched()
        {
            Add(new StartNodeData            { NodeType = StartNodeData.NodeTypeId });
            Add(new StarterStatementNodeData { NodeType = StarterStatementNodeData.NodeTypeId });
            Add(new ChoiceNodeData           { NodeType = ChoiceNodeData.NodeTypeId });
            Add(new SubGraphNodeData         { NodeType = SubGraphNodeData.NodeTypeId });
            Add(new EndNodeData              { NodeType = EndNodeData.NodeTypeId });

            Assert.AreEqual(5, _graph.Nodes.Count);
            var viewTypes = _view.nodes.ToList().Select(n => n.GetType().Name).ToList();
            foreach (var t in new[] { "StartNodeView", "StarterStatementNodeView", "ChoiceNodeView", "SubGraphNodeView", "EndNodeView" })
                Assert.Contains(t, viewTypes, $"{t} must be dispatched by CreateNodeView");
        }

        // ── Inspector sections ─────────────────────────────────────────────────
        [Test]
        public void Inspector_EndReason_SubGraph_Choice_Params()
        {
            var inspector = new StarterNodeInspectorView();
            inspector.SetGraph(_graph);
            inspector.SetGraphView(_view);

            // EndReason
            var end = new EndNodeData { Id = "e", NodeType = EndNodeData.NodeTypeId };
            inspector.SetEndReason(end, EndReason.Cancelled);
            Assert.AreEqual(EndReason.Cancelled, end.EndReason);

            // Choice add/remove
            var choice = new ChoiceNodeData { Id = "c", NodeType = ChoiceNodeData.NodeTypeId };
            inspector.AddChoice(choice);
            Assert.AreEqual(1, choice.Choices.Count);
            inspector.RemoveChoice(choice, choice.Choices[0]);
            Assert.AreEqual(0, choice.Choices.Count);

            // SubGraph inherit toggle
            var sg = new SubGraphNodeData { Id = "sg", NodeType = SubGraphNodeData.NodeTypeId };
            inspector.SetInheritParentContext(sg, true);
            Assert.IsTrue(sg.InheritParentContext);
        }

        [Test]
        public void Inspector_SubGraphTarget_RefusesSelfReference()
        {
            var inspector = new StarterNodeInspectorView();
            inspector.SetGraph(_graph);
            var sg = new SubGraphNodeData { Id = "sg", NodeType = SubGraphNodeData.NodeTypeId };
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Cycle refused"));
            bool ok = inspector.SetSubGraphTarget(sg, _graph); // target == current graph → cycle
            Assert.IsFalse(ok);
            Assert.IsNull(sg.TargetGraph);
        }
    }
}
