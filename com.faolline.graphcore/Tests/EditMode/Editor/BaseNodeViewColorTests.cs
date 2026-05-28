using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphCore.Tests
{
    [TestFixture]
    public class BaseNodeViewColorTests
    {
        // Concrete test-double: minimal BaseNodeData subclass
        private class TestNodeData : BaseNodeData { }

        // Concrete test-double: BaseNodeView with configurable override
        private class TestNodeView : BaseNodeView
        {
            public bool UseOverride { get; set; }
            public Color OverrideColorValue { get; set; } = Color.magenta;

            protected override bool HasColorOverride => UseOverride;
            protected override Color ColorOverride => OverrideColorValue;

            protected override void OnBuildView() { }

            public TestNodeView(BaseNodeData nodeData)
            {
                Initialize(nodeData);
            }
        }

        [TearDown]
        public void TearDown()
        {
            NodeTypeColorRegistry.Clear();
        }

        [Test]
        public void ResolveColor_HasColorOverride_ReturnsOverrideColor()
        {
            var data = new TestNodeData();
            data.NodeType = "test/override";
            var view = new TestNodeView(data) { UseOverride = true, OverrideColorValue = Color.cyan };

            Assert.AreEqual(Color.cyan, view.ResolveColor());
        }

        [Test]
        public void ResolveColor_NoOverride_RegisteredType_ReturnsRegistryColor()
        {
            NodeTypeColorRegistry.Register("test/registered", Color.green);
            var data = new TestNodeData();
            data.NodeType = "test/registered";
            var view = new TestNodeView(data) { UseOverride = false };

            Assert.AreEqual(Color.green, view.ResolveColor());
        }

        [Test]
        public void ResolveColor_NoOverride_NoRegistry_ReturnsNodeGrey()
        {
            var data = new TestNodeData();
            data.NodeType = "test/unknown";
            var view = new TestNodeView(data) { UseOverride = false };

            Assert.AreEqual(GraphCoreDefaults.NodeGrey, view.ResolveColor());
        }

        [Test]
        public void ResolveColor_NullNodeData_ReturnsNodeGrey()
        {
            var view = new TestNodeView(null);
            Assert.AreEqual(GraphCoreDefaults.NodeGrey, view.ResolveColor());
        }

        // ── US6: Registry integration tests ──────────────────────────────────

        [Test]
        public void ResolveColor_NoOverride_RegisteredNodeType_ReturnsRegistryColor()
        {
            NodeTypeColorRegistry.Register("test/registered-node", Color.red);
            var data = new TestNodeData();
            data.NodeType = "test/registered-node";
            var view = new TestNodeView(data) { UseOverride = false };

            Assert.AreEqual(Color.red, view.ResolveColor());
        }

        [Test]
        public void ResolveColor_AfterClear_FallsBackToNodeGrey()
        {
            NodeTypeColorRegistry.Register("test/cleared", Color.blue);
            NodeTypeColorRegistry.Clear();

            var data = new TestNodeData();
            data.NodeType = "test/cleared";
            var view = new TestNodeView(data) { UseOverride = false };

            Assert.AreEqual(GraphCoreDefaults.NodeGrey, view.ResolveColor());
        }
    }
}
