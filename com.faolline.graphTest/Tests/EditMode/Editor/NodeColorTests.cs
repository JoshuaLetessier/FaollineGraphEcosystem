using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;
using Faolline.GraphTest.Editor;

namespace Faolline.GraphTest.Tests
{
    /// <summary>
    /// Verifies the node-color override applies to the canvas: setting a node's color override and
    /// calling RefreshNodeColors updates the node view's title background. This is the apply path the
    /// inspector now triggers live on a color-field change (OnNodeVisualsChanged → RefreshNodeColors).
    /// </summary>
    [TestFixture]
    public class NodeColorTests
    {
        [Test]
        public void RefreshNodeColors_AppliesColorOverrideToNodeTitle()
        {
            var graph = ScriptableObject.CreateInstance<TestGraph>();
            var view = new TestGraphView();
            try
            {
                var node = new TestStatementNodeData { Id = "n", NodeType = TestStatementNodeData.NodeTypeId };
                graph.AddNode(node);
                view.LoadGraph(graph);

                node.HasColorOverride = true;
                node.NodeColor = Color.red;
                view.RefreshNodeColors();

                var nodeView = view.nodes.ToList().OfType<BaseNodeView>().First(v => v.NodeData?.Id == "n");
                var body = nodeView.Q("contents") ?? nodeView.mainContainer;
                Assert.AreEqual(Color.red, body.style.backgroundColor.value,
                    "After a color override + RefreshNodeColors, the node body must show the chosen color");
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void RefreshNodeColors_NoOverride_DoesNotForceColor()
        {
            var graph = ScriptableObject.CreateInstance<TestGraph>();
            var view = new TestGraphView();
            try
            {
                var node = new TestStatementNodeData { Id = "n", NodeType = TestStatementNodeData.NodeTypeId };
                graph.AddNode(node);
                view.LoadGraph(graph);

                node.HasColorOverride = false;
                node.NodeColor = Color.red;
                view.RefreshNodeColors();

                var nodeView = view.nodes.ToList().OfType<BaseNodeView>().First(v => v.NodeData?.Id == "n");
                var body = nodeView.Q("contents") ?? nodeView.mainContainer;
                Assert.AreNotEqual(Color.red, body.style.backgroundColor.value,
                    "With HasColorOverride off, the node body must NOT be forced to NodeColor");
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void RefreshNodeColors_TransparentOverride_IsMadeOpaque()
        {
            var graph = ScriptableObject.CreateInstance<TestGraph>();
            var view = new TestGraphView();
            try
            {
                var node = new TestStatementNodeData { Id = "n", NodeType = TestStatementNodeData.NodeTypeId };
                graph.AddNode(node);
                view.LoadGraph(graph);

                node.HasColorOverride = true;
                node.NodeColor = new Color(1f, 0f, 0f, 0f); // red, but fully transparent (the field's default alpha)
                view.RefreshNodeColors();

                var nodeView = view.nodes.ToList().OfType<BaseNodeView>().First(v => v.NodeData?.Id == "n");
                var body = nodeView.Q("contents") ?? nodeView.mainContainer;
                Assert.AreEqual(new Color(1f, 0f, 0f, 1f), body.style.backgroundColor.value,
                    "A transparent override color (alpha 0) must be made opaque so the node body is visible");
            }
            finally { Object.DestroyImmediate(graph); }
        }
    }
}
