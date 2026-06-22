using NUnit.Framework;
using Faolline.GraphCore;

namespace Faolline.GraphTest.Tests
{
    [TestFixture]
    public class GraphRunContextRegistryTests
    {
        private class StubProbe : IGraphRunProbe
        {
            public GraphRunNodeStatus StatusOf(BaseGraph graph, string nodeId) => GraphRunNodeStatus.None;
            public string ActiveNodeId(BaseGraph graph) => null;
        }

        [TearDown]
        public void TearDown()
        {
            GraphRunContextRegistry.Unregister(_probe);
        }

        private StubProbe _probe = new StubProbe();

        [Test]
        public void Register_And_GetContext_ReturnsRegisteredContext()
        {
            var ctx = new BaseContext();
            GraphRunContextRegistry.Register(_probe, ctx);

            Assert.AreSame(ctx, GraphRunContextRegistry.GetContext(_probe));
        }

        [Test]
        public void Unregister_RemovesEntry()
        {
            var ctx = new BaseContext();
            GraphRunContextRegistry.Register(_probe, ctx);
            GraphRunContextRegistry.Unregister(_probe);

            Assert.IsNull(GraphRunContextRegistry.GetContext(_probe));
        }

        [Test]
        public void GetContext_UnknownProbe_ReturnsNull()
        {
            var unknown = new StubProbe();
            Assert.IsNull(GraphRunContextRegistry.GetContext(unknown));
        }
    }
}
