using System;
using NUnit.Framework;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// US3 — change notifications: fire exactly once per real membership change (new add / present-remove /
    /// non-empty clear), silent on idempotent/no-op operations, re-entrant safe, unsubscribe honored.
    /// </summary>
    public class CollectionNotificationTests
    {
        [Test]
        public void Add_NewElement_FiresOnce_WithKey()
        {
            var ctx = new BaseContext();
            int hits = 0; string got = null;
            ctx.OnCollectionChanged("k", key => { hits++; got = key; });

            ctx.AddToCollection("k", "a");

            Assert.AreEqual(1, hits);
            Assert.AreEqual("k", got);
        }

        [Test]
        public void Add_Idempotent_IsSilent()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("k", "a");
            int hits = 0;
            ctx.OnCollectionChanged("k", _ => hits++);

            ctx.AddToCollection("k", "a");   // already present

            Assert.AreEqual(0, hits);
        }

        [Test]
        public void Remove_Present_Fires_Absent_Silent()
        {
            var ctx = new BaseContext();
            ctx.AddToCollection("k", "a");
            int hits = 0;
            ctx.OnCollectionChanged("k", _ => hits++);

            ctx.RemoveFromCollection("k", "a");   // fires
            ctx.RemoveFromCollection("k", "a");   // silent (absent)

            Assert.AreEqual(1, hits);
        }

        [Test]
        public void Clear_NonEmpty_Fires_EmptyOrAbsent_Silent()
        {
            var ctx = new BaseContext();
            int hits = 0;
            ctx.OnCollectionChanged("k", _ => hits++);

            ctx.ClearCollection("k");        // absent → silent
            ctx.AddToCollection("k", "a");   // fires (add)
            ctx.ClearCollection("k");        // non-empty → fires

            Assert.AreEqual(2, hits);
        }

        [Test]
        public void Off_StopsDelivery()
        {
            var ctx = new BaseContext();
            int hits = 0;
            Action<string> h = _ => hits++;
            ctx.OnCollectionChanged("k", h);
            ctx.AddToCollection("k", "a");
            ctx.OffCollectionChanged("k", h);
            ctx.AddToCollection("k", "b");

            Assert.AreEqual(1, hits);
        }

        [Test]
        public void ReentrantUnsubscribe_IsSafe()
        {
            var ctx = new BaseContext();
            int hits = 0;
            Action<string> self = null;
            self = _ => { hits++; ctx.OffCollectionChanged("k", self); };
            ctx.OnCollectionChanged("k", self);
            ctx.OnCollectionChanged("k", _ => hits++);

            Assert.DoesNotThrow(() => ctx.AddToCollection("k", "a"));
            Assert.AreEqual(2, hits, "Both subscribers fire over a stable snapshot.");
        }
    }
}
