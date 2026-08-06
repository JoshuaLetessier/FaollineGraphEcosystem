using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
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

        [Test]
        public void Save_BackendThrows_DoesNotPropagate()
        {
            IGraphSaveStore store = new SaveSystemGraphStore(new ThrowingBackend());
            LogAssert.Expect(LogType.Error, new Regex(@"\[GraphSave\] Backend threw while saving.*"));
            Assert.DoesNotThrow(() => store.Save("s", Sample()));
        }

        [Test]
        public void Load_BackendThrowsOnExists_ReturnsNull()
        {
            IGraphSaveStore store = new SaveSystemGraphStore(new ThrowingBackend());
            GraphRunSnapshot loaded = null;
            LogAssert.Expect(LogType.Warning, new Regex(@"\[GraphSave\] Backend threw while loading.*"));
            Assert.DoesNotThrow(() => loaded = store.Load("s"));
            Assert.IsNull(loaded);
        }

        [Test]
        public void Exists_BackendExistsTrueButLoadReturnsNull_ReturnsFalse()
        {
            // Simulates JsonSaveSystem's corrupted-checksum case: the backend's own Exists() is a raw
            // presence check that says "yes", but its Load() additionally validates and returns null for
            // the same slot. The bridge must not repeat that inconsistency to its own callers.
            IGraphSaveStore store = new SaveSystemGraphStore(new InconsistentBackend());
            Assert.IsFalse(store.Exists("s"), "Exists() must agree with Load() even when the backend itself doesn't.");
        }

        [Test]
        public void Exists_BackendThrows_ReturnsFalse()
        {
            IGraphSaveStore store = new SaveSystemGraphStore(new ThrowingBackend());
            var exists = true;
            LogAssert.Expect(LogType.Warning, new Regex(@"\[GraphSave\] Backend threw while checking.*"));
            Assert.DoesNotThrow(() => exists = store.Exists("s"));
            Assert.IsFalse(exists);
        }

        [Test]
        public void Delete_BackendThrows_DoesNotPropagate()
        {
            IGraphSaveStore store = new SaveSystemGraphStore(new ThrowingBackend());
            LogAssert.Expect(LogType.Warning, new Regex(@"\[GraphSave\] Backend threw while deleting.*"));
            Assert.DoesNotThrow(() => store.Delete("s"));
        }

        [Test]
        public void GetAllKeys_DelegatesToBackend()
        {
            var backend = new FakeBackend();
            IGraphSaveStore store = new SaveSystemGraphStore(backend);
            store.Save("a", Sample());
            store.Save("b", Sample());

            CollectionAssert.AreEquivalent(new[] { "a", "b" }, store.GetAllKeys());
        }

        [Test]
        public void GetAllKeys_BackendThrows_ReturnsEmpty()
        {
            IGraphSaveStore store = new SaveSystemGraphStore(new ThrowingBackend());
            LogAssert.Expect(LogType.Warning, new Regex(@"\[GraphSave\] Backend threw while listing keys.*"));
            IEnumerable<string> keys = null;
            Assert.DoesNotThrow(() => keys = store.GetAllKeys());
            CollectionAssert.IsEmpty(keys);
        }

        [Test]
        public void DeleteAll_DelegatesToBackend()
        {
            var backend = new FakeBackend();
            IGraphSaveStore store = new SaveSystemGraphStore(backend);
            store.Save("a", Sample());
            store.Save("b", Sample());

            store.DeleteAll();

            Assert.IsFalse(store.Exists("a"));
            Assert.IsFalse(store.Exists("b"));
        }

        [Test]
        public void DeleteAll_BackendThrows_DoesNotPropagate()
        {
            IGraphSaveStore store = new SaveSystemGraphStore(new ThrowingBackend());
            LogAssert.Expect(LogType.Warning, new Regex(@"\[GraphSave\] Backend threw while deleting all slots.*"));
            Assert.DoesNotThrow(() => store.DeleteAll());
        }

        [Test]
        public void Json_Backend_GetAllKeysAndDeleteAll_RoundTripThroughDisk()
        {
            var store = new SaveSystemGraphStore(new JsonSaveSystem<GraphRunSnapshot>());
            var slotA = "graphsave_bridge_keys_" + System.Guid.NewGuid().ToString("N");
            var slotB = "graphsave_bridge_keys_" + System.Guid.NewGuid().ToString("N");
            try
            {
                store.Save(slotA, Sample());
                store.Save(slotB, Sample());

                var keys = store.GetAllKeys().ToList();
                CollectionAssert.Contains(keys, slotA);
                CollectionAssert.Contains(keys, slotB);
            }
            finally
            {
                store.Delete(slotA);
                store.Delete(slotB);
            }
        }

        [Test]
        public void Json_Backend_PathTraversalSlot_DoesNotEscapeAndDegradesGracefully()
        {
            // End-to-end through the REAL JsonSaveSystem (not a synthetic double): confirms the external
            // package's own path-traversal rejection actually reaches the caller as graceful null/false/no-op
            // through our bridge, and that no file is ever written outside its own Saves/ folder.
            var store = new SaveSystemGraphStore(new JsonSaveSystem<GraphRunSnapshot>());
            var afterTraversal = "graphsave_bridge_traversal_" + System.Guid.NewGuid().ToString("N");
            var slot = "../" + afterTraversal;
            var escapedPath = Path.Combine(Application.persistentDataPath, afterTraversal + ".json");

            LogAssert.ignoreFailingMessages = true;
            try
            {
                Assert.DoesNotThrow(() => store.Save(slot, Sample()));
                Assert.IsFalse(store.Exists(slot));
                Assert.IsNull(store.Load(slot));
                Assert.DoesNotThrow(() => store.Delete(slot));

                Assert.IsFalse(File.Exists(escapedPath), "a path-traversal slot must never write outside the backend's own save folder.");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        [Test]
        public void Json_Backend_CorruptedChecksumOnDisk_ExistsAgreesWithLoad()
        {
            // Exercises the #3 fix (Exists() cross-checking Load()) against the REAL checksum mechanism,
            // not just the synthetic InconsistentBackend double.
            var store = new SaveSystemGraphStore(new JsonSaveSystem<GraphRunSnapshot>());
            var slot = "graphsave_bridge_corrupt_" + System.Guid.NewGuid().ToString("N");
            var path = Path.Combine(Application.persistentDataPath, "Saves", slot + ".json");
            try
            {
                store.Save(slot, Sample());
                Assert.IsTrue(store.Exists(slot), "sanity: freshly-saved slot exists before corruption.");

                // Flip a byte near the start of the file — always inside the JSON portion (which precedes the
                // "---CHECKSUM---" marker) regardless of payload size — so the stored checksum no longer matches.
                var bytes = File.ReadAllBytes(path);
                bytes[5] ^= 0xFF;
                File.WriteAllBytes(path, bytes);

                LogAssert.ignoreFailingMessages = true;
                Assert.IsNull(store.Load(slot), "sanity: the real checksum mechanism already returns null for corrupted data.");
                Assert.IsFalse(store.Exists(slot), "Exists() must agree with Load() against the REAL checksum mechanism, not just a synthetic double.");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
                store.Delete(slot);
            }
        }

        [Test]
        public void CrossStore_TraversalSlotName_BothRejectConsistently()
        {
            // Both IGraphSaveStore implementations now handle a traversal-shaped slot name the SAME way.
            // graphsave 0.8.0 closed a previously-documented asymmetry: JsonFileGraphSaveStore used to
            // SANITIZE (replace bad characters, always succeed under a mangled name) while this bridge +
            // JsonSaveSystem REJECTS (refuses the key, silently no-ops) — same slot name, same
            // IGraphSaveStore contract, different actual persistence outcome. Both now reject, matching
            // JsonSaveSystem's own reject-and-log strategy for the same path-traversal risk (fix 330c049).
            var slot = "../traversal_" + System.Guid.NewGuid().ToString("N");
            var tempDir = Path.Combine(Path.GetTempPath(), "GraphSaveCrossStoreTest_" + System.Guid.NewGuid().ToString("N"));

            try
            {
                var fileStore = new JsonFileGraphSaveStore(tempDir);
                LogAssert.ignoreFailingMessages = true;
                fileStore.Save(slot, Sample());
                Assert.IsFalse(fileStore.Exists(slot), "JsonFileGraphSaveStore now rejects a traversal-shaped slot name instead of sanitizing it.");

                var bridgeStore = new SaveSystemGraphStore(new JsonSaveSystem<GraphRunSnapshot>());
                bridgeStore.Save(slot, Sample());
                Assert.IsFalse(bridgeStore.Exists(slot), "SaveSystemGraphStore + JsonSaveSystem also rejects the same slot name.");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
            }
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
            public IEnumerable<string> GetAllKeys() => Map.Keys;
        }

        // A less-defensive custom backend that throws on every call — the scenario the bridge must survive.
        private sealed class ThrowingBackend : ISaveSystem<GraphRunSnapshot>
        {
            public void Save(string key, GraphRunSnapshot data) => throw new System.InvalidOperationException("backend save failure");
            public GraphRunSnapshot Load(string key) => throw new System.InvalidOperationException("backend load failure");
            public void Delete(string key) => throw new System.InvalidOperationException("backend delete failure");
            public void DeleteAll() => throw new System.InvalidOperationException("backend deleteAll failure");
            public bool Exists(string key) => throw new System.InvalidOperationException("backend exists failure");
            public IEnumerable<string> GetAllKeys() => throw new System.InvalidOperationException("backend getAllKeys failure");
        }

        // Stands in for a backend whose Exists() is a raw presence check while Load() additionally
        // validates integrity (e.g. JsonSaveSystem's on-disk checksum) and can return null for a
        // corrupted-but-present file.
        private sealed class InconsistentBackend : ISaveSystem<GraphRunSnapshot>
        {
            public bool Exists(string key) => true;
            public GraphRunSnapshot Load(string key) => null;
            public void Save(string key, GraphRunSnapshot data) { }
            public void Delete(string key) { }
            public void DeleteAll() { }
            public IEnumerable<string> GetAllKeys() => System.Array.Empty<string>();
        }
    }
}
