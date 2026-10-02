using System.Collections.Generic;
using NUnit.Framework;

namespace Faolline.GraphLocalization.Unity.Tests
{
    /// <summary>
    /// Runtime side of 052 (research R4/R5): a lookup in a designated table reads ONLY that table's collection —
    /// it never probes the others, which is what lets separately-packaged tables stay unloaded. A table absent
    /// from the build manifest (e.g. a graph renamed by Instantiate to "X(Clone)") falls back to the classic
    /// probing, warned once. Observed through a recording fake of the provider's table-reader seam.
    /// </summary>
    public class UnityLocalizationProviderScopedTests
    {
        private sealed class RecordingReader : IStringTableReader
        {
            public readonly Dictionary<string, Dictionary<string, string>> Collections = new Dictionary<string, Dictionary<string, string>>();
            public readonly List<string> Reads = new List<string>();

            public RecordingReader With(string collection, string key, string value)
            {
                if (!Collections.TryGetValue(collection, out var keys)) Collections[collection] = keys = new Dictionary<string, string>();
                keys[key] = value;
                return this;
            }

            public bool TryRead(string collection, string key, out string value)
            {
                Reads.Add(collection);
                value = null;
                return Collections.TryGetValue(collection, out var keys) && keys.TryGetValue(key, out value);
            }
        }

        private static readonly string[] Manifest = { "D1_Text", "D2_Text", "GraphDialogue_Speakers_A_Text" };

        private static UnityLocalizationProvider Provider(RecordingReader reader)
            => new UnityLocalizationProvider(Manifest, null, reader);

        [Test]
        public void KnownTable_ReadsOnlyThatCollection()
        {
            var reader = new RecordingReader().With("D1_Text", "line_a", "Bonjour").With("D2_Text", "line_b", "Salut");

            Assert.AreEqual("Bonjour", Provider(reader).ResolveInTable("D1", "line_a", "fr"));
            CollectionAssert.AreEqual(new[] { "D1_Text" }, reader.Reads);
        }

        [Test]
        public void KnownTable_MissingKey_IsTheMarker_WithoutProbingOthers()
        {
            var reader = new RecordingReader().With("D2_Text", "line_a", "Elsewhere");

            Assert.AreEqual("#line_a", Provider(reader).ResolveInTable("D1", "line_a", "fr"));
            CollectionAssert.AreEqual(new[] { "D1_Text" }, reader.Reads, "a targeted miss must not open other tables");
        }

        [Test]
        public void KnownTable_UntranslatedEverywhere_IsTheMarker()
        {
            var reader = new RecordingReader().With("D1_Text", "line_a", "");
            Assert.AreEqual("#line_a", Provider(reader).ResolveInTable("D1", "line_a", "fr"));
        }

        [Test]
        public void SpeakerGroupTable_IsTargetedToo()
        {
            var reader = new RecordingReader().With("GraphDialogue_Speakers_A_Text", "speaker_x", "Aubergiste");

            Assert.AreEqual("Aubergiste", Provider(reader).ResolveInTable("GraphDialogue_Speakers_A", "speaker_x", "fr"));
            CollectionAssert.AreEqual(new[] { "GraphDialogue_Speakers_A_Text" }, reader.Reads);
        }

        [Test]
        public void UnknownTable_FallsBackToProbing_ReportedOncePerTable()
        {
            var reader = new RecordingReader().With("D2_Text", "line_a", "Found by probing");
            var provider = Provider(reader);

            Assert.AreEqual("Found by probing", provider.ResolveInTable("X(Clone)", "line_a", "fr"));
            CollectionAssert.AreEqual(new[] { "D1_Text", "D2_Text" }, reader.Reads, "classic manifest-order probing");
            Assert.AreEqual(1, provider.UnknownTablesReported);

            provider.ResolveInTable("X(Clone)", "line_b", "fr");
            Assert.AreEqual(1, provider.UnknownTablesReported, "warned once per table, not per lookup");
        }

        [Test]
        public void EmptyTable_IsTheClassicLookup()
        {
            var reader = new RecordingReader().With("D2_Text", "line_a", "Probed");
            Assert.AreEqual("Probed", Provider(reader).ResolveInTable(null, "line_a", "fr"));
            Assert.AreEqual(0, Provider(new RecordingReader()).UnknownTablesReported);
        }

        [Test]
        public void ClassicResolve_StillProbesInManifestOrder_AndCaches()
        {
            var reader = new RecordingReader().With("D2_Text", "line_a", "Salut");
            var provider = Provider(reader);

            Assert.AreEqual("Salut", provider.Resolve("line_a", "fr"));
            CollectionAssert.AreEqual(new[] { "D1_Text", "D2_Text" }, reader.Reads);

            reader.Reads.Clear();
            Assert.AreEqual("Salut", provider.Resolve("line_a", "fr"));
            CollectionAssert.AreEqual(new[] { "D2_Text" }, reader.Reads, "second lookup served from the key→collection cache");
        }

        [Test]
        public void ScopedLookup_DoesNotFeedTheClassicCache()
        {
            var reader = new RecordingReader().With("D1_Text", "line_a", "Bonjour").With("D2_Text", "line_a", "Other");
            var provider = Provider(reader);

            provider.ResolveInTable("D2", "line_a", "fr");
            reader.Reads.Clear();
            Assert.AreEqual("Bonjour", provider.Resolve("line_a", "fr"), "classic lookup unaffected by an earlier scoped one");
            CollectionAssert.AreEqual(new[] { "D1_Text" }, reader.Reads);
        }
    }
}
