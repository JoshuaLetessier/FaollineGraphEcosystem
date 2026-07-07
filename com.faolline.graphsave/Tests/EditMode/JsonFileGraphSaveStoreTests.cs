using System.IO;
using NUnit.Framework;
using Faolline.GraphCore;

namespace Faolline.GraphSave.Tests
{
    public class JsonFileGraphSaveStoreTests
    {
        private string _tempDir;
        private JsonFileGraphSaveStore _store;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "GraphSaveTest_" + System.Guid.NewGuid().ToString("N"));
            _store = new JsonFileGraphSaveStore(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        private static GraphRunSnapshot SampleSnapshot()
        {
            var ctx = new BaseContext();
            ctx.Set<int>("gold", 42);
            ctx.Set<string>("name", "Hero");
            ctx.AddToCollection("inventory", "sword");
            return GraphRunSnapshot.Capture(ctx, "graph1", "node5");
        }

        [Test]
        public void Save_Load_RoundTrips()
        {
            var original = SampleSnapshot();
            _store.Save("slot1", original);

            var loaded = _store.Load("slot1");
            Assert.IsNotNull(loaded);
            Assert.AreEqual("graph1", loaded.GraphId);
            Assert.AreEqual("node5", loaded.CurrentNodeId);
            Assert.AreEqual(original.Variables.Count, loaded.Variables.Count);
            Assert.AreEqual(original.Collections.Count, loaded.Collections.Count);
        }

        [Test]
        public void Exists_TrueAfterSave_FalseAfterDelete()
        {
            Assert.IsFalse(_store.Exists("slot1"));
            _store.Save("slot1", SampleSnapshot());
            Assert.IsTrue(_store.Exists("slot1"));
            _store.Delete("slot1");
            Assert.IsFalse(_store.Exists("slot1"));
        }

        [Test]
        public void Load_AbsentSlot_ReturnsNull()
        {
            Assert.IsNull(_store.Load("no_such_slot"));
        }

        [Test]
        public void Delete_AbsentSlot_IsNoOp()
        {
            _store.Delete("no_such_slot");
        }

        [Test]
        public void Save_OverwritesExistingSlot()
        {
            _store.Save("slot1", SampleSnapshot());
            var updated = GraphRunSnapshot.Capture(new BaseContext(), "graph2", "nodeX");
            _store.Save("slot1", updated);

            var loaded = _store.Load("slot1");
            Assert.AreEqual("graph2", loaded.GraphId);
        }

        [Test]
        public void Save_NullSlot_IsNoOp()
        {
            _store.Save(null, SampleSnapshot());
            _store.Save("", SampleSnapshot());
        }

        [Test]
        public void ApplyTo_RestoresContext()
        {
            _store.Save("slot1", SampleSnapshot());
            var loaded = _store.Load("slot1");

            var ctx = new BaseContext();
            loaded.ApplyTo(ctx);
            Assert.AreEqual(42, ctx.Get<int>("gold"));
            Assert.AreEqual("Hero", ctx.Get<string>("name"));
            Assert.IsTrue(ctx.CollectionContains("inventory", "sword"));
        }
    }
}
