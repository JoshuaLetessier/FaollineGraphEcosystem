using NUnit.Framework;

namespace Faolline.GraphLocalization.Tests
{
    /// <summary>
    /// The inline per-graph localization flags (embedded on a graph via <see cref="ILocalizedGraph"/>,
    /// replacing the old companion asset): default fall-through, per-node overrides, apply-to-all.
    /// </summary>
    public class GraphLocalizationFlagsTests
    {
        [Test]
        public void GetFlags_UnknownNode_ReturnsDefault()
        {
            var f = new GraphLocalizationFlags { DefaultFlags = LocalizedAssetFlags.Text };
            Assert.AreEqual(LocalizedAssetFlags.Text, f.GetFlags("nope"));
        }

        [Test]
        public void SetFlags_OverridesOneNode_OthersStayDefault()
        {
            var f = new GraphLocalizationFlags { DefaultFlags = LocalizedAssetFlags.Text };
            f.SetFlags("a", LocalizedAssetFlags.Text | LocalizedAssetFlags.Audio);

            Assert.AreEqual(LocalizedAssetFlags.Text | LocalizedAssetFlags.Audio, f.GetFlags("a"));
            Assert.AreEqual(LocalizedAssetFlags.Text, f.GetFlags("b"), "unset node still resolves to the default");
        }

        [Test]
        public void SetFlags_SameNodeTwice_UpdatesInPlace()
        {
            var f = new GraphLocalizationFlags();
            f.SetFlags("a", LocalizedAssetFlags.Audio);
            f.SetFlags("a", LocalizedAssetFlags.Sprite);

            Assert.AreEqual(LocalizedAssetFlags.Sprite, f.GetFlags("a"));
            Assert.AreEqual(1, f.Entries.Count, "re-setting a node does not add a second entry");
        }

        [Test]
        public void ApplyDefaultToAll_MaterializesEveryNode()
        {
            var f = new GraphLocalizationFlags { DefaultFlags = LocalizedAssetFlags.Audio };
            f.ApplyDefaultToAll(new[] { "a", "b", "c" });

            Assert.AreEqual(3, f.Entries.Count);
            Assert.AreEqual(LocalizedAssetFlags.Audio, f.GetFlags("b"));
        }

        [Test]
        public void HasLocalizedAssets_TrueOnlyBeyondText()
        {
            var f = new GraphLocalizationFlags();
            f.SetFlags("text",  LocalizedAssetFlags.Text);
            f.SetFlags("audio", LocalizedAssetFlags.Text | LocalizedAssetFlags.Audio);

            Assert.IsFalse(f.HasLocalizedAssets("text"));
            Assert.IsTrue(f.HasLocalizedAssets("audio"));
        }

        [Test]
        public void SetFlags_NullOrEmptyId_IsNoOp()
        {
            var f = new GraphLocalizationFlags();
            Assert.DoesNotThrow(() => { f.SetFlags(null, LocalizedAssetFlags.Audio); f.SetFlags("", LocalizedAssetFlags.Audio); });
            Assert.AreEqual(0, f.Entries.Count);
        }
    }
}
