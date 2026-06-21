using System.Collections.Generic;
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

        // The registry is a process-wide singleton populated by real adapters via [InitializeOnLoad].
        // Snapshot and restore it around each test so running tests never wipes the live registrations
        // (which would otherwise make Build All Tables find no adapters until the next domain reload).
        private List<IGraphLocalizationAdapter> _saved;

        [SetUp]
        public void SetUp()
        {
            _saved = new List<IGraphLocalizationAdapter>(GraphLocalizationAdapterRegistry.Adapters);
            GraphLocalizationAdapterRegistry.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            GraphLocalizationAdapterRegistry.Clear();
            foreach (var adapter in _saved)
                GraphLocalizationAdapterRegistry.Register(adapter);
        }

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
        public void DiscoverAdapters_IncludesManualRegistrations()
        {
            GraphLocalizationAdapterRegistry.Register(new StubAdapter("ManualLib"));
            var discovered = GraphLocalizationAdapterRegistry.DiscoverAdapters();

            bool found = false;
            foreach (var a in discovered) if (a.LibName == "ManualLib") { found = true; break; }
            Assert.IsTrue(found, "DiscoverAdapters must include manually-registered adapters.");
        }

        [Test]
        public void DiscoverAdapters_DeduplicatesByLibName()
        {
            // A manual adapter whose LibName collides with an auto-discovered one would appear once.
            GraphLocalizationAdapterRegistry.Register(new StubAdapter("Dup"));
            GraphLocalizationAdapterRegistry.Register(new StubAdapter("Dup")); // ignored by Register
            var discovered = GraphLocalizationAdapterRegistry.DiscoverAdapters();

            int count = 0;
            foreach (var a in discovered) if (a.LibName == "Dup") count++;
            Assert.AreEqual(1, count);
        }

        [Test]
        public void ScanAndIndex_Called_PopulatesDatabase()
        {
            // Verify the adapter contract: ScanAndIndex receives the database and can populate it.
            LocalizationDatabase captured = null;
            var adapter = new CapturingAdapter("Test", d => captured = d);

            GraphLocalizationAdapterRegistry.Register(adapter);

            var db = new LocalizationDatabase();
            adapter.ScanAndIndex(db);
            Assert.AreSame(db, captured, "ScanAndIndex must receive the passed database.");
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
