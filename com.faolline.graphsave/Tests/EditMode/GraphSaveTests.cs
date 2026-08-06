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
        public void Capture_And_ApplyTo_RoundTripsValueTypes_ThroughJson()
        {
            var ctx = new BaseContext();
            ctx.Set<Vector2>("v2", new Vector2(1f, 2f));
            ctx.Set<Vector3>("v3", new Vector3(3f, 4f, 5f));
            ctx.Set<Color>("col", new Color(0.1f, 0.2f, 0.3f, 0.4f));

            var snap = GraphRunSnapshot.Capture(ctx, "g", "n");
            var back = JsonUtility.FromJson<GraphRunSnapshot>(JsonUtility.ToJson(snap));

            var restored = new BaseContext();
            back.ApplyTo(restored);
            Assert.IsTrue(restored.TryGet<Vector2>("v2", out var v2) && v2 == new Vector2(1f, 2f));
            Assert.IsTrue(restored.TryGet<Vector3>("v3", out var v3) && v3 == new Vector3(3f, 4f, 5f));
            Assert.IsTrue(restored.TryGet<Color>("col", out var c) && c == new Color(0.1f, 0.2f, 0.3f, 0.4f));
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

        // ── Edge cases ───────────────────────────────────────────────────────

        [Test]
        public void Capture_EmptyContext_ProducesEmptySnapshot()
        {
            var snap = GraphRunSnapshot.Capture(new BaseContext(), "g", "n");
            Assert.AreEqual(0, snap.Variables.Count);
            Assert.AreEqual(0, snap.Collections.Count);

            var ctx = new BaseContext();
            snap.ApplyTo(ctx);
            Assert.AreEqual(0, ctx.GetAllVariables().Count);
            Assert.AreEqual(0, ctx.GetAllCollections().Count);
        }

        [Test]
        public void Capture_NullContext_ProducesEmptySnapshot()
        {
            var snap = GraphRunSnapshot.Capture((BaseContext)null, "g", "n");
            Assert.AreEqual("g", snap.GraphId);
            Assert.AreEqual("n", snap.CurrentNodeId);
            Assert.AreEqual(0, snap.Variables.Count);
            Assert.AreEqual(0, snap.Collections.Count);
        }

        [Test]
        public void Capture_FromRunner_ReadsGraphIdAndNodeId()
        {
            var graph = ScriptableObject.CreateInstance<BaseGraph>();
            var start = new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId };
            graph.AddNode(start);
            graph.EntryNodeId = "s";
            try
            {
                var runner = new BaseRunner();
                var ctx = new BaseContext();
                ctx.Set<int>("v", 7);
                runner.Start(graph, ctx, new NodeExecutorRegistry());

                var snap = GraphRunSnapshot.Capture(runner, ctx);
                Assert.AreEqual(graph.GraphId, snap.GraphId);
                Assert.AreEqual("s", snap.CurrentNodeId);
                Assert.AreEqual(1, snap.Variables.Count);
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void Capture_FromNullRunner_ProducesNullIds()
        {
            var ctx = new BaseContext();
            var snap = GraphRunSnapshot.Capture((BaseRunner)null, ctx);
            Assert.IsNull(snap.GraphId);
            Assert.IsNull(snap.CurrentNodeId);
        }

        [Test]
        public void ApplyTo_NullContext_DoesNotThrow()
        {
            var snap = new GraphRunSnapshot();
            snap.Variables.Add(new GraphRunSnapshot.Param { Key = "k", Type = "int", Value = "1" });
            Assert.DoesNotThrow(() => snap.ApplyTo(null));
        }

        [Test]
        public void ApplyTo_MalformedFloat_SkipsGracefully()
        {
            var snap = new GraphRunSnapshot();
            snap.Variables.Add(new GraphRunSnapshot.Param { Key = "bad", Type = "float", Value = "not_a_number" });
            snap.Variables.Add(new GraphRunSnapshot.Param { Key = "ok", Type = "int", Value = "5" });

            var ctx = new BaseContext();
            snap.ApplyTo(ctx);
            Assert.IsFalse(ctx.TryGet<float>("bad", out _), "unparseable float is silently skipped.");
            Assert.IsTrue(ctx.TryGet<int>("ok", out var v) && v == 5, "valid params still applied.");
        }

        [Test]
        public void ApplyTo_MalformedVector_SkipsGracefully()
        {
            var snap = new GraphRunSnapshot();
            snap.Variables.Add(new GraphRunSnapshot.Param { Key = "v2_bad", Type = "vector2", Value = "1,2,3" });
            snap.Variables.Add(new GraphRunSnapshot.Param { Key = "v3_bad", Type = "vector3", Value = "nope" });
            snap.Variables.Add(new GraphRunSnapshot.Param { Key = "col_bad", Type = "color", Value = "" });

            var ctx = new BaseContext();
            snap.ApplyTo(ctx);
            Assert.IsFalse(ctx.TryGet<Vector2>("v2_bad", out _), "wrong component count skipped.");
            Assert.IsFalse(ctx.TryGet<Vector3>("v3_bad", out _), "non-numeric components skipped.");
            Assert.IsFalse(ctx.TryGet<Color>("col_bad", out _), "empty value skipped.");
        }

        [Test]
        public void ApplyTo_NullParam_SkipsGracefully()
        {
            var snap = new GraphRunSnapshot();
            snap.Variables.Add(null);
            snap.Variables.Add(new GraphRunSnapshot.Param { Key = "", Type = "int", Value = "1" });
            snap.Variables.Add(new GraphRunSnapshot.Param { Key = "ok", Type = "int", Value = "2" });

            var ctx = new BaseContext();
            Assert.DoesNotThrow(() => snap.ApplyTo(ctx));
            Assert.IsTrue(ctx.TryGet<int>("ok", out var v) && v == 2);
        }

        [Test]
        public void ApplyTo_NullCollection_SkipsGracefully()
        {
            var snap = new GraphRunSnapshot();
            snap.Collections.Add(null);
            snap.Collections.Add(new GraphRunSnapshot.Collection { Key = "ok", Items = { "a" } });

            var ctx = new BaseContext();
            Assert.DoesNotThrow(() => snap.ApplyTo(ctx));
            Assert.IsTrue(ctx.CollectionContains("ok", "a"));
        }

        [Test]
        public void MultipleCollections_RoundTrip()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("quests", "q1");
            ctx.AddToCollection("quests", "q2");
            ctx.AddToCollection("inventory", "sword");

            var snap = GraphRunSnapshot.Capture(ctx);
            var json = JsonUtility.ToJson(snap);
            var back = JsonUtility.FromJson<GraphRunSnapshot>(json);

            var restored = new BaseContext();
            back.ApplyTo(restored);
            Assert.IsTrue(restored.CollectionContains("quests", "q1"));
            Assert.IsTrue(restored.CollectionContains("quests", "q2"));
            Assert.IsTrue(restored.CollectionContains("inventory", "sword"));
            Assert.AreEqual(2, restored.CollectionCount("quests"));
            Assert.AreEqual(1, restored.CollectionCount("inventory"));
        }

        [Test]
        public void UnknownType_FallsBackToString()
        {
            var snap = new GraphRunSnapshot();
            snap.Variables.Add(new GraphRunSnapshot.Param { Key = "custom", Type = "widget", Value = "hello" });

            var ctx = new BaseContext();
            snap.ApplyTo(ctx);
            Assert.IsTrue(ctx.TryGet<string>("custom", out var v) && v == "hello");
        }

        [Test]
        public void Restore_NullArgs_DoesNotThrow()
        {
            var snap = new GraphRunSnapshot { CurrentNodeId = "n" };
            Assert.DoesNotThrow(() => snap.Restore(null, null, null));
            Assert.DoesNotThrow(() => snap.Restore(new BaseRunner(), null, new BaseContext()));
        }

        [Test]
        public void Restore_MissingNodeId_FallsBackToEntry()
        {
            var graph = ScriptableObject.CreateInstance<BaseGraph>();
            var start = new StartNodeData { Id = "entry", NodeType = StartNodeData.NodeTypeId };
            graph.AddNode(start);
            graph.EntryNodeId = "entry";
            try
            {
                var snap = new GraphRunSnapshot { GraphId = graph.GraphId, CurrentNodeId = "" };
                snap.Variables.Add(new GraphRunSnapshot.Param { Key = "x", Type = "int", Value = "3" });

                var runner = new BaseRunner();
                var ctx = new BaseContext();
                snap.Restore(runner, graph, ctx);

                Assert.AreEqual("entry", runner.CurrentNode?.Id, "falls back to entry node.");
                Assert.IsTrue(ctx.TryGet<int>("x", out var x) && x == 3);
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void Store_Overwrite_ReplacesSlot()
        {
            var store = new MemoryStore();
            var ctx1 = new BaseContext();
            ctx1.Set<int>("v", 1);
            store.Save("s", GraphRunSnapshot.Capture(ctx1));

            var ctx2 = new BaseContext();
            ctx2.Set<int>("v", 99);
            store.Save("s", GraphRunSnapshot.Capture(ctx2));

            var restored = new BaseContext();
            store.Load("s").ApplyTo(restored);
            Assert.IsTrue(restored.TryGet<int>("v", out var v) && v == 99, "overwrite replaces the slot.");
        }

        [Test]
        public void Store_MultipleSlots_AreIndependent()
        {
            var store = new MemoryStore();
            var c1 = new BaseContext(); c1.Set<int>("v", 1);
            var c2 = new BaseContext(); c2.Set<int>("v", 2);
            store.Save("a", GraphRunSnapshot.Capture(c1));
            store.Save("b", GraphRunSnapshot.Capture(c2));

            store.Delete("a");
            Assert.IsFalse(store.Exists("a"));
            Assert.IsTrue(store.Exists("b"));

            var r = new BaseContext();
            store.Load("b").ApplyTo(r);
            Assert.IsTrue(r.TryGet<int>("v", out var v) && v == 2);
        }

        [Test]
        public void FullPipeline_Capture_Serialize_Deserialize_Restore()
        {
            var graph = ScriptableObject.CreateInstance<BaseGraph>();
            var start = new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId };
            var mid = new StatementNodeData { Id = "m", NodeType = StatementNodeData.NodeTypeId };
            graph.AddNode(start); graph.AddNode(mid);
            graph.AddEdge(new BaseEdgeData { FromNodeId = "s", ToNodeId = "m" });
            graph.EntryNodeId = "s";
            try
            {
                var ctx = new BaseContext();
                ctx.Set<int>("score", 42);
                ctx.Set<float>("hp", 0.75f);
                ctx.Set<bool>("boss_defeated", true);
                ctx.Set<string>("zone", "dungeon");
                ctx.Set<Vector3>("pos", new Vector3(1f, 2f, 3f));
                ctx.AddToCollection("keys", "red");
                ctx.AddToCollection("keys", "blue");

                var runner = new BaseRunner();
                runner.Start(graph, ctx, new NodeExecutorRegistry());
                runner.Proceed();

                var snap = GraphRunSnapshot.Capture(runner, ctx);
                var store = new MemoryStore();
                store.Save("save1", snap);

                var loaded = store.Load("save1");
                var runner2 = new BaseRunner();
                var ctx2 = new BaseContext();
                loaded.Restore(runner2, graph, ctx2);

                Assert.AreEqual("m", runner2.CurrentNode?.Id);
                Assert.AreEqual(42, ctx2.Get<int>("score"));
                Assert.That(ctx2.Get<float>("hp"), Is.EqualTo(0.75f).Within(0.001f));
                Assert.IsTrue(ctx2.Get<bool>("boss_defeated"));
                Assert.AreEqual("dungeon", ctx2.Get<string>("zone"));
                Assert.AreEqual(new Vector3(1f, 2f, 3f), ctx2.Get<Vector3>("pos"));
                Assert.IsTrue(ctx2.CollectionContains("keys", "red"));
                Assert.IsTrue(ctx2.CollectionContains("keys", "blue"));
                Assert.AreEqual(2, ctx2.CollectionCount("keys"));
            }
            finally { Object.DestroyImmediate(graph); }
        }

        // ── Quantities (0.6.0) ─────────────────────────────────────────────────

        [Test]
        public void Capture_And_ApplyTo_RoundTripsQuantities()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("inv", "sword");          // quantity 1
            ctx.AddToCollection("inv", "potion", 5);      // quantity 5

            var snap = GraphRunSnapshot.Capture(ctx);
            var restored = new BaseContext();
            snap.ApplyTo(restored);

            Assert.AreEqual(1, restored.CollectionItemCount("inv", "sword"));
            Assert.AreEqual(5, restored.CollectionItemCount("inv", "potion"));
            Assert.AreEqual(2, restored.CollectionCount("inv"), "distinct count unaffected by quantity");
        }

        [Test]
        public void JsonUtility_RoundTrip_PreservesQuantities()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("inv", "arrow", 20);

            var snap = GraphRunSnapshot.Capture(ctx);
            var json = JsonUtility.ToJson(snap);
            var back = JsonUtility.FromJson<GraphRunSnapshot>(json);

            var ctx2 = new BaseContext();
            back.ApplyTo(ctx2);
            Assert.AreEqual(20, ctx2.CollectionItemCount("inv", "arrow"));
        }

        [Test]
        public void ApplyTo_OldSnapshotWithoutCounts_TreatsEveryItemAsQuantityOne()
        {
            // Simulates a save file written before Counts existed: the field is present (JsonUtility default)
            // but empty — every item must resolve to quantity 1, exactly the pre-0.6.0 behavior.
            var snap = new GraphRunSnapshot();
            snap.Collections.Add(new GraphRunSnapshot.Collection { Key = "inv", Items = { "sword", "potion" } });
            Assert.AreEqual(0, snap.Collections[0].Counts.Count, "simulates a pre-0.6.0 snapshot with no Counts data");

            var ctx = new BaseContext();
            snap.ApplyTo(ctx);

            Assert.AreEqual(1, ctx.CollectionItemCount("inv", "sword"));
            Assert.AreEqual(1, ctx.CollectionItemCount("inv", "potion"));
        }

        [Test]
        public void ApplyTo_ReplaceCollections_QuantityIsAuthoritative()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("inv", "potion", 99);   // stale pre-existing quantity

            var snap = new GraphRunSnapshot();
            snap.Collections.Add(new GraphRunSnapshot.Collection
            {
                Key = "inv", Items = { "potion" }, Counts = { 3 }
            });

            snap.ApplyTo(ctx, replaceCollections: true);

            Assert.AreEqual(3, ctx.CollectionItemCount("inv", "potion"), "replace makes the snapshot's quantity authoritative");
        }

        [Test]
        public void ApplyTo_MergeMode_QuantityStacksOnRepeatedApply_DocumentedCaveat()
        {
            // Documents the caveat in ApplyTo's XML doc: merge mode (replaceCollections: false) uses the
            // additive stacking overload for quantities, so re-applying the SAME snapshot twice doubles it.
            // Restore() and the one real consumer (GraphFlowDriver) always use replaceCollections: true,
            // which does not have this property (each call starts from a cleared collection).
            var snap = new GraphRunSnapshot();
            snap.Collections.Add(new GraphRunSnapshot.Collection { Key = "inv", Items = { "potion" }, Counts = { 2 } });

            var ctx = new BaseContext();
            snap.ApplyTo(ctx);
            snap.ApplyTo(ctx);

            Assert.AreEqual(4, ctx.CollectionItemCount("inv", "potion"), "merge mode stacks quantities on repeat apply — documented, not a bug");
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
            public IEnumerable<string> GetAllKeys() => _json.Keys;
            public void DeleteAll() => _json.Clear();
        }
    }
}
