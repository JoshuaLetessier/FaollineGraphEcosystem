using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// US1 — BaseContext signal channel: pub/sub delivery, broadcast, no-op on no subscriber,
    /// no-payload distinction, re-entrant safety, naming guards, payload-type validation.
    /// </summary>
    public class SignalChannelTests
    {
        [Test]
        public void RaiseSignal_DeliversPayload_ToSubscriber()
        {
            var ctx = new BaseContext();
            SignalArgs received = default;
            int hits = 0;
            ctx.OnSignal("itemCollected", a => { received = a; hits++; });

            ctx.RaiseSignal<string>("itemCollected", "key");

            Assert.AreEqual(1, hits);
            Assert.AreEqual("itemCollected", received.Name);
            Assert.IsTrue(received.HasPayload);
            Assert.AreEqual("key", received.GetPayload<string>());
        }

        [Test]
        public void RaiseSignal_NoSubscriber_IsNoOp_AndStoresLast()
        {
            var ctx = new BaseContext();
            Assert.DoesNotThrow(() => ctx.RaiseSignal("nobodyListening"));
            Assert.IsTrue(ctx.TryGetLastSignal("nobodyListening", out var a));
            Assert.IsFalse(a.HasPayload);
        }

        [Test]
        public void RaiseSignal_Broadcasts_ToAllSubscribers()
        {
            var ctx = new BaseContext();
            int a = 0, b = 0, c = 0;
            ctx.OnSignal("ping", _ => a++);
            ctx.OnSignal("ping", _ => b++);
            ctx.OnSignal("ping", _ => c++);

            ctx.RaiseSignal("ping");

            Assert.AreEqual(1, a);
            Assert.AreEqual(1, b);
            Assert.AreEqual(1, c);
        }

        [Test]
        public void RaiseSignal_NoPayload_HasPayloadFalse_PayloadBoxedNull()
        {
            var ctx = new BaseContext();
            SignalArgs received = default;
            ctx.OnSignal("ev", a => received = a);

            ctx.RaiseSignal("ev");

            Assert.IsFalse(received.HasPayload);
            Assert.IsNull(received.PayloadBoxed);
        }

        [Test]
        public void GetPayload_WhenNoPayload_Throws()
        {
            var ctx = new BaseContext();
            SignalArgs received = default;
            ctx.OnSignal("ev", a => received = a);
            ctx.RaiseSignal("ev");

            Assert.Throws<InvalidOperationException>(() => received.GetPayload<int>());
        }

        [Test]
        public void GetPayload_WrongType_ThrowsInvalidCast()
        {
            var ctx = new BaseContext();
            SignalArgs received = default;
            ctx.OnSignal("ev", a => received = a);
            ctx.RaiseSignal<int>("ev", 5);

            Assert.Throws<InvalidCastException>(() => received.GetPayload<string>());
        }

        [Test]
        public void OffSignal_StopsDelivery()
        {
            var ctx = new BaseContext();
            int hits = 0;
            Action<SignalArgs> h = _ => hits++;
            ctx.OnSignal("ev", h);
            ctx.RaiseSignal("ev");
            ctx.OffSignal("ev", h);
            ctx.RaiseSignal("ev");

            Assert.AreEqual(1, hits);
        }

        [Test]
        public void RaiseSignal_ReentrantUnsubscribe_DoesNotCorruptIteration()
        {
            var ctx = new BaseContext();
            int hits = 0;
            Action<SignalArgs> self = null;
            self = _ => { hits++; ctx.OffSignal("ev", self); };   // unsubscribe during delivery
            ctx.OnSignal("ev", self);
            ctx.OnSignal("ev", _ => hits++);

            Assert.DoesNotThrow(() => ctx.RaiseSignal("ev"));
            Assert.AreEqual(2, hits, "Both subscribers fire over a stable snapshot.");
        }

        [Test]
        public void RaiseSignalT_UnsupportedType_Throws()
        {
            var ctx = new BaseContext();
            Assert.Throws<ArgumentException>(() => ctx.RaiseSignal<DateTime>("ev", DateTime.Now));
        }

        [Test]
        public void NullOrEmptyName_Warns_AndNoOps()
        {
            var ctx = new BaseContext();

            LogAssert.Expect(LogType.Warning, "[GraphCore] RaiseSignal called with a null or empty name; ignored.");
            ctx.RaiseSignal("");

            LogAssert.Expect(LogType.Warning, "[GraphCore] OnSignal called with a null or empty name; ignored.");
            ctx.OnSignal(null, _ => { });

            LogAssert.Expect(LogType.Warning, "[GraphCore] OffSignal called with a null or empty name; ignored.");
            ctx.OffSignal("", _ => { });
        }
    }
}
