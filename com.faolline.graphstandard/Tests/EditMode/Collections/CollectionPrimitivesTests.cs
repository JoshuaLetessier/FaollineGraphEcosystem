using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphStandard.Tests
{
    /// <summary>
    /// Slice 6 — the universal collection primitives (AddToCollectionAction, CollectionContainsCondition,
    /// CollectionCountAtLeastCondition) and the reactive-hosting pattern they complete (a Linear flow records
    /// completion → a ReactiveEvaluator over the SAME context derives k-of-N unlocks, bridged by
    /// OnCollectionChanged → Reevaluate).
    /// </summary>
    public class CollectionPrimitivesTests
    {
        private static AddToCollectionAction Add(string key, string value)
        {
            var a = ScriptableObject.CreateInstance<AddToCollectionAction>();
            a.CollectionKey = key;
            a.Value = value;
            return a;
        }

        private static CollectionContainsCondition Contains(string key, string value)
        {
            var c = ScriptableObject.CreateInstance<CollectionContainsCondition>();
            c.CollectionKey = key;
            c.Value = value;
            return c;
        }

        private static CollectionCountAtLeastCondition CountAtLeast(string key, int threshold)
        {
            var c = ScriptableObject.CreateInstance<CollectionCountAtLeastCondition>();
            c.CollectionKey = key;
            c.Threshold = threshold;
            return c;
        }

        // ── US1: record membership from a node ─────────────────────────────────

        [Test]
        public void AddToCollection_RecordsValue()
        {
            var ctx = new BaseContext();
            Add("completed", "a").Execute(ctx);
            Assert.IsTrue(ctx.CollectionContains("completed", "a"));
        }

        [Test]
        public void AddToCollection_IsIdempotent()
        {
            var ctx = new BaseContext();
            var action = Add("completed", "a");
            action.Execute(ctx);
            action.Execute(ctx);
            Assert.AreEqual(1, ctx.CollectionCount("completed"));
        }

        [Test]
        public void AddToCollection_EmptyKeyOrValue_IsNoOp()
        {
            var ctx = new BaseContext();
            Add("", "a").Execute(ctx);
            Add("completed", "").Execute(ctx);
            Add("   ", "a").Execute(ctx);
            Assert.AreEqual(0, ctx.CollectionCount("completed"));
            Assert.AreEqual(0, ctx.CollectionCount(""));
        }

        // ── US2: gate on collection state ──────────────────────────────────────

        [Test]
        public void Contains_TrueOnlyWhenPresent()
        {
            var ctx = new BaseContext();
            var cond = Contains("completed", "a");
            Assert.IsFalse(cond.Evaluate(ctx), "absent collection ⇒ false");
            ctx.AddToCollection("completed", "a");
            Assert.IsTrue(cond.Evaluate(ctx));
            Assert.IsFalse(Contains("completed", "b").Evaluate(ctx));
        }

        [Test]
        public void CountAtLeast_TrueWhenCountReachesThreshold()
        {
            var ctx = new BaseContext();
            var cond = CountAtLeast("completed", 2);
            Assert.IsFalse(cond.Evaluate(ctx));
            ctx.AddToCollection("completed", "a");
            Assert.IsFalse(cond.Evaluate(ctx), "1 < 2");
            ctx.AddToCollection("completed", "b");
            Assert.IsTrue(cond.Evaluate(ctx), "2 >= 2");
            ctx.AddToCollection("completed", "c");
            Assert.IsTrue(cond.Evaluate(ctx), "3 >= 2");
        }

        [Test]
        public void CountAtLeast_ZeroThreshold_AlwaysTrue_EvenAbsent()
        {
            var ctx = new BaseContext();
            Assert.IsTrue(CountAtLeast("never-touched", 0).Evaluate(ctx));
        }

        [Test]
        public void CountAtLeast_PositiveThreshold_FalseOnAbsentKey()
        {
            var ctx = new BaseContext();
            Assert.IsFalse(CountAtLeast("never-touched", 1).Evaluate(ctx));
        }

        // ── US3: host a reactive progression on the shared context ─────────────

        [Test]
        public void ReactiveHostingPattern_ActionWrites_EvaluatorUnlocksAtThreshold()
        {
            // progression: p1,p2,p3 are prerequisites of "exit"; 2-of-3 unlocks it.
            var graph = ScriptableObject.CreateInstance<BaseGraph>();
            foreach (var id in new[] { "p1", "p2", "p3", "exit" })
                graph.AddNode(new StatementNodeData { Id = id, NodeType = StatementNodeData.NodeTypeId });
            graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "p1", ToNodeId = "exit" });
            graph.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "p2", ToNodeId = "exit" });
            graph.AddEdge(new BaseEdgeData { Id = "e3", FromNodeId = "p3", ToNodeId = "exit" });

            var ctx = new BaseContext();
            var evaluator = new ReactiveEvaluator(graph, ctx, "completed",
                new Dictionary<string, int> { ["exit"] = 2 });

            // The two-line bridge: a write into "completed" re-derives the progression.
            ctx.OnCollectionChanged("completed", _ => evaluator.Reevaluate());

            int exitAvailable = 0;
            evaluator.OnNodeAvailable += id => { if (id == "exit") exitAvailable++; };
            evaluator.Start();

            Assert.AreEqual(ReactiveNodeState.Locked, evaluator.GetState("exit"));

            // Linear flow records the first prerequisite via the stock action.
            Add("completed", "p1").Execute(ctx);
            Assert.AreEqual(ReactiveNodeState.Locked, evaluator.GetState("exit"), "1 of 2 — still locked");
            Assert.AreEqual(0, exitAvailable);

            // Second prerequisite crosses the threshold.
            Add("completed", "p3").Execute(ctx);
            Assert.AreEqual(ReactiveNodeState.Available, evaluator.GetState("exit"), "2 of 2 — unlocked");
            Assert.AreEqual(1, exitAvailable, "availability event raised exactly once");

            Object.DestroyImmediate(graph);
        }

        [Test]
        public void ReactiveHostingPattern_CountCondition_GatesLinearEdge()
        {
            // The same completed-set also gates a Linear edge directly.
            var ctx = new BaseContext();
            var gate = CountAtLeast("completed", 2);
            Add("completed", "p1").Execute(ctx);
            Assert.IsFalse(gate.Evaluate(ctx));
            Add("completed", "p2").Execute(ctx);
            Assert.IsTrue(gate.Evaluate(ctx));
        }

        // ── US4: remove / clear collections from a node ───────────────────────

        private static RemoveFromCollectionAction Remove(string key, string value)
        {
            var a = ScriptableObject.CreateInstance<RemoveFromCollectionAction>();
            a.CollectionKey = key;
            a.Value = value;
            return a;
        }

        private static ClearCollectionAction Clear(string key)
        {
            var a = ScriptableObject.CreateInstance<ClearCollectionAction>();
            a.CollectionKey = key;
            return a;
        }

        [Test]
        public void RemoveFromCollection_RemovesValue()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("inv", "sword");
            ctx.AddToCollection("inv", "shield");
            Remove("inv", "sword").Execute(ctx);
            Assert.IsFalse(ctx.CollectionContains("inv", "sword"));
            Assert.IsTrue(ctx.CollectionContains("inv", "shield"));
        }

        [Test]
        public void RemoveFromCollection_AbsentValue_IsNoOp()
        {
            var ctx = new BaseContext();
            Remove("inv", "missing").Execute(ctx);
            Assert.AreEqual(0, ctx.CollectionCount("inv"));
        }

        [Test]
        public void RemoveFromCollection_EmptyKeyOrValue_IsNoOp()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("inv", "sword");
            Remove("", "sword").Execute(ctx);
            Remove("inv", "").Execute(ctx);
            Assert.IsTrue(ctx.CollectionContains("inv", "sword"));
        }

        [Test]
        public void ClearCollection_EmptiesSet()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("inv", "a");
            ctx.AddToCollection("inv", "b");
            Clear("inv").Execute(ctx);
            Assert.AreEqual(0, ctx.CollectionCount("inv"));
        }

        [Test]
        public void ClearCollection_EmptyKey_IsNoOp()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("inv", "a");
            Clear("").Execute(ctx);
            Assert.AreEqual(1, ctx.CollectionCount("inv"));
        }
    }
}
