using NUnit.Framework;

namespace Faolline.GraphLocalization.Tests
{
    /// <summary>
    /// The single, platform-stable naming rule shared by the table build, the translation import and the
    /// runtime lookup (052, research R1). Every case must give the same result on every platform — the
    /// rule no longer depends on <c>Path.GetInvalidFileNameChars()</c>.
    /// </summary>
    public class LocalizationTableNamesTests
    {
        [TestCase("a\"b", "a_b")]
        [TestCase("a<b", "a_b")]
        [TestCase("a>b", "a_b")]
        [TestCase("a|b", "a_b")]
        [TestCase("a:b", "a_b")]
        [TestCase("a*b", "a_b")]
        [TestCase("a?b", "a_b")]
        [TestCase("a\\b", "a_b")]
        [TestCase("a/b", "a_b")]
        [TestCase("a\tb", "a_b")]
        [TestCase("a\u0001b", "a_b")]
        public void Sanitize_ReplacesEveryInvalidCharWithUnderscore(string raw, string expected)
            => Assert.AreEqual(expected, LocalizationTableNames.Sanitize(raw));

        [TestCase(null)]
        [TestCase("")]
        public void Sanitize_NullOrEmpty_IsUnnamed(string raw)
            => Assert.AreEqual("Unnamed", LocalizationTableNames.Sanitize(raw));

        [Test]
        public void Sanitize_LegalName_PassesThroughUnchanged()
            => Assert.AreEqual("DLG_001 Intro-Tavern (v2)", LocalizationTableNames.Sanitize("DLG_001 Intro-Tavern (v2)"));

        [Test]
        public void NormalizeGroup_TrimsAndTurnsBlankIntoNull()
        {
            Assert.AreEqual("Chapitre1", LocalizationTableNames.NormalizeGroup("  Chapitre1 "));
            Assert.IsNull(LocalizationTableNames.NormalizeGroup("   "));
            Assert.IsNull(LocalizationTableNames.NormalizeGroup(""));
            Assert.IsNull(LocalizationTableNames.NormalizeGroup(null));
        }

        [Test]
        public void GroupComparisonKey_IgnoresCaseAndSurroundingSpaces()
        {
            Assert.AreEqual(LocalizationTableNames.GroupComparisonKey("Chap1"),
                LocalizationTableNames.GroupComparisonKey(" chap1 "));
            Assert.AreNotEqual(LocalizationTableNames.GroupComparisonKey("Chap1"),
                LocalizationTableNames.GroupComparisonKey("Chap2"));
        }

        [Test]
        public void GroupComparisonKey_NamesThatSanitizeAlike_Collide()
            => Assert.AreEqual(LocalizationTableNames.GroupComparisonKey("a:b"),
                LocalizationTableNames.GroupComparisonKey("a?b"));

        [Test]
        public void ForGraph_IsTheSanitizedGraphName()
        {
            Assert.AreEqual("DLG_001", LocalizationTableNames.ForGraph("DLG_001"));
            Assert.AreEqual("Act_1", LocalizationTableNames.ForGraph("Act:1"));
        }

        [Test]
        public void ForGroup_IsLibScoped_DefaultGroupIsGlobal()
        {
            Assert.AreEqual("GraphDialogue_Global", LocalizationTableNames.ForGroup("GraphDialogue", null));
            Assert.AreEqual("GraphDialogue_Global", LocalizationTableNames.ForGroup("GraphDialogue", "  "));
            Assert.AreEqual("GraphDialogue_Speakers_Chapitre1",
                LocalizationTableNames.ForGroup("GraphDialogue", " Speakers_Chapitre1 "));
        }

        [Test]
        public void CollectionSuffixes()
        {
            Assert.AreEqual("DLG_001_Text", LocalizationTableNames.TextCollection("DLG_001"));
            Assert.AreEqual("DLG_001_Audio", LocalizationTableNames.AssetCollection("DLG_001", "Audio"));
        }

        [Test]
        public void AssetTypes_MatchTheLocalizedAssetFlags()
        {
            var types = LocalizationTableNames.AssetTypes;
            Assert.AreEqual(5, types.Count);
            Assert.AreEqual((1 << 1, "Audio"), types[0]);
            Assert.AreEqual((1 << 2, "Sprite"), types[1]);
            Assert.AreEqual((1 << 3, "Texture"), types[2]);
            Assert.AreEqual((1 << 4, "Video"), types[3]);
            Assert.AreEqual((1 << 5, "Font"), types[4]);
        }
    }
}
