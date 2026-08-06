using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
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

        [Test]
        public void Load_CorruptedFile_ReturnsNullInsteadOfThrowing()
        {
            _store.Save("slot1", SampleSnapshot());
            // Simulate a crash mid-write / truncated file: overwrite with unparseable content.
            var path = Path.Combine(_tempDir, "slot1.json");
            File.WriteAllText(path, "{ this is not valid json ][");

            GraphRunSnapshot loaded = null;
            Assert.DoesNotThrow(() => loaded = _store.Load("slot1"));
            Assert.IsNull(loaded);
        }

        [Test]
        public void Save_Load_LongUnicodeSlotName_DoesNotThrow()
        {
            var longUnicode = new string('あ', 400) + "🎮🎮🎮🎮🎮";

            Assert.DoesNotThrow(() => _store.Save(longUnicode, SampleSnapshot()));
            GraphRunSnapshot loaded = null;
            Assert.DoesNotThrow(() => loaded = _store.Load(longUnicode));
            Assert.IsNotNull(loaded);
            Assert.AreEqual("graph1", loaded.GraphId);
        }

        [Test]
        public void Save_TwoDistinctLongSlotNames_DoNotCollide()
        {
            var prefix = new string('x', 400);
            var slotA = prefix + "AAAA";
            var slotB = prefix + "BBBB";

            var snapA = GraphRunSnapshot.Capture(new BaseContext(), "graphA", "nodeA");
            var snapB = GraphRunSnapshot.Capture(new BaseContext(), "graphB", "nodeB");

            _store.Save(slotA, snapA);
            _store.Save(slotB, snapB);

            Assert.AreEqual("graphA", _store.Load(slotA).GraphId);
            Assert.AreEqual("graphB", _store.Load(slotB).GraphId);
        }

        [Test]
        public void Save_TraversalSlot_IsRejectedAndDoesNotEscape()
        {
            // Reject (not sanitize) a slot that isn't a single path segment — matches
            // com.faolline.savesystem.core's JsonSaveSystem, which rejects the same shape (fix 330c049).
            var afterTraversal = "escape_test_" + System.Guid.NewGuid().ToString("N");
            var slot = "../" + afterTraversal;
            var escapedPath = Path.Combine(Path.GetDirectoryName(_tempDir), afterTraversal + ".json");

            LogAssert.Expect(LogType.Error, new Regex(@"\[GraphSave\] Slot '.*' is not a valid save name.*"));
            Assert.DoesNotThrow(() => _store.Save(slot, SampleSnapshot()));
            Assert.IsFalse(_store.Exists(slot));
            Assert.IsNull(_store.Load(slot));
            Assert.DoesNotThrow(() => _store.Delete(slot));

            Assert.IsFalse(File.Exists(escapedPath), "a traversal-shaped slot must never write outside the store's own folder.");
        }

        [Test]
        public void Save_SlotWithInvalidFilenameCharacter_IsRejected()
        {
            var slot = "bad:name?";
            LogAssert.Expect(LogType.Error, new Regex(@"\[GraphSave\] Slot '.*' is not a valid save name.*"));
            Assert.DoesNotThrow(() => _store.Save(slot, SampleSnapshot()));
            Assert.IsFalse(_store.Exists(slot));
            Assert.IsNull(_store.Load(slot));
        }

        [Test]
        public void GetAllKeys_EmptyStore_ReturnsEmpty()
        {
            CollectionAssert.IsEmpty(_store.GetAllKeys());
        }

        [Test]
        public void GetAllKeys_ReturnsEverySavedSlot()
        {
            _store.Save("slot1", SampleSnapshot());
            _store.Save("slot2", SampleSnapshot());

            var keys = _store.GetAllKeys().ToList();
            CollectionAssert.AreEquivalent(new[] { "slot1", "slot2" }, keys);
        }

        [Test]
        public void GetAllKeys_AfterDelete_ExcludesDeletedSlot()
        {
            _store.Save("slot1", SampleSnapshot());
            _store.Save("slot2", SampleSnapshot());
            _store.Delete("slot1");

            CollectionAssert.AreEquivalent(new[] { "slot2" }, _store.GetAllKeys().ToList());
        }

        [Test]
        public void DeleteAll_RemovesEverySlot()
        {
            _store.Save("slot1", SampleSnapshot());
            _store.Save("slot2", SampleSnapshot());

            _store.DeleteAll();

            Assert.IsFalse(_store.Exists("slot1"));
            Assert.IsFalse(_store.Exists("slot2"));
            CollectionAssert.IsEmpty(_store.GetAllKeys());
        }

        [Test]
        public void DeleteAll_OnEmptyStore_IsNoOp()
        {
            Assert.DoesNotThrow(() => _store.DeleteAll());
        }

        [Test]
        public void DeleteAll_StoreNeverUsed_IsNoOp()
        {
            // RootPath's directory may not even exist yet if nothing was ever saved — DeleteAll must not throw.
            var freshStore = new JsonFileGraphSaveStore(Path.Combine(Path.GetTempPath(), "GraphSaveTest_" + System.Guid.NewGuid().ToString("N")));
            Assert.DoesNotThrow(() => freshStore.DeleteAll());
        }
    }
}
