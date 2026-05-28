using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphCore.Tests
{
    [TestFixture]
    public class BaseEdgeViewColorTests
    {
        private class TestEdgeView : BaseEdgeView
        {
            public bool UseOverride { get; set; }
            public Color OverrideColorValue { get; set; } = Color.magenta;

            protected override bool HasColorOverride => UseOverride;
            protected override Color ColorOverride => OverrideColorValue;

            public TestEdgeView(BaseEdgeData edgeData)
            {
                Initialize(edgeData);
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
            var data = new BaseEdgeData { Id = System.Guid.NewGuid().ToString("D") };
            var view = new TestEdgeView(data) { UseOverride = true, OverrideColorValue = Color.yellow };
            Assert.AreEqual(Color.yellow, view.ResolveColor());
        }

        [Test]
        public void ResolveColor_NoOverride_NoRegistry_ReturnsNodeGrey()
        {
            var data = new BaseEdgeData { Id = System.Guid.NewGuid().ToString("D") };
            var view = new TestEdgeView(data) { UseOverride = false };
            Assert.AreEqual(GraphCoreDefaults.NodeGrey, view.ResolveColor());
        }

        [Test]
        public void ResolveColor_NullEdgeData_ReturnsNodeGrey()
        {
            var view = new TestEdgeView(null);
            Assert.AreEqual(GraphCoreDefaults.NodeGrey, view.ResolveColor());
        }

        // ── US6: Mirrors BaseNodeView three-step chain ────────────────────────

        [Test]
        public void ResolveColor_HasColorOverride_TakesPrecedence()
        {
            var data = new BaseEdgeData { Id = System.Guid.NewGuid().ToString("D") };
            var view = new TestEdgeView(data) { UseOverride = true, OverrideColorValue = Color.cyan };
            Assert.AreEqual(Color.cyan, view.ResolveColor());
        }

        [Test]
        public void ResolveColor_NoOverride_NoRegistry_AlwaysFallsBack()
        {
            var data = new BaseEdgeData { Id = System.Guid.NewGuid().ToString("D") };
            var view = new TestEdgeView(data) { UseOverride = false };
            Assert.AreEqual(GraphCoreDefaults.NodeGrey, view.ResolveColor());
        }
    }
}
