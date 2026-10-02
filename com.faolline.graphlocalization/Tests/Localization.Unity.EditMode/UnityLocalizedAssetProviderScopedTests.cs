using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphLocalization.Unity.Tests
{
    /// <summary>
    /// Asset side of 052's targeted lookup: a voice clip is looked up only in the designated table's own asset
    /// collections ({table}_Audio, {table}_Sprite…); a known table without asset collections resolves to null
    /// without probing; a table unknown to the manifest falls back to probing, reported once.
    /// </summary>
    public class UnityLocalizedAssetProviderScopedTests
    {
        private sealed class RecordingReader : IAssetTableReader
        {
            public readonly Dictionary<(string, string), Object> Assets = new Dictionary<(string, string), Object>();
            public readonly List<string> Reads = new List<string>();

            public bool TryRead<T>(string collection, string key, out T asset) where T : Object
            {
                Reads.Add(collection);
                asset = Assets.TryGetValue((collection, key), out var a) ? a as T : null;
                return Assets.ContainsKey((collection, key));
            }
        }

        private static readonly string[] AssetManifest = { "D1_Audio", "D1_Sprite", "D2_Audio" };
        private static readonly string[] TextManifest = { "D1_Text", "D2_Text", "D3_Text" };

        private static UnityLocalizedAssetProvider Provider(RecordingReader reader)
            => new UnityLocalizedAssetProvider(AssetManifest, TextManifest, reader);

        [Test]
        public void KnownTable_ReadsOnlyItsOwnAssetCollections()
        {
            var clip = AudioClip.Create("v", 1, 1, 1000, false);
            try
            {
                var reader = new RecordingReader();
                reader.Assets[("D1_Audio", "line_a")] = clip;

                Assert.AreSame(clip, Provider(reader).ResolveAssetInTable<AudioClip>("D1", "line_a"));
                CollectionAssert.AreEqual(new[] { "D1_Audio" }, reader.Reads);
            }
            finally { Object.DestroyImmediate(clip); }
        }

        [Test]
        public void KnownTable_Miss_ReadsOnlyItsCandidates()
        {
            var reader = new RecordingReader();
            Assert.IsNull(Provider(reader).ResolveAssetInTable<AudioClip>("D1", "line_a"));
            CollectionAssert.AreEqual(new[] { "D1_Audio", "D1_Sprite" }, reader.Reads, "never D2_*");
        }

        [Test]
        public void KnownTableWithoutAssetCollections_IsNull_WithoutProbing()
        {
            var reader = new RecordingReader();
            var provider = Provider(reader);
            Assert.IsNull(provider.ResolveAssetInTable<AudioClip>("D3", "line_a"));
            Assert.IsEmpty(reader.Reads);
            Assert.AreEqual(0, provider.UnknownTablesReported);
        }

        [Test]
        public void UnknownTable_FallsBackToProbing_ReportedOnce()
        {
            var reader = new RecordingReader();
            var provider = Provider(reader);

            provider.ResolveAssetInTable<AudioClip>("X(Clone)", "line_a");
            CollectionAssert.AreEqual(AssetManifest, reader.Reads);
            provider.ResolveAssetInTable<AudioClip>("X(Clone)", "line_b");
            Assert.AreEqual(1, provider.UnknownTablesReported);
        }

        [Test]
        public void ClassicResolveAsset_StillProbes()
        {
            var reader = new RecordingReader();
            new UnityLocalizedAssetProvider(AssetManifest, TextManifest, reader).ResolveAsset<AudioClip>("line_a");
            CollectionAssert.AreEqual(AssetManifest, reader.Reads);
        }
    }
}
