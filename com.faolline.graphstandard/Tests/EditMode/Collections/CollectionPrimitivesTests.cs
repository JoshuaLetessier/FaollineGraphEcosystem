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
    /// <para>
    /// CollectionName/CollectionEntry.Key is a stable GUID assigned in OnEnable (not a name-fallback string),
    /// so two calls that must refer to the SAME collection/entry share the SAME asset instance — never two
    /// instances constructed with an equal label.
    /// </para>
    /// </summary>
    public class CollectionPrimitivesTests
    {
        private static CollectionName Col() => ScriptableObject.CreateInstance<CollectionName>();
        private static CollectionEntry Entry() => ScriptableObject.CreateInstance<CollectionEntry>();

        private static AddToCollectionAction Add(CollectionName col, CollectionEntry entry)
        {
            var a = ScriptableObject.CreateInstance<AddToCollectionAction>();
            a.Collection = col;
            a.Entry = entry;
            return a;
        }

        private static CollectionContainsCondition Contains(CollectionName col, CollectionEntry entry)
        {
            var c = ScriptableObject.CreateInstance<CollectionContainsCondition>();
            c.Collection = col;
            c.Entry = entry;
            return c;
        }

        private static CollectionCountAtLeastCondition CountAtLeast(CollectionName col, int threshold)
        {
            var c = ScriptableObject.CreateInstance<CollectionCountAtLeastCondition>();
            c.Collection = col;
            c.Threshold = threshold;
            return c;
        }

        // ── US1: record membership from a node ─────────────────────────────────

        [Test]
        public void AddToCollection_RecordsValue()
        {
            var ctx = new BaseContext();
            var col = Col(); var a = Entry();
            Add(col, a).Execute(ctx);
            Assert.IsTrue(ctx.CollectionContains(col.Key, a.Key));
        }

        [Test]
        public void AddToCollection_IsIdempotent()
        {
            var ctx = new BaseContext();
            var col = Col(); var a = Entry();
            var action = Add(col, a);
            action.Execute(ctx);
            action.Execute(ctx);
            Assert.AreEqual(1, ctx.CollectionCount(col.Key));
        }

        [Test]
        public void AddToCollection_NullCollectionOrEntry_IsNoOp()
        {
            // Key is always a non-empty GUID once the asset exists — the only way to hit the guard now
            // is an unassigned (null) Collection or Entry reference.
            var ctx = new BaseContext();
            var col = Col(); var a = Entry();
            Add(null, a).Execute(ctx);
            Add(col, null).Execute(ctx);
            Assert.AreEqual(0, ctx.CollectionCount(col.Key));
        }

        // ── US2: gate on collection state ──────────────────────────────────────

        [Test]
        public void Contains_TrueOnlyWhenPresent()
        {
            var ctx = new BaseContext();
            var col = Col(); var a = Entry(); var b = Entry();
            var cond = Contains(col, a);
            Assert.IsFalse(cond.Evaluate(ctx), "absent collection ⇒ false");
            ctx.AddToCollection(col.Key, a.Key);
            Assert.IsTrue(cond.Evaluate(ctx));
            Assert.IsFalse(Contains(col, b).Evaluate(ctx));
        }

        [Test]
        public void CountAtLeast_TrueWhenCountReachesThreshold()
        {
            var ctx = new BaseContext();
            var col = Col();
            var cond = CountAtLeast(col, 2);
            Assert.IsFalse(cond.Evaluate(ctx));
            ctx.AddToCollection(col.Key, "a");
            Assert.IsFalse(cond.Evaluate(ctx), "1 < 2");
            ctx.AddToCollection(col.Key, "b");
            Assert.IsTrue(cond.Evaluate(ctx), "2 >= 2");
            ctx.AddToCollection(col.Key, "c");
            Assert.IsTrue(cond.Evaluate(ctx), "3 >= 2");
        }

        [Test]
        public void CountAtLeast_ZeroThreshold_AlwaysTrue_EvenAbsent()
        {
            var ctx = new BaseContext();
            Assert.IsTrue(CountAtLeast(Col(), 0).Evaluate(ctx));
        }

        [Test]
        public void CountAtLeast_PositiveThreshold_FalseOnAbsentKey()
        {
            var ctx = new BaseContext();
            Assert.IsFalse(CountAtLeast(Col(), 1).Evaluate(ctx));
        }

        // ── US3: host a reactive progression on the shared context ─────────────

        [Test]
        public void ReactiveHostingPattern_ActionWrites_EvaluatorUnlocksAtThreshold()
        {
            // progression: p1,p2,p3 are prerequisites of "exit"; 2-of-3 unlocks it. p1/p3 are node ids
            // taken from CollectionEntry.Key itself (stable GUID) — the reactive evaluator matches
            // prerequisite completion by exact string equality, so the SAME identity must be used as the
            // node id AND as the entry the stock action writes.
            var p1 = Entry(); var p3 = Entry();
            var graph = ScriptableObject.CreateInstance<BaseGraph>();
            graph.AddNode(new StatementNodeData { Id = p1.Key, NodeType = StatementNodeData.NodeTypeId });
            graph.AddNode(new StatementNodeData { Id = "p2", NodeType = StatementNodeData.NodeTypeId });
            graph.AddNode(new StatementNodeData { Id = p3.Key, NodeType = StatementNodeData.NodeTypeId });
            graph.AddNode(new StatementNodeData { Id = "exit", NodeType = StatementNodeData.NodeTypeId });
            graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = p1.Key, ToNodeId = "exit" });
            graph.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "p2",   ToNodeId = "exit" });
            graph.AddEdge(new BaseEdgeData { Id = "e3", FromNodeId = p3.Key, ToNodeId = "exit" });

            var ctx = new BaseContext();
            var col = Col();
            var evaluator = new ReactiveEvaluator(graph, ctx, col.Key,
                new Dictionary<string, int> { ["exit"] = 2 });

            // The two-line bridge: a write into "completed" re-derives the progression.
            ctx.OnCollectionChanged(col.Key, _ => evaluator.Reevaluate());

            int exitAvailable = 0;
            evaluator.OnNodeAvailable += id => { if (id == "exit") exitAvailable++; };
            evaluator.Start();

            Assert.AreEqual(ReactiveNodeState.Locked, evaluator.GetState("exit"));

            // Linear flow records the first prerequisite via the stock action.
            Add(col, p1).Execute(ctx);
            Assert.AreEqual(ReactiveNodeState.Locked, evaluator.GetState("exit"), "1 of 2 — still locked");
            Assert.AreEqual(0, exitAvailable);

            // Second prerequisite crosses the threshold.
            Add(col, p3).Execute(ctx);
            Assert.AreEqual(ReactiveNodeState.Available, evaluator.GetState("exit"), "2 of 2 — unlocked");
            Assert.AreEqual(1, exitAvailable, "availability event raised exactly once");

            Object.DestroyImmediate(graph);
        }

        [Test]
        public void ReactiveHostingPattern_CountCondition_GatesLinearEdge()
        {
            // The same completed-set also gates a Linear edge directly.
            var ctx = new BaseContext();
            var col = Col();
            var gate = CountAtLeast(col, 2);
            Add(col, Entry()).Execute(ctx);
            Assert.IsFalse(gate.Evaluate(ctx));
            Add(col, Entry()).Execute(ctx);
            Assert.IsTrue(gate.Evaluate(ctx));
        }

        // ── US4: remove / clear collections from a node ───────────────────────

        private static RemoveFromCollectionAction Remove(CollectionName col, CollectionEntry entry)
        {
            var a = ScriptableObject.CreateInstance<RemoveFromCollectionAction>();
            a.Collection = col;
            a.Entry = entry;
            return a;
        }

        private static ClearCollectionAction Clear(CollectionName col)
        {
            var a = ScriptableObject.CreateInstance<ClearCollectionAction>();
            a.Collection = col;
            return a;
        }

        [Test]
        public void RemoveFromCollection_RemovesValue()
        {
            var ctx = new BaseContext();
            var col = Col(); var sword = Entry(); var shield = Entry();
            ctx.AddToCollection(col.Key, sword.Key);
            ctx.AddToCollection(col.Key, shield.Key);
            Remove(col, sword).Execute(ctx);
            Assert.IsFalse(ctx.CollectionContains(col.Key, sword.Key));
            Assert.IsTrue(ctx.CollectionContains(col.Key, shield.Key));
        }

        [Test]
        public void RemoveFromCollection_AbsentValue_IsNoOp()
        {
            var ctx = new BaseContext();
            var col = Col();
            Remove(col, Entry()).Execute(ctx);
            Assert.AreEqual(0, ctx.CollectionCount(col.Key));
        }

        [Test]
        public void RemoveFromCollection_NullCollectionOrEntry_IsNoOp()
        {
            var ctx = new BaseContext();
            var col = Col(); var sword = Entry();
            ctx.AddToCollection(col.Key, sword.Key);
            Remove(null, sword).Execute(ctx);
            Remove(col, null).Execute(ctx);
            Assert.IsTrue(ctx.CollectionContains(col.Key, sword.Key));
        }

        [Test]
        public void ClearCollection_EmptiesSet()
        {
            var ctx = new BaseContext();
            var col = Col();
            ctx.AddToCollection(col.Key, "a");
            ctx.AddToCollection(col.Key, "b");
            Clear(col).Execute(ctx);
            Assert.AreEqual(0, ctx.CollectionCount(col.Key));
        }

        [Test]
        public void ClearCollection_NullCollection_IsNoOp()
        {
            var ctx = new BaseContext();
            var col = Col();
            ctx.AddToCollection(col.Key, "a");
            Clear(null).Execute(ctx);
            Assert.AreEqual(1, ctx.CollectionCount(col.Key));
        }

        // ── US5: stacking (quantities) via the action's opt-in Stack toggle ────

        private static CollectionItemCountAtLeastCondition ItemCountAtLeast(
            CollectionName col, CollectionEntry entry, int threshold)
        {
            var c = ScriptableObject.CreateInstance<CollectionItemCountAtLeastCondition>();
            c.Collection = col;
            c.Entry = entry;
            c.Threshold = threshold;
            return c;
        }

        [Test]
        public void AddToCollectionAction_StackOff_IsPlainIdempotentAdd_MatchingPreExistingAssets()
        {
            // Stack defaults false — every asset authored before this option existed deserializes with
            // Stack == false, so behaviour must be byte-for-byte the classic idempotent Add.
            var ctx = new BaseContext();
            var col = Col(); var potion = Entry();
            var action = Add(col, potion);
            Assert.IsFalse(action.Stack, "Stack must default OFF for back-compat.");

            action.Execute(ctx);
            action.Execute(ctx);

            Assert.AreEqual(1, ctx.CollectionItemCount(col.Key, potion.Key));
        }

        [Test]
        public void AddToCollectionAction_StackOn_AddsQuantity()
        {
            var ctx = new BaseContext();
            var col = Col(); var potion = Entry();
            var action = Add(col, potion);
            action.Stack = true;
            action.Count = 3;

            action.Execute(ctx);
            action.Execute(ctx);

            Assert.AreEqual(6, ctx.CollectionItemCount(col.Key, potion.Key));
            Assert.AreEqual(1, ctx.CollectionCount(col.Key), "still one distinct entry");
        }

        [Test]
        public void RemoveFromCollectionAction_StackOff_RemovesWholeStack_MatchingPreExistingAssets()
        {
            var ctx = new BaseContext();
            var col = Col(); var arrow = Entry();
            ctx.AddToCollection(col.Key, arrow.Key, 99);

            var action = Remove(col, arrow);
            Assert.IsFalse(action.Stack, "Stack must default OFF for back-compat.");
            action.Execute(ctx);

            Assert.AreEqual(0, ctx.CollectionItemCount(col.Key, arrow.Key));
        }

        [Test]
        public void RemoveFromCollectionAction_StackOn_Decrements()
        {
            var ctx = new BaseContext();
            var col = Col(); var arrow = Entry();
            ctx.AddToCollection(col.Key, arrow.Key, 10);

            var action = Remove(col, arrow);
            action.Stack = true;
            action.Count = 4;
            action.Execute(ctx);

            Assert.AreEqual(6, ctx.CollectionItemCount(col.Key, arrow.Key));
        }

        [Test]
        public void ItemCountAtLeast_GatesOnQuantity_NotDistinctCount()
        {
            var ctx = new BaseContext();
            var col = Col(); var potion = Entry();
            var cond = ItemCountAtLeast(col, potion, 3);

            Assert.IsFalse(cond.Evaluate(ctx), "absent ⇒ false");
            ctx.AddToCollection(col.Key, potion.Key, 2);
            Assert.IsFalse(cond.Evaluate(ctx), "2 < 3");
            ctx.AddToCollection(col.Key, potion.Key, 1);
            Assert.IsTrue(cond.Evaluate(ctx), "3 >= 3");
        }

        [Test]
        public void ItemCountAtLeast_ZeroThreshold_AlwaysTrue_EvenAbsent()
        {
            var ctx = new BaseContext();
            Assert.IsTrue(ItemCountAtLeast(Col(), Entry(), 0).Evaluate(ctx));
        }

        [Test]
        public void ItemCountAtLeast_NullCollectionOrEntry_FallsBackToThresholdCheck()
        {
            var ctx = new BaseContext();
            Assert.IsTrue(ItemCountAtLeast(null, Entry(), 0).Evaluate(ctx));
            Assert.IsFalse(ItemCountAtLeast(null, Entry(), 1).Evaluate(ctx));
        }
    }
}
