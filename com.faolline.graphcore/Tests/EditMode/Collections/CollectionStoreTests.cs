using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// US1 — collection store: add/remove/contains/count/enumerate/clear with set semantics, an
    /// independent keyspace from scalars, snapshot reads, and null/empty guards.
    /// </summary>
    public class CollectionStoreTests
    {
        [Test]
        public void Add_IsIdempotent_NoDuplicates()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("items", "a");
            ctx.AddToCollection("items", "b");
            ctx.AddToCollection("items", "a");

            Assert.AreEqual(2, ctx.CollectionCount("items"));
            Assert.IsTrue(ctx.CollectionContains("items", "a"));
            Assert.IsTrue(ctx.CollectionContains("items", "b"));
        }

        [Test]
        public void Remove_DropsMembership()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("items", "a");
            ctx.RemoveFromCollection("items", "a");

            Assert.IsFalse(ctx.CollectionContains("items", "a"));
            Assert.AreEqual(0, ctx.CollectionCount("items"));
        }

        [Test]
        public void AbsentCollection_QueriesAreSafe()
        {
            var ctx = new BaseContext();
            Assert.IsFalse(ctx.CollectionContains("nope", "x"));
            Assert.AreEqual(0, ctx.CollectionCount("nope"));
            Assert.IsNotNull(ctx.GetCollection("nope"));
            Assert.AreEqual(0, ctx.GetCollection("nope").Count);
            Assert.DoesNotThrow(() => ctx.RemoveFromCollection("nope", "x"));
            Assert.DoesNotThrow(() => ctx.ClearCollection("nope"));
        }

        [Test]
        public void Clear_Empties()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("k", "a");
            ctx.AddToCollection("k", "b");
            ctx.ClearCollection("k");
            Assert.AreEqual(0, ctx.CollectionCount("k"));
        }

        [Test]
        public void Enumerate_YieldsMembers()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("k", "a");
            ctx.AddToCollection("k", "b");
            var members = new List<string>(ctx.GetCollection("k"));
            members.Sort();
            CollectionAssert.AreEqual(new[] { "a", "b" }, members);
        }

        [Test]
        public void IndependentKeyspace_ScalarAndCollectionShareKey()
        {
            var ctx = new BaseContext();
            ctx.Set<int>("x", 5);
            ctx.AddToCollection("x", "a");

            Assert.AreEqual(5, ctx.Get<int>("x"));
            Assert.IsTrue(ctx.CollectionContains("x", "a"));
            Assert.AreEqual(1, ctx.CollectionCount("x"));
        }

        [Test]
        public void GetCollection_SnapshotIsIndependent()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("k", "a");
            var snap = ctx.GetCollection("k");
            ctx.AddToCollection("k", "b");
            Assert.AreEqual(1, snap.Count, "A snapshot must not reflect later mutations.");
        }

        [Test]
        public void NullOrEmptyKey_NullItem_WarnAndNoOp()
        {
            var ctx = new BaseContext();
            LogAssert.Expect(LogType.Warning, "[GraphCore] AddToCollection called with a null/empty key or null item; ignored.");
            ctx.AddToCollection("", "a");
            LogAssert.Expect(LogType.Warning, "[GraphCore] AddToCollection called with a null/empty key or null item; ignored.");
            ctx.AddToCollection("k", null);
            Assert.AreEqual(0, ctx.CollectionCount("k"));
        }
    }
}
