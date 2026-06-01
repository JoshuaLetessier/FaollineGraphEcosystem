using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphLocalization;

namespace Faolline.GraphLocalization.Tests
{
    /// <summary>Unit tests for LocalizationDatabase — per-graph entries and global keys.</summary>
    public class LocalizationDatabaseTests
    {
        private LocalizationDatabase _db;

        [SetUp]
        public void SetUp() => _db = ScriptableObject.CreateInstance<LocalizationDatabase>();

        [TearDown]
        public void TearDown() { if (_db != null) Object.DestroyImmediate(_db); }

        // ── Per-graph entries ────────────────────────────────────────────────────

        [Test]
        public void GetOrCreateGraphEntry_CreatesNewEntry()
        {
            var entry = _db.GetOrCreateGraphEntry("guid-1", "MyGraph");
            Assert.IsNotNull(entry);
            Assert.AreEqual("guid-1", entry.GraphGuid);
            Assert.AreEqual("MyGraph", entry.GraphName);
            Assert.AreEqual(1, _db.Graphs.Count);
        }

        [Test]
        public void GetOrCreateGraphEntry_ReturnsSameEntryForSameGuid()
        {
            var a = _db.GetOrCreateGraphEntry("g1", "Graph");
            var b = _db.GetOrCreateGraphEntry("g1", "Graph");
            Assert.AreSame(a, b);
            Assert.AreEqual(1, _db.Graphs.Count);
        }

        [Test]
        public void FindGraphEntry_ReturnsNullForUnknownGuid()
        {
            _db.GetOrCreateGraphEntry("g1", "Graph");
            Assert.IsNull(_db.FindGraphEntry("unknown"));
        }

        [Test]
        public void LocalizationGraphEntry_AddKey_Deduplicates()
        {
            var entry = _db.GetOrCreateGraphEntry("g1", "G");
            entry.AddKey("k1", LocalizationKeyType.Text, hint: "Hello");
            entry.AddKey("k1", LocalizationKeyType.Text, hint: "Hello");
            Assert.AreEqual(1, entry.Keys.Count);
        }

        // ── Global keys ──────────────────────────────────────────────────────────

        [Test]
        public void AddGlobalKey_AddsKey()
        {
            _db.AddGlobalKey("speaker_npc", LocalizationKeyType.SpeakerName, "NPC");
            Assert.AreEqual(1, _db.GlobalKeys.Count);
            Assert.AreEqual("speaker_npc", _db.GlobalKeys[0].Key);
            Assert.AreEqual("NPC", _db.GlobalKeys[0].DefaultHint);
        }

        [Test]
        public void AddGlobalKey_Deduplicates()
        {
            _db.AddGlobalKey("speaker_npc", LocalizationKeyType.SpeakerName);
            _db.AddGlobalKey("speaker_npc", LocalizationKeyType.SpeakerName);
            Assert.AreEqual(1, _db.GlobalKeys.Count);
        }

        [Test]
        public void AddGlobalKey_IgnoresNullOrWhitespace()
        {
            _db.AddGlobalKey(null, LocalizationKeyType.Text);
            _db.AddGlobalKey("   ", LocalizationKeyType.Text);
            Assert.AreEqual(0, _db.GlobalKeys.Count);
        }

        // ── Clear ────────────────────────────────────────────────────────────────

        [Test]
        public void Clear_RemovesGraphsAndGlobalKeys()
        {
            _db.GetOrCreateGraphEntry("g1", "G");
            _db.AddGlobalKey("k1", LocalizationKeyType.SpeakerName);
            _db.Clear();
            Assert.AreEqual(0, _db.Graphs.Count);
            Assert.AreEqual(0, _db.GlobalKeys.Count);
        }

        // ── GetAllKeys ───────────────────────────────────────────────────────────

        [Test]
        public void GetAllKeys_UnifiesGraphAndGlobalKeys()
        {
            var entry = _db.GetOrCreateGraphEntry("g1", "G");
            entry.AddKey("line_abc", LocalizationKeyType.Text);
            entry.AddKey("choice_def", LocalizationKeyType.ChoiceLabel);
            _db.AddGlobalKey("speaker_npc", LocalizationKeyType.SpeakerName);

            var all = _db.GetAllKeys();
            Assert.AreEqual(3, all.Count);
            Assert.IsTrue(all.Contains("line_abc"));
            Assert.IsTrue(all.Contains("choice_def"));
            Assert.IsTrue(all.Contains("speaker_npc"));
        }

        [Test]
        public void GetAllKeys_EmptyWhenNothingAdded()
        {
            Assert.AreEqual(0, _db.GetAllKeys().Count);
        }
    }
}
