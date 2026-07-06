using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// Stacking (0.31.0): the 2-arg AddToCollection/RemoveFromCollection stay pure membership (idempotent,
    /// covered by CollectionStoreTests/CollectionNotificationTests, untouched by this feature); the 3-arg
    /// count overloads add real quantity on top, and CollectionCount stays the DISTINCT item count.
    /// </summary>
    public class CollectionQuantityTests
    {
        [Test]
        public void Add_WithCount_AccumulatesQuantity()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("inv", "potion", 2);
            ctx.AddToCollection("inv", "potion", 1);

            Assert.AreEqual(3, ctx.CollectionItemCount("inv", "potion"));
            Assert.AreEqual(1, ctx.CollectionCount("inv"), "distinct count is unaffected by quantity");
            Assert.IsTrue(ctx.CollectionContains("inv", "potion"));
        }

        [Test]
        public void Add_WithCount_OnNewItem_CreatesAtThatQuantity()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("inv", "arrow", 20);
            Assert.AreEqual(20, ctx.CollectionItemCount("inv", "arrow"));
        }

        [Test]
        public void Add_PlainOverload_NeverAffectsQuantityOfAnExistingStack()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("inv", "potion", 5);
            ctx.AddToCollection("inv", "potion");   // 2-arg: idempotent ensure-present

            Assert.AreEqual(5, ctx.CollectionItemCount("inv", "potion"),
                "the plain overload must not reset or bump an existing quantity");
        }

        [Test]
        public void Remove_WithCount_Decrements_ThenDropsAtZero()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("inv", "arrow", 5);

            ctx.RemoveFromCollection("inv", "arrow", 2);
            Assert.AreEqual(3, ctx.CollectionItemCount("inv", "arrow"));

            ctx.RemoveFromCollection("inv", "arrow", 3);
            Assert.AreEqual(0, ctx.CollectionItemCount("inv", "arrow"));
            Assert.IsFalse(ctx.CollectionContains("inv", "arrow"));
            Assert.AreEqual(0, ctx.CollectionCount("inv"));
        }

        [Test]
        public void Remove_WithCount_ClampsAtZero_NeverGoesNegative()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("inv", "arrow", 2);
            ctx.RemoveFromCollection("inv", "arrow", 10);
            Assert.AreEqual(0, ctx.CollectionItemCount("inv", "arrow"));
        }

        [Test]
        public void Remove_PlainOverload_DropsTheWholeStack()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("inv", "arrow", 99);
            ctx.RemoveFromCollection("inv", "arrow");   // 2-arg: remove entirely, whatever the quantity
            Assert.AreEqual(0, ctx.CollectionItemCount("inv", "arrow"));
            Assert.IsFalse(ctx.CollectionContains("inv", "arrow"));
        }

        [Test]
        public void CollectionItemCount_AbsentItemOrCollection_IsZero()
        {
            var ctx = new BaseContext();
            Assert.AreEqual(0, ctx.CollectionItemCount("nope", "x"));
            ctx.AddToCollection("inv", "potion", 3);
            Assert.AreEqual(0, ctx.CollectionItemCount("inv", "missing"));
        }

        [Test]
        public void GetCollectionWithCounts_ReturnsPairsInInsertionOrder()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("inv", "sword");
            ctx.AddToCollection("inv", "potion", 3);
            ctx.AddToCollection("inv", "shield");

            var pairs = ctx.GetCollectionWithCounts("inv");
            Assert.AreEqual(3, pairs.Count);
            CollectionAssert.AreEqual(
                new[] { ("sword", 1), ("potion", 3), ("shield", 1) },
                pairs);
        }

        [Test]
        public void GetCollectionWithCounts_AbsentCollection_IsEmpty()
        {
            var ctx = new BaseContext();
            Assert.AreEqual(0, ctx.GetCollectionWithCounts("nope").Count);
        }

        // ── Notification rules for the count overloads ─────────────────────────

        [Test]
        public void Add_WithCount_AlwaysFires_EvenOnAnExistingItem()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("inv", "potion", 1);
            int hits = 0;
            ctx.OnCollectionChanged("inv", _ => hits++);

            ctx.AddToCollection("inv", "potion", 1);   // stacks onto the existing item — a real change
            ctx.AddToCollection("inv", "potion", 1);

            Assert.AreEqual(2, hits, "unlike the plain overload, stacking is never idempotent");
        }

        [Test]
        public void Remove_WithCount_FiresOnRealDecrement_SilentWhenAbsent()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("inv", "arrow", 5);
            int hits = 0;
            ctx.OnCollectionChanged("inv", _ => hits++);

            ctx.RemoveFromCollection("inv", "arrow", 2);   // fires
            ctx.RemoveFromCollection("inv", "missing", 1); // absent — silent

            Assert.AreEqual(1, hits);
        }

        [Test]
        public void Add_NonPositiveCount_WarnsAndIsNoOp()
        {
            var ctx = new BaseContext();
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("non-positive count"));
            ctx.AddToCollection("inv", "potion", 0);
            Assert.AreEqual(0, ctx.CollectionItemCount("inv", "potion"));
        }

        [Test]
        public void Remove_NonPositiveCount_WarnsAndIsNoOp()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("inv", "potion", 3);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("non-positive count"));
            ctx.RemoveFromCollection("inv", "potion", -1);
            Assert.AreEqual(3, ctx.CollectionItemCount("inv", "potion"));
        }

        // ── Durability: quantities survive DeepClone / GoBack ──────────────────

        [Test]
        public void DeepClone_PreservesQuantities_Independently()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("inv", "potion", 4);

            var clone = ctx.DeepClone();
            clone.AddToCollection("inv", "potion", 10);

            Assert.AreEqual(4, ctx.CollectionItemCount("inv", "potion"), "source untouched by clone mutation");
            Assert.AreEqual(14, clone.CollectionItemCount("inv", "potion"));
        }

        [Test]
        public void GoBack_RestoresQuantities()
        {
            var graph = ScriptableObject.CreateInstance<BaseGraph>();
            try
            {
                var start = new StartNodeData     { Id = "s", NodeType = StartNodeData.NodeTypeId };
                var mid   = new StatementNodeData { Id = "m", NodeType = StatementNodeData.NodeTypeId };
                var end   = new EndNodeData       { Id = "e", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };
                graph.AddNode(start); graph.AddNode(mid); graph.AddNode(end);
                graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "m" });
                graph.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "m", ToNodeId = "e" });
                graph.EntryNodeId = "s";

                var ctx = new BaseContext();
                var runner = new BaseRunner();
                runner.Start(graph, ctx, new NodeExecutorRegistry());
                ctx.AddToCollection("inv", "potion", 2);
                runner.Proceed();                          // snapshots quantity=2, advances s → m
                ctx.AddToCollection("inv", "potion", 5);    // live quantity=7
                Assert.AreEqual(7, ctx.CollectionItemCount("inv", "potion"));

                runner.GoBack();

                Assert.AreEqual(2, ctx.CollectionItemCount("inv", "potion"), "step-back restores the snapshot quantity");
            }
            finally { Object.DestroyImmediate(graph); }
        }
    }
}
