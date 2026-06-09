using NUnit.Framework;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// US2 (context layer) — durability: DeepClone produces independent copies, GetAllCollections snapshots
    /// all collections, and GetAllParameters stays scalar-only (collections excluded).
    /// </summary>
    public class CollectionDurabilityTests
    {
        [Test]
        public void DeepClone_ProducesIndependentCopies()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("k", "a");

            var clone = ctx.DeepClone();
            clone.AddToCollection("k", "b");

            Assert.IsTrue(clone.CollectionContains("k", "a"), "Clone keeps the original members.");
            Assert.IsFalse(ctx.CollectionContains("k", "b"), "Clone mutation must not affect the source.");
        }

        [Test]
        public void GetAllCollections_SnapshotsAll()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("a", "1");
            ctx.AddToCollection("a", "2");
            ctx.AddToCollection("b", "x");

            var all = ctx.GetAllCollections();
            Assert.IsTrue(all.ContainsKey("a"));
            Assert.IsTrue(all.ContainsKey("b"));
            Assert.AreEqual(2, all["a"].Count);
            Assert.AreEqual(1, all["b"].Count);
        }

        [Test]
        public void GetAllParameters_ExcludesCollections()
        {
            var ctx = new BaseContext();
            ctx.Set<int>("gold", 5);
            ctx.AddToCollection("items", "a");

            var scalars = ctx.GetAllParameters();
            Assert.IsTrue(scalars.ContainsKey("gold"));
            Assert.IsFalse(scalars.ContainsKey("items"), "Collections must not leak into the scalar snapshot.");
        }

        [Test]
        public void GetAllCollections_IsReadOnlySnapshot()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("k", "a");
            var snap = ctx.GetAllCollections();
            ctx.AddToCollection("k", "b");
            Assert.AreEqual(1, snap["k"].Count, "Snapshot must not reflect later mutations.");
        }
    }
}
