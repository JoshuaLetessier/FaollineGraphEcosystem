using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphGameFlow;

namespace Faolline.GraphGameFlow.Tests
{
    /// <summary>
    /// Constitution VI: a typed context subclass must clone to its own type and carry its domain fields, or
    /// history snapshot restore silently breaks. For slice 1 the domain field is the scene loader service.
    /// </summary>
    public class GameFlowContextTests
    {
        [Test]
        public void DeepClone_ReturnsGameFlowContext_CarryingSceneLoaderAndValues()
        {
            var ctx = new GameFlowContext();
            var stub = new StubSceneLoader();
            ctx.SceneLoader = stub;
            ctx.Set<int>("hp", 5);

            var clone = ctx.DeepClone();

            Assert.IsInstanceOf<GameFlowContext>(clone, "clone must be the subclass, not a bare BaseContext.");
            Assert.AreSame(stub, ((GameFlowContext)clone).SceneLoader, "the loader reference must carry through.");
            Assert.AreEqual(5, clone.Get<int>("hp"), "base parameter values must carry through.");
        }

        [Test]
        public void GraphCatalog_DefaultsToNull_IsSettable_AndCarriesThroughClone()
        {
            var ctx = new GameFlowContext();
            Assert.IsNull(ctx.GraphCatalog, "a project with a single root graph never has to set this.");

            var catalog = new DirectGraphCatalog();
            ctx.GraphCatalog = catalog;

            var clone = (GameFlowContext)ctx.DeepClone();
            Assert.AreSame(catalog, clone.GraphCatalog, "a shared service reference, like SceneLoader, carries through clone.");
        }

        [Test]
        public void PendingNextGraph_DefaultsToNull_IsSettable_AndCarriesThroughClone()
        {
            var ctx = new GameFlowContext();
            Assert.IsNull(ctx.PendingNextGraph, "a project that never triggers a preload never touches this.");

            var graph = ScriptableObject.CreateInstance<BaseGraph>();
            try
            {
                ctx.PendingNextGraph = graph;
                var clone = (GameFlowContext)ctx.DeepClone();
                Assert.AreSame(graph, clone.PendingNextGraph);
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void DeepClone_IsIndependentForValues()
        {
            var ctx = new GameFlowContext();
            ctx.Set<int>("hp", 1);
            var clone = ctx.DeepClone();

            clone.Set<int>("hp", 99);

            Assert.AreEqual(1, ctx.Get<int>("hp"), "mutating the clone must not affect the source.");
        }

        // ── Scene registry ───────────────────────────────────────────────────────

        [Test]
        public void IsSceneLoaded_FalseUntilMarked_ThenTrue()
        {
            var ctx = new GameFlowContext();

            Assert.IsFalse(ctx.IsSceneLoaded("Hub"));

            ctx.MarkSceneLoaded("Hub");

            Assert.IsTrue(ctx.IsSceneLoaded("Hub"));
            CollectionAssert.Contains(ctx.LoadedScenes, "Hub");
        }

        [Test]
        public void MarkSceneUnloaded_RemovesFromRegistry()
        {
            var ctx = new GameFlowContext();
            ctx.MarkSceneLoaded("Overlay");

            ctx.MarkSceneUnloaded("Overlay");

            Assert.IsFalse(ctx.IsSceneLoaded("Overlay"));
            CollectionAssert.DoesNotContain(ctx.LoadedScenes, "Overlay");
        }

        [Test]
        public void MarkSceneLoaded_IsIdempotent()
        {
            var ctx = new GameFlowContext();
            ctx.MarkSceneLoaded("Hub");
            ctx.MarkSceneLoaded("Hub");

            Assert.AreEqual(1, ctx.LoadedScenes.Count);
        }

        [TestCase(null)]
        [TestCase("")]
        public void MarkSceneLoaded_NullOrEmpty_IsNoOp(string sceneName)
        {
            var ctx = new GameFlowContext();
            Assert.DoesNotThrow(() => ctx.MarkSceneLoaded(sceneName));
            Assert.AreEqual(0, ctx.LoadedScenes.Count);
        }

        [TestCase(null)]
        [TestCase("")]
        public void IsSceneLoaded_NullOrEmpty_ReturnsFalse(string sceneName)
        {
            var ctx = new GameFlowContext();
            Assert.IsFalse(ctx.IsSceneLoaded(sceneName));
        }

        [Test]
        public void DeepClone_CarriesLoadedScenes_Independently()
        {
            var ctx = new GameFlowContext();
            ctx.MarkSceneLoaded("Hub");

            var clone = (GameFlowContext)ctx.DeepClone();
            Assert.IsTrue(clone.IsSceneLoaded("Hub"), "the registry carries through on clone.");

            clone.MarkSceneLoaded("Overlay");

            Assert.IsFalse(ctx.IsSceneLoaded("Overlay"), "mutating the clone's registry must not affect the source.");
        }
    }
}
