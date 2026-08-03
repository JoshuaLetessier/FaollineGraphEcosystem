using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Faolline.GraphGameFlow.Editor;

namespace Faolline.GraphGameFlow.Tests
{
    /// <summary>Mirrors <c>SceneKeySourceRegistryTests</c> — covers the seam itself plus the new reverse lookup.</summary>
    public class GraphKeySourceRegistryTests
    {
        private sealed class FakeProvider : IGraphKeySourceProvider
        {
            public string SourceLabel { get; set; } = "Fake";
            public List<string> Keys = new List<string>();
            public bool Promotable = true;
            public string PromotedPath;
            public string PromotedGraphId;
            public Dictionary<string, string> GuidToKey = new Dictionary<string, string>();

            public IReadOnlyList<string> GetKeys() => Keys;
            public bool CanPromote(string graphAssetPath, string graphId) => Promotable;
            public void Promote(string graphAssetPath, string graphId)
            {
                PromotedPath = graphAssetPath;
                PromotedGraphId = graphId;
            }

            public bool TryResolveGuid(string assetGuid, out string key) => GuidToKey.TryGetValue(assetGuid ?? string.Empty, out key);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var p in new List<IGraphKeySourceProvider>(GraphKeySourceRegistry.Providers))
                GraphKeySourceRegistry.Unregister(p);
        }

        [Test]
        public void Register_AddsProvider()
        {
            var p = new FakeProvider();
            GraphKeySourceRegistry.Register(p);
            Assert.IsTrue(GraphKeySourceRegistry.Providers.Contains(p));
        }

        [Test]
        public void Register_SameProviderTwice_Idempotent()
        {
            var p = new FakeProvider();
            var before = GraphKeySourceRegistry.Providers.Count;
            GraphKeySourceRegistry.Register(p);
            GraphKeySourceRegistry.Register(p);
            Assert.AreEqual(before + 1, GraphKeySourceRegistry.Providers.Count);
        }

        [Test]
        public void Register_Null_NoOp()
        {
            var before = GraphKeySourceRegistry.Providers.Count;
            GraphKeySourceRegistry.Register(null);
            Assert.AreEqual(before, GraphKeySourceRegistry.Providers.Count);
        }

        [Test]
        public void Unregister_RemovesProvider()
        {
            var p = new FakeProvider();
            GraphKeySourceRegistry.Register(p);
            GraphKeySourceRegistry.Unregister(p);
            Assert.IsFalse(GraphKeySourceRegistry.Providers.Contains(p));
        }

        [Test]
        public void TryResolveGuid_PromotedAsset_ReturnsTrueAndKey()
        {
            var p = new FakeProvider();
            p.GuidToKey["abc123"] = "chapter-2";
            GraphKeySourceRegistry.Register(p);

            Assert.IsTrue(p.TryResolveGuid("abc123", out var key));
            Assert.AreEqual("chapter-2", key);
        }

        [Test]
        public void TryResolveGuid_UnpromotedAsset_ReturnsFalse()
        {
            var p = new FakeProvider();
            GraphKeySourceRegistry.Register(p);

            Assert.IsFalse(p.TryResolveGuid("never-promoted", out _));
        }
    }
}
