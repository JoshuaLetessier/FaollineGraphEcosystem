using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// <see cref="GraphValidatorExtensionRegistry"/> is the generic seam a downstream lib (never
    /// graphcore itself) plugs domain-specific validation opinions into, mirroring the ecosystem's
    /// existing ContextKeyLabelRegistry shape. Empty by default; graphcore never registers into it.
    /// </summary>
    public class GraphValidatorExtensionRegistryTests
    {
        private sealed class FakeExtension : IGraphValidatorExtension
        {
            public string Result;
            public string CheckSubGraphTarget(BaseGraph targetGraph) => Result;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var ext in new List<IGraphValidatorExtension>(GraphValidatorExtensionRegistry.Extensions))
                GraphValidatorExtensionRegistry.Unregister(ext);
        }

        // No "empty by default" assertion here — a downstream lib present in the SAME project (e.g.
        // graphgameflow's ChapterRootSubGraphValidatorExtension) self-registers via [InitializeOnLoadMethod]
        // at domain-load, before any test runs, so the registry is legitimately non-empty in a real project.
        // Mirrors SceneKeySourceRegistryTests.cs, which for the identical reason never asserts this either —
        // only relative (Contains/Count-delta) assertions are safe here.

        [Test]
        public void Register_AddsExtension()
        {
            var ext = new FakeExtension();
            GraphValidatorExtensionRegistry.Register(ext);
            Assert.IsTrue(GraphValidatorExtensionRegistry.Extensions.Contains(ext));
        }

        [Test]
        public void Register_SameExtensionTwice_Idempotent()
        {
            var ext = new FakeExtension();
            var before = GraphValidatorExtensionRegistry.Extensions.Count;
            GraphValidatorExtensionRegistry.Register(ext);
            GraphValidatorExtensionRegistry.Register(ext);

            Assert.AreEqual(before + 1, GraphValidatorExtensionRegistry.Extensions.Count,
                "registering the same instance twice must not duplicate it.");
        }

        [Test]
        public void Register_Null_IsIgnored()
        {
            var before = GraphValidatorExtensionRegistry.Extensions.Count;
            GraphValidatorExtensionRegistry.Register(null);
            Assert.AreEqual(before, GraphValidatorExtensionRegistry.Extensions.Count);
        }

        [Test]
        public void Unregister_RemovesExtension()
        {
            var ext = new FakeExtension();
            GraphValidatorExtensionRegistry.Register(ext);
            GraphValidatorExtensionRegistry.Unregister(ext);

            Assert.IsFalse(GraphValidatorExtensionRegistry.Extensions.Contains(ext));
        }
    }
}
