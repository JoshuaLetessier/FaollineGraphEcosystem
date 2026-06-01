using NUnit.Framework;
using Faolline.GraphLocalization;
using Faolline.GraphLocalization.Editor;

namespace Faolline.GraphLocalization.Tests
{
    /// <summary>Unit tests for GraphLocalizationAdapterRegistry — registration and deduplication.</summary>
    public class GraphLocalizationAdapterRegistryTests
    {
        // Minimal stub adapter for testing.
        private sealed class StubAdapter : IGraphLocalizationAdapter
        {
            public string LibName { get; }
            public StubAdapter(string name) => LibName = name;
            public void ScanAndIndex(LocalizationDatabase database) { }
        }

        [SetUp]
        public void SetUp() => GraphLocalizationAdapterRegistry.Clear();

        [TearDown]
        public void TearDown() => GraphLocalizationAdapterRegistry.Clear();

        [Test]
        public void Register_AddsAdapter()
        {
            GraphLocalizationAdapterRegistry.Register(new StubAdapter("Foo"));
            Assert.AreEqual(1, GraphLocalizationAdapterRegistry.Adapters.Count);
        }

        [Test]
        public void Register_IgnoresDuplicateLibName()
        {
            GraphLocalizationAdapterRegistry.Register(new StubAdapter("Foo"));
            GraphLocalizationAdapterRegistry.Register(new StubAdapter("Foo"));
            Assert.AreEqual(1, GraphLocalizationAdapterRegistry.Adapters.Count);
        }

        [Test]
        public void Register_IgnoresNull()
        {
            GraphLocalizationAdapterRegistry.Register(null);
            Assert.AreEqual(0, GraphLocalizationAdapterRegistry.Adapters.Count);
        }

        [Test]
        public void Register_AllowsDifferentLibNames()
        {
            GraphLocalizationAdapterRegistry.Register(new StubAdapter("Foo"));
            GraphLocalizationAdapterRegistry.Register(new StubAdapter("Bar"));
            Assert.AreEqual(2, GraphLocalizationAdapterRegistry.Adapters.Count);
        }

        [Test]
        public void Unregister_RemovesAdapter()
        {
            var a = new StubAdapter("Foo");
            GraphLocalizationAdapterRegistry.Register(a);
            GraphLocalizationAdapterRegistry.Unregister(a);
            Assert.AreEqual(0, GraphLocalizationAdapterRegistry.Adapters.Count);
        }

        [Test]
        public void Clear_RemovesAll()
        {
            GraphLocalizationAdapterRegistry.Register(new StubAdapter("A"));
            GraphLocalizationAdapterRegistry.Register(new StubAdapter("B"));
            GraphLocalizationAdapterRegistry.Clear();
            Assert.AreEqual(0, GraphLocalizationAdapterRegistry.Adapters.Count);
        }

        [Test]
        public void ScanAndIndex_Called_PopulatesDatabase()
        {
            // Verify the adapter contract: ScanAndIndex receives the database and can populate it.
            UnityEngine.ScriptableObject db = null;
            LocalizationDatabase captured = null;
            var adapter = new CapturingAdapter("Test", d => captured = d);

            GraphLocalizationAdapterRegistry.Register(adapter);

            db = UnityEngine.ScriptableObject.CreateInstance<LocalizationDatabase>();
            try
            {
                adapter.ScanAndIndex((LocalizationDatabase)db);
                Assert.AreSame(db, captured, "ScanAndIndex must receive the passed database.");
            }
            finally { UnityEngine.Object.DestroyImmediate(db); }
        }

        private sealed class CapturingAdapter : IGraphLocalizationAdapter
        {
            private readonly System.Action<LocalizationDatabase> _onScan;
            public string LibName { get; }
            public CapturingAdapter(string name, System.Action<LocalizationDatabase> onScan)
            {
                LibName = name;
                _onScan = onScan;
            }
            public void ScanAndIndex(LocalizationDatabase database) => _onScan(database);
        }
    }
}
