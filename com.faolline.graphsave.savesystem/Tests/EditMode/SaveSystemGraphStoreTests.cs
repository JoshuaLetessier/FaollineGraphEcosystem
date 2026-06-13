using System.Collections.Generic;
using NUnit.Framework;
using Faolline.GraphCore;
using Faolline.GraphSave;
using SaveSystem;
using SaveSystem.SSJson;

namespace Faolline.GraphSave.UnitySaveSystem.Tests
{
    /// <summary>The UnitySaveSystem bridge: delegation to a backend, and a real JSON-backend round-trip.</summary>
    public class SaveSystemGraphStoreTests
    {
        private static GraphRunSnapshot Sample()
        {
            var ctx = new BaseContext();
            ctx.Set<int>("score", 7);
            ctx.AddToCollection("completed", "p1");
            return GraphRunSnapshot.Capture(ctx, "g", "node3");
        }

        [Test]
        public void Bridge_DelegatesToBackend()
        {
            var backend = new FakeBackend();
            IGraphSaveStore store = new SaveSystemGraphStore(backend);

            Assert.IsFalse(store.Exists("s"));
            store.Save("s", Sample());
            Assert.IsTrue(store.Exists("s"));
            Assert.IsTrue(backend.Map.ContainsKey("s"), "save reaches the wrapped backend.");

            var loaded = store.Load("s");
            Assert.IsNotNull(loaded);
            Assert.AreEqual("node3", loaded.CurrentNodeId);

            store.Delete("s");
            Assert.IsFalse(store.Exists("s"));
            Assert.IsNull(store.Load("s"), "load on a missing slot returns null (never throws).");
        }

        [Test]
        public void Json_Backend_RoundTripsThroughDisk()
        {
            var store = new SaveSystemGraphStore(new JsonSaveSystem<GraphRunSnapshot>());
            var slot = "graphsave_bridge_test_" + System.Guid.NewGuid().ToString("N");
            try
            {
                store.Save(slot, Sample());
                Assert.IsTrue(store.Exists(slot));

                var loaded = store.Load(slot);
                Assert.IsNotNull(loaded);
                Assert.AreEqual("node3", loaded.CurrentNodeId);

                var ctx = new BaseContext();
                loaded.ApplyTo(ctx);
                Assert.IsTrue(ctx.TryGet<int>("score", out var s) && s == 7);
                Assert.IsTrue(ctx.CollectionContains("completed", "p1"));
            }
            finally { store.Delete(slot); }
        }

        // In-memory ISaveSystem<GraphRunSnapshot> standing in for a real backend.
        private sealed class FakeBackend : ISaveSystem<GraphRunSnapshot>
        {
            public readonly Dictionary<string, GraphRunSnapshot> Map = new Dictionary<string, GraphRunSnapshot>();
            public void Save(string key, GraphRunSnapshot data) => Map[key] = data;
            public GraphRunSnapshot Load(string key) => Map.TryGetValue(key, out var v) ? v : null;
            public void Delete(string key) => Map.Remove(key);
            public void DeleteAll() => Map.Clear();
            public bool Exists(string key) => Map.ContainsKey(key);
        }
    }
}
