using NUnit.Framework;

namespace Faolline.GraphLocalization.Tests
{
    /// <summary>
    /// Grouped global keys (052, research R6): a global key carries an optional, normalized group; the build
    /// produces one table per group. Group spellings are canonicalized case-insensitively (first seen wins)
    /// so two spellings never produce two tables that collide on a case-insensitive file system.
    /// </summary>
    public class LocalizationDatabaseGroupTests
    {
        private LocalizationDatabase _db;

        [SetUp]
        public void SetUp() => _db = new LocalizationDatabase();

        [Test]
        public void AddGlobalKey_StoresTrimmedGroup()
        {
            _db.AddGlobalKey("speaker_a", LocalizationKeyType.SpeakerName, "A", " Speakers_Chap1 ");
            Assert.AreEqual("Speakers_Chap1", _db.GlobalKeys[0].Group);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void AddGlobalKey_BlankGroup_IsDefaultGroup(string group)
        {
            _db.AddGlobalKey("speaker_a", LocalizationKeyType.SpeakerName, "A", group);
            Assert.IsNull(_db.GlobalKeys[0].Group);
        }

        [Test]
        public void AddGlobalKey_ThreeArgumentCall_StillDefaultGroup()
        {
            _db.AddGlobalKey("speaker_a", LocalizationKeyType.SpeakerName, "A");
            Assert.IsNull(_db.GlobalKeys[0].Group);
            Assert.AreEqual("A", _db.GlobalKeys[0].DefaultHint);
        }

        [Test]
        public void GlobalKeysByGroup_GroupsInFirstAppearanceOrder()
        {
            _db.AddGlobalKey("speaker_b", LocalizationKeyType.SpeakerName, "B", "Speakers_B");
            _db.AddGlobalKey("speaker_x", LocalizationKeyType.SpeakerName, "X", null);
            _db.AddGlobalKey("speaker_b2", LocalizationKeyType.SpeakerName, "B2", "Speakers_B");

            var groups = _db.GlobalKeysByGroup();

            Assert.AreEqual(2, groups.Count);
            Assert.AreEqual("Speakers_B", groups[0].group);
            CollectionAssert.AreEqual(new[] { "speaker_b", "speaker_b2" }, new[] { groups[0].keys[0].Key, groups[0].keys[1].Key });
            Assert.IsNull(groups[1].group);
            Assert.AreEqual("speaker_x", groups[1].keys[0].Key);
        }

        [Test]
        public void GlobalKeysByGroup_EmptyWhenNoGlobalKeys()
            => Assert.AreEqual(0, _db.GlobalKeysByGroup().Count);

        [Test]
        public void CaseOnlyVariant_ReusesFirstSpelling()
        {
            _db.AddGlobalKey("speaker_a", LocalizationKeyType.SpeakerName, "A", "Speakers_Chap1");
            _db.AddGlobalKey("speaker_b", LocalizationKeyType.SpeakerName, "B", "speakers_chap1");

            Assert.AreEqual("Speakers_Chap1", _db.GlobalKeys[1].Group);
            Assert.AreEqual(1, _db.GlobalKeysByGroup().Count, "one table, not two colliding ones");
        }

        [Test]
        public void SanitizeAlikeVariant_ReusesFirstSpelling()
        {
            _db.AddGlobalKey("speaker_a", LocalizationKeyType.SpeakerName, "A", "Act:1");
            _db.AddGlobalKey("speaker_b", LocalizationKeyType.SpeakerName, "B", "Act?1");

            Assert.AreEqual("Act:1", _db.GlobalKeys[1].Group);
        }

        [Test]
        public void DuplicateKey_FirstEntryWins()
        {
            _db.AddGlobalKey("speaker_a", LocalizationKeyType.SpeakerName, "First", "Speakers_A");
            _db.AddGlobalKey("speaker_a", LocalizationKeyType.SpeakerName, "Second", "Speakers_B");

            Assert.AreEqual(1, _db.GlobalKeys.Count);
            Assert.AreEqual("First", _db.GlobalKeys[0].DefaultHint);
            Assert.AreEqual("Speakers_A", _db.GlobalKeys[0].Group);
        }
    }
}
