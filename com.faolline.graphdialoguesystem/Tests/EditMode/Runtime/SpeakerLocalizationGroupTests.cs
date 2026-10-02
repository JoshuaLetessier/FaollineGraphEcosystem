using Faolline.GraphLocalization;
using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>
    /// A speaker's optional localization group and the table names derived from it (052, research R2):
    /// no group → "GraphDialogue_Speakers"; group G → "GraphDialogue_Speakers_G".
    /// </summary>
    public class SpeakerLocalizationGroupTests
    {
        private Speaker _speaker;

        [SetUp]
        public void SetUp() => _speaker = ScriptableObject.CreateInstance<Speaker>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_speaker);

        [Test]
        public void NewSpeaker_HasNoGroup()
            => Assert.AreEqual(string.Empty, _speaker.LocalizationGroup);

        [Test]
        public void LibName_IsGraphDialogue()
            => Assert.AreEqual("GraphDialogue", DialogueLocalizationKeys.LibName);

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void NoGroup_MapsToDefaultSpeakersTable(string group)
        {
            _speaker.LocalizationGroup = group;
            Assert.AreEqual("Speakers", DialogueLocalizationKeys.SpeakerTableGroup(_speaker));
            Assert.AreEqual("GraphDialogue_Speakers", DialogueLocalizationKeys.ForSpeakerTable(_speaker));
        }

        [Test]
        public void Group_IsTrimmedAndScoped()
        {
            _speaker.LocalizationGroup = " Chapitre1 ";
            Assert.AreEqual("Speakers_Chapitre1", DialogueLocalizationKeys.SpeakerTableGroup(_speaker));
            Assert.AreEqual("GraphDialogue_Speakers_Chapitre1", DialogueLocalizationKeys.ForSpeakerTable(_speaker));
        }

        [Test]
        public void ForSpeakerGroupTable_MatchesTheSpeakerOverload()
        {
            Assert.AreEqual("GraphDialogue_Speakers", DialogueLocalizationKeys.ForSpeakerGroupTable(null));
            Assert.AreEqual("GraphDialogue_Speakers_Chapitre1", DialogueLocalizationKeys.ForSpeakerGroupTable(" Chapitre1 "));
            _speaker.LocalizationGroup = "Chapitre1";
            Assert.AreEqual(DialogueLocalizationKeys.ForSpeakerTable(_speaker), DialogueLocalizationKeys.ForSpeakerGroupTable("Chapitre1"));
        }

        [Test]
        public void NullSpeaker_MapsToDefaultSpeakersTable()
        {
            Assert.AreEqual("Speakers", DialogueLocalizationKeys.SpeakerTableGroup(null));
            Assert.AreEqual("GraphDialogue_Speakers", DialogueLocalizationKeys.ForSpeakerTable(null));
        }

        [Test]
        public void ForGraphTable_IsTheSharedPerGraphName()
        {
            Assert.IsNull(DialogueLocalizationKeys.ForGraphTable(null));
            var graph = ScriptableObject.CreateInstance<DialogueGraph>();
            try
            {
                graph.name = "DLG:001";
                Assert.AreEqual(LocalizationTableNames.ForGraph("DLG:001"), DialogueLocalizationKeys.ForGraphTable(graph));
                Assert.AreEqual("DLG_001", DialogueLocalizationKeys.ForGraphTable(graph));
            }
            finally { Object.DestroyImmediate(graph); }
        }
    }
}
