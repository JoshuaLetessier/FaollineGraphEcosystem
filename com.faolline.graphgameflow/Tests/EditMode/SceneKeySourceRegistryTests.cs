using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Faolline.GraphGameFlow.Editor;

namespace Faolline.GraphGameFlow.Tests
{
    /// <summary>
    /// Covers the seam itself (registration bookkeeping) — mirrors <c>NodeTypeColorRegistryTests</c> /
    /// <c>GraphEditorWindowRegistryTests</c> in graphcore. <see cref="SceneNameFieldDrawer"/>'s IMGUI
    /// consumption of the registry is not covered here (no IMGUI test harness in this codebase, same as the
    /// pre-existing <c>LoadSceneActionEditor</c>/<c>UnloadSceneActionEditor</c>).
    /// </summary>
    public class SceneKeySourceRegistryTests
    {
        private sealed class FakeProvider : ISceneKeySourceProvider
        {
            public string SourceLabel { get; set; } = "Fake";
            public List<string> Keys = new List<string>();
            public bool Promotable = true;
            public string PromotedPath;
            public string PromotedName;

            public IReadOnlyList<string> GetKeys() => Keys;
            public bool CanPromote(string projectScenePath, string sceneName) => Promotable;
            public void Promote(string projectScenePath, string sceneName)
            {
                PromotedPath = projectScenePath;
                PromotedName = sceneName;
            }
        }

        [TearDown]
        public void TearDown()
        {
            // Registry state is static/global — unregister everything a test added so tests stay isolated.
            foreach (var p in new List<ISceneKeySourceProvider>(SceneKeySourceRegistry.Providers))
                SceneKeySourceRegistry.Unregister(p);
        }

        [Test]
        public void Register_AddsProvider()
        {
            var p = new FakeProvider();
            SceneKeySourceRegistry.Register(p);
            Assert.IsTrue(SceneKeySourceRegistry.Providers.Contains(p));
        }

        [Test]
        public void Register_SameProviderTwice_Idempotent()
        {
            var p = new FakeProvider();
            SceneKeySourceRegistry.Register(p);
            SceneKeySourceRegistry.Register(p);
            Assert.AreEqual(1, SceneKeySourceRegistry.Providers.Count);
        }

        [Test]
        public void Register_Null_NoOp()
        {
            SceneKeySourceRegistry.Register(null);
            Assert.AreEqual(0, SceneKeySourceRegistry.Providers.Count);
        }

        [Test]
        public void Unregister_RemovesProvider()
        {
            var p = new FakeProvider();
            SceneKeySourceRegistry.Register(p);
            SceneKeySourceRegistry.Unregister(p);
            Assert.AreEqual(0, SceneKeySourceRegistry.Providers.Count);
        }

        [Test]
        public void Providers_PreservesRegistrationOrder()
        {
            var a = new FakeProvider { SourceLabel = "A" };
            var b = new FakeProvider { SourceLabel = "B" };
            SceneKeySourceRegistry.Register(a);
            SceneKeySourceRegistry.Register(b);

            Assert.AreEqual("A", SceneKeySourceRegistry.Providers[0].SourceLabel);
            Assert.AreEqual("B", SceneKeySourceRegistry.Providers[1].SourceLabel);
        }

        [Test]
        public void FakeProvider_PromoteRecordsArguments()
        {
            var p = new FakeProvider();
            p.Promote("Assets/Scenes/Foo.unity", "Foo");
            Assert.AreEqual("Assets/Scenes/Foo.unity", p.PromotedPath);
            Assert.AreEqual("Foo", p.PromotedName);
        }
    }
}
