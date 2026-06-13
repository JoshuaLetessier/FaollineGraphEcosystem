using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphSave.Tests
{
    /// <summary>The neutral save core: snapshot capture/apply, JSON round-trip, the store seam, runner restore.</summary>
    public class GraphSaveTests
    {
        [Test]
        public void Capture_And_ApplyTo_RoundTripsParamsAndCollections()
        {
            var ctx = new BaseContext();
            ctx.Set<int>("score", 5);
            ctx.Set<float>("hp", 0.3f);
            ctx.Set<string>("name", "hero");
            ctx.Set<bool>("door", true);
            ctx.AddToCollection("completed", "puzzle1");
            ctx.AddToCollection("completed", "puzzle2");

            var snap = GraphRunSnapshot.Capture(ctx, "g1", "node7");
            Assert.AreEqual("node7", snap.CurrentNodeId);
            Assert.AreEqual("g1", snap.GraphId);

            var restored = new BaseContext();
            snap.ApplyTo(restored);

            Assert.IsTrue(restored.TryGet<int>("score", out var sc) && sc == 5);
            Assert.IsTrue(restored.TryGet<float>("hp", out var hp) && Mathf.Approximately(hp, 0.3f));
            Assert.IsTrue(restored.TryGet<string>("name", out var nm) && nm == "hero");
            Assert.IsTrue(restored.TryGet<bool>("door", out var dr) && dr);
            Assert.IsTrue(restored.CollectionContains("completed", "puzzle1"));
            Assert.IsTrue(restored.CollectionContains("completed", "puzzle2"));
        }

        [Test]
        public void JsonUtility_RoundTrip_PreservesSnapshot()
        {
            var ctx = new BaseContext();
            ctx.Set<int>("k", 42);
            ctx.AddToCollection("set", "a");

            var snap = GraphRunSnapshot.Capture(ctx, "g", "n");
            var json = JsonUtility.ToJson(snap);
            var back = JsonUtility.FromJson<GraphRunSnapshot>(json);

            Assert.AreEqual("n", back.CurrentNodeId);
            var ctx2 = new BaseContext();
            back.ApplyTo(ctx2);
            Assert.IsTrue(ctx2.TryGet<int>("k", out var k) && k == 42);
            Assert.IsTrue(ctx2.CollectionContains("set", "a"));
        }

        [Test]
        public void ApplyTo_ReplaceCollections_DoesNotDoubleOntoPopulatedContext()
        {
            var snap = new GraphRunSnapshot();
            snap.Collections.Add(new GraphRunSnapshot.Collection { Key = "completed", Items = { "p1" } });

            // A context that already holds the item (e.g. a previous run / replay).
            var ctx = new BaseContext();
            ctx.AddToCollection("completed", "p1");

            // Default merge re-adds (the collection is a set, so still just one — but counts must be 1).
            snap.ApplyTo(ctx);
            Assert.AreEqual(1, ctx.CollectionCount("completed"), "a set collection stays deduped under merge.");

            // Replace clears first, so the snapshot is authoritative even with extra pre-existing items.
            ctx.AddToCollection("completed", "stale");
            snap.ApplyTo(ctx, replaceCollections: true);
            Assert.IsTrue(ctx.CollectionContains("completed", "p1"));
            Assert.IsFalse(ctx.CollectionContains("completed", "stale"), "replace drops items not in the snapshot.");
            Assert.AreEqual(1, ctx.CollectionCount("completed"));
        }

        [Test]
        public void Store_Save_Load_Exists_Delete()
        {
            var store = new MemoryStore();
            var ctx = new BaseContext();
            ctx.Set<int>("x", 1);
            var snap = GraphRunSnapshot.Capture(ctx, "g", "n");

            Assert.IsFalse(store.Exists("slot0"));
            store.Save("slot0", snap);
            Assert.IsTrue(store.Exists("slot0"));

            var loaded = store.Load("slot0");
            Assert.IsNotNull(loaded);
            var c = new BaseContext();
            loaded.ApplyTo(c);
            Assert.IsTrue(c.TryGet<int>("x", out var x) && x == 1);

            store.Delete("slot0");
            Assert.IsFalse(store.Exists("slot0"));
            Assert.IsNull(store.Load("slot0"));
        }

        [Test]
        public void Restore_RehydratesContext_AndEntersSavedNode()
        {
            var graph = ScriptableObject.CreateInstance<BaseGraph>();
            var start = new StartNodeData { Id = "start", NodeType = StartNodeData.NodeTypeId };
            graph.AddNode(start);
            graph.EntryNodeId = "start";
            try
            {
                var srcCtx = new BaseContext();
                srcCtx.Set<int>("score", 9);
                var snap = GraphRunSnapshot.Capture(srcCtx, graph.GraphId, "start");

                var runner = new BaseRunner();
                var ctx = new BaseContext();
                snap.Restore(runner, graph, ctx);

                Assert.AreEqual("start", runner.CurrentNode?.Id, "restore re-enters the saved node.");
                Assert.IsTrue(ctx.TryGet<int>("score", out var s) && s == 9, "context state is rehydrated.");
            }
            finally { Object.DestroyImmediate(graph); }
        }

        // In-memory store that mirrors a real one by going through JSON, exercising the serializable contract.
        private sealed class MemoryStore : IGraphSaveStore
        {
            private readonly Dictionary<string, string> _json = new Dictionary<string, string>();
            public void Save(string slot, GraphRunSnapshot snapshot) => _json[slot] = JsonUtility.ToJson(snapshot);
            public GraphRunSnapshot Load(string slot)
                => _json.TryGetValue(slot, out var j) ? JsonUtility.FromJson<GraphRunSnapshot>(j) : null;
            public bool Exists(string slot) => _json.ContainsKey(slot);
            public void Delete(string slot) => _json.Remove(slot);
        }
    }
}
