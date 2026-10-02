using NUnit.Framework;

namespace Faolline.GraphImport.Tests
{
    /// <summary>
    /// 052 US4: the speaker → table-group mapping (SpeakerKey,Table) the dialogue import reads. Validated as a
    /// whole before anything is written — never guessed.
    /// </summary>
    public class SpeakerGroupMappingTests
    {
        [Test]
        public void ColumnsInAnyOrder_ExtraColumnsIgnored()
        {
            var m = SpeakerGroupMapping.Parse("Table,Notes,SpeakerKey\nChapitre1,whatever,PNJ_A\n");
            Assert.IsTrue(m.TryGetGroup("PNJ_A", out var group));
            Assert.AreEqual("Chapitre1", group);
        }

        [Test]
        public void QuotedKeyWithComma()
        {
            var m = SpeakerGroupMapping.Parse("SpeakerKey,Table\n\"PNJ_Garde, nuit\",Chapitre1\n");
            Assert.IsTrue(m.TryGetGroup("PNJ_Garde, nuit", out var group));
            Assert.AreEqual("Chapitre1", group);
        }

        [Test]
        public void EmptyTable_IsAnExplicitNoGroup()
        {
            var m = SpeakerGroupMapping.Parse("SpeakerKey,Table\nNarrateur,\n");
            Assert.IsTrue(m.TryGetGroup("Narrateur", out var group));
            Assert.AreEqual(string.Empty, group);
        }

        [Test]
        public void ValuesAreTrimmed()
        {
            var m = SpeakerGroupMapping.Parse("SpeakerKey,Table\n PNJ_A , Chapitre1 \n");
            Assert.IsTrue(m.TryGetGroup("PNJ_A", out var group));
            Assert.AreEqual("Chapitre1", group);
        }

        [Test]
        public void ExactDuplicateRow_IsAccepted()
        {
            var m = SpeakerGroupMapping.Parse("SpeakerKey,Table\nPNJ_A,Chapitre1\nPNJ_A, Chapitre1\n");
            Assert.AreEqual(1, m.Count);
        }

        [Test]
        public void SameKeyWithTwoGroups_IsRejected_NamingKeyAndBothValues()
        {
            var ex = Assert.Throws<SpeakerGroupMappingException>(() =>
                SpeakerGroupMapping.Parse("SpeakerKey,Table\nPNJ_A,Chapitre1\nPNJ_A,Chapitre2\n"));
            StringAssert.Contains("PNJ_A", ex.Message);
            StringAssert.Contains("Chapitre1", ex.Message);
            StringAssert.Contains("Chapitre2", ex.Message);
        }

        [TestCase("Key,Table\nPNJ_A,C1\n", "SpeakerKey")]
        [TestCase("SpeakerKey,Group\nPNJ_A,C1\n", "Table")]
        [TestCase("", "SpeakerKey")]
        public void MissingRequiredColumn_IsRejected_NamingIt(string csv, string column)
        {
            var ex = Assert.Throws<SpeakerGroupMappingException>(() => SpeakerGroupMapping.Parse(csv));
            StringAssert.Contains(column, ex.Message);
        }

        [Test]
        public void EmptyKey_IsRejected_NamingTheLine()
        {
            var ex = Assert.Throws<SpeakerGroupMappingException>(() =>
                SpeakerGroupMapping.Parse("SpeakerKey,Table\nPNJ_A,C1\n  ,C2\n"));
            StringAssert.Contains("line 3", ex.Message);
        }

        [Test]
        public void UnknownKey_IsNotFound()
        {
            var m = SpeakerGroupMapping.Parse("SpeakerKey,Table\nPNJ_A,C1\n");
            Assert.IsFalse(m.TryGetGroup("PNJ_Z", out _));
            Assert.IsFalse(m.TryGetGroup(null, out _));
        }

        [Test]
        public void Count_IsDistinctKeys_HeaderOnlyIsEmpty()
        {
            Assert.AreEqual(2, SpeakerGroupMapping.Parse("SpeakerKey,Table\nA,1\nB,\n").Count);
            Assert.AreEqual(0, SpeakerGroupMapping.Parse("SpeakerKey,Table\n").Count);
        }
    }
}
