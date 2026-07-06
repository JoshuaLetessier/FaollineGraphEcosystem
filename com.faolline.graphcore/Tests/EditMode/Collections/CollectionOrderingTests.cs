using System.Collections.Generic;
using NUnit.Framework;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// GetCollection/GetAllCollections yield distinct items in INSERTION order (0.31.0) — previously an
    /// arbitrary HashSet order. No existing consumer depended on the old order (CollectionStoreTests'
    /// Enumerate_YieldsMembers sorts before comparing), so this is additive, not a behavior break.
    /// </summary>
    public class CollectionOrderingTests
    {
        [Test]
        public void GetCollection_YieldsInsertionOrder()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("quest_log", "intro");
            ctx.AddToCollection("quest_log", "meet_elder");
            ctx.AddToCollection("quest_log", "find_sword");

            CollectionAssert.AreEqual(
                new[] { "intro", "meet_elder", "find_sword" },
                ctx.GetCollection("quest_log"));
        }

        [Test]
        public void GetCollection_ReAdd_DoesNotMoveItem()
        {
            // The item already occupies its original slot — re-adding (idempotent) must not reorder it.
            var ctx = new BaseContext();
            ctx.AddToCollection("k", "a");
            ctx.AddToCollection("k", "b");
            ctx.AddToCollection("k", "a");   // idempotent re-add

            CollectionAssert.AreEqual(new[] { "a", "b" }, ctx.GetCollection("k"));
        }

        [Test]
        public void GetCollection_RemoveThenReAdd_MovesItemToTheEnd()
        {
            // Once removed and re-added, the item is a genuinely new insertion — it belongs at the end.
            var ctx = new BaseContext();
            ctx.AddToCollection("k", "a");
            ctx.AddToCollection("k", "b");
            ctx.RemoveFromCollection("k", "a");
            ctx.AddToCollection("k", "a");

            CollectionAssert.AreEqual(new[] { "b", "a" }, ctx.GetCollection("k"));
        }

        [Test]
        public void GetAllCollections_PreservesPerKeyInsertionOrder()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("log", "third");
            ctx.AddToCollection("log", "first");
            ctx.AddToCollection("log", "second");
            // (names don't imply order — only call sequence does; this proves the stored order is
            // call-sequence-based, not alphabetical or otherwise coincidental)

            var all = ctx.GetAllCollections();
            CollectionAssert.AreEqual(new[] { "third", "first", "second" }, all["log"]);
        }

        [Test]
        public void DeepClone_PreservesInsertionOrder()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("k", "z");
            ctx.AddToCollection("k", "a");
            ctx.AddToCollection("k", "m");

            var clone = ctx.DeepClone();
            CollectionAssert.AreEqual(new[] { "z", "a", "m" }, clone.GetCollection("k"));
        }
    }
}
