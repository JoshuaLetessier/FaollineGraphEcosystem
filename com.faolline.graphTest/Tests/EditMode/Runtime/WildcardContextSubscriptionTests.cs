using NUnit.Framework;
using Faolline.GraphCore;

namespace Faolline.GraphTest.Tests
{
    [TestFixture]
    public class WildcardContextSubscriptionTests
    {
        private BaseContext _ctx;

        [SetUp]
        public void SetUp() => _ctx = new BaseContext();

        // ── OnAnyVariableChanged ─────────────────────────────────────────────

        [Test]
        public void OnAnyParameterChanged_FiresOnSetInt()
        {
            string changedKey = null;
            _ctx.OnAnyVariableChanged(k => changedKey = k);

            _ctx.Set<int>("score", 42);

            Assert.AreEqual("score", changedKey);
        }

        [Test]
        public void OnAnyParameterChanged_FiresForAllTypes()
        {
            var keys = new System.Collections.Generic.List<string>();
            _ctx.OnAnyVariableChanged(k => keys.Add(k));

            _ctx.Set<bool>("flag", true);
            _ctx.Set<int>("score", 1);
            _ctx.Set<float>("hp", 0.5f);
            _ctx.Set<string>("name", "test");

            Assert.AreEqual(4, keys.Count);
            Assert.Contains("flag", keys);
            Assert.Contains("score", keys);
            Assert.Contains("hp", keys);
            Assert.Contains("name", keys);
        }

        [Test]
        public void OffAnyParameterChanged_StopsNotifications()
        {
            int count = 0;
            System.Action<string> handler = _ => count++;

            _ctx.OnAnyVariableChanged(handler);
            _ctx.Set<int>("a", 1);
            Assert.AreEqual(1, count);

            _ctx.OffAnyParameterChanged(handler);
            _ctx.Set<int>("b", 2);
            Assert.AreEqual(1, count, "Should not fire after Off.");
        }

        [Test]
        public void PerKeyHandler_FiresBeforeWildcard()
        {
            var order = new System.Collections.Generic.List<string>();
            _ctx.OnVariableChanged("gold", _ => order.Add("perkey"));
            _ctx.OnAnyVariableChanged(_ => order.Add("wildcard"));

            _ctx.Set<int>("gold", 100);

            Assert.AreEqual(2, order.Count);
            Assert.AreEqual("perkey", order[0]);
            Assert.AreEqual("wildcard", order[1]);
        }

        // ── OnAnyCollectionChanged ────────────────────────────────────────────

        [Test]
        public void OnAnyCollectionChanged_FiresOnAddRemoveClear()
        {
            var keys = new System.Collections.Generic.List<string>();
            _ctx.OnAnyCollectionChanged(k => keys.Add(k));

            _ctx.AddToCollection("inv", "sword");
            _ctx.RemoveFromCollection("inv", "sword");
            _ctx.AddToCollection("herbs", "a");
            _ctx.ClearCollection("herbs");

            Assert.AreEqual(4, keys.Count);
            Assert.AreEqual("inv", keys[0]);
            Assert.AreEqual("inv", keys[1]);
            Assert.AreEqual("herbs", keys[2]);
            Assert.AreEqual("herbs", keys[3]);
        }

        [Test]
        public void OffAnyCollectionChanged_StopsNotifications()
        {
            int count = 0;
            System.Action<string> handler = _ => count++;

            _ctx.OnAnyCollectionChanged(handler);
            _ctx.AddToCollection("inv", "sword");
            Assert.AreEqual(1, count);

            _ctx.OffAnyCollectionChanged(handler);
            _ctx.AddToCollection("inv", "shield");
            Assert.AreEqual(1, count, "Should not fire after Off.");
        }

        [Test]
        public void PerKeyCollectionHandler_FiresBeforeWildcard()
        {
            var order = new System.Collections.Generic.List<string>();
            _ctx.OnCollectionChanged("inv", _ => order.Add("perkey"));
            _ctx.OnAnyCollectionChanged(_ => order.Add("wildcard"));

            _ctx.AddToCollection("inv", "sword");

            Assert.AreEqual(2, order.Count);
            Assert.AreEqual("perkey", order[0]);
            Assert.AreEqual("wildcard", order[1]);
        }
    }
}
