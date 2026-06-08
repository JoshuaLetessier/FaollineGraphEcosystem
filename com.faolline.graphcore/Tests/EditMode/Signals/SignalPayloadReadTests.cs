using NUnit.Framework;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// US3 — graph logic reads the last payload of a named signal via TryGetLastSignal, distinguishing a
    /// scalar payload from "no payload" and reading it typed.
    /// </summary>
    public class SignalPayloadReadTests
    {
        [Test]
        public void TryGetLastSignal_ReturnsLast_WithPayload()
        {
            var ctx = new BaseContext();
            ctx.RaiseSignal<string>("itemCollected", "key");

            Assert.IsTrue(ctx.TryGetLastSignal("itemCollected", out var a));
            Assert.IsTrue(a.HasPayload);
            Assert.AreEqual("key", a.GetPayload<string>());
        }

        [Test]
        public void TryGetLastSignal_Unknown_ReturnsFalse()
        {
            var ctx = new BaseContext();
            Assert.IsFalse(ctx.TryGetLastSignal("never", out var a));
            Assert.IsFalse(a.HasPayload);
        }

        [Test]
        public void TryGetLastSignal_OverwritesWithLatest()
        {
            var ctx = new BaseContext();
            ctx.RaiseSignal<int>("score", 1);
            ctx.RaiseSignal<int>("score", 2);

            Assert.IsTrue(ctx.TryGetLastSignal("score", out var a));
            Assert.AreEqual(2, a.GetPayload<int>());
        }

        [Test]
        public void TryGetLastSignal_NoPayload_AbsenceIsDetectable()
        {
            var ctx = new BaseContext();
            ctx.RaiseSignal("ev");

            Assert.IsTrue(ctx.TryGetLastSignal("ev", out var a));
            Assert.IsFalse(a.HasPayload);
        }

        [Test]
        public void TryGetLastSignal_EmptyName_ReturnsFalse()
        {
            var ctx = new BaseContext();
            Assert.IsFalse(ctx.TryGetLastSignal("", out _));
        }
    }
}
