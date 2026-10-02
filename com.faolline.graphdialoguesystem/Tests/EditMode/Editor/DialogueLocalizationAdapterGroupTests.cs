using System;
using System.Linq;
using System.Text.RegularExpressions;
using Faolline.GraphDialogue.Editor;
using Faolline.GraphLocalization;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>
    /// The dialogue localization adapter files each speaker's name key under its table group (052, research
    /// R2/R7): no group → "Speakers", group G → "Speakers_G". Two speakers sharing an id but not a group is an
    /// explicit error naming both assets; the path-order-first one keeps the key (its translations survive).
    /// The project holds other speakers, so every assertion is filtered to this test's unique ids.
    /// </summary>
    public class DialogueLocalizationAdapterGroupTests
    {
        private string _folder;
        private string _suffix;

        [SetUp]
        public void SetUp()
        {
            _suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var name = "__GDLocGroupTest_" + _suffix;
            AssetDatabase.CreateFolder("Assets", name);
            _folder = "Assets/" + name;
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(_folder);

        private string Id(string local) => $"t{_suffix}_{local}";

        private Speaker CreateSpeaker(string fileName, string speakerId, string group)
        {
            var s = ScriptableObject.CreateInstance<Speaker>();
            s.SpeakerId = speakerId;
            s.DisplayNameFallback = speakerId;
            s.LocalizationGroup = group;
            AssetDatabase.CreateAsset(s, $"{_folder}/{fileName}.asset");
            return s;
        }

        private static LocalizationKeyEntry Find(LocalizationDatabase db, string speakerId)
            => db.GlobalKeys.Single(k => k.Key == DialogueLocalizationKeys.ForSpeakerId(speakerId));

        [Test]
        public void SpeakerKeys_AreFiledUnderTheirTableGroup()
        {
            CreateSpeaker("A", Id("a"), "Chapitre1");
            CreateSpeaker("B", Id("b"), "");

            var db = new LocalizationDatabase();
            new DialogueGraphLocalizationAdapter().ScanAndIndex(db);

            Assert.AreEqual("Speakers_Chapitre1", Find(db, Id("a")).Group);
            Assert.AreEqual("Speakers", Find(db, Id("b")).Group);
            Assert.AreEqual(LocalizationKeyType.SpeakerName, Find(db, Id("a")).Type);
        }

        [Test]
        public void SameIdSameGroup_IsOneKeyWithoutError()
        {
            CreateSpeaker("A", Id("dup"), "Chapitre1");
            CreateSpeaker("B", Id("dup"), " Chapitre1 ");

            var db = new LocalizationDatabase();
            new DialogueGraphLocalizationAdapter().ScanAndIndex(db);

            Assert.AreEqual(1, db.GlobalKeys.Count(k => k.Key == DialogueLocalizationKeys.ForSpeakerId(Id("dup"))));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SameIdDifferentGroups_IsAnErrorNamingBothAssets_FirstPathKeepsTheKey()
        {
            CreateSpeaker("A_first", Id("clash"), "Chapitre1");
            CreateSpeaker("B_second", Id("clash"), "Chapitre2");

            LogAssert.Expect(LogType.Error, new Regex(
                $"{Regex.Escape(Id("clash"))}.*{Regex.Escape(_folder + "/A_first.asset")}.*{Regex.Escape(_folder + "/B_second.asset")}",
                RegexOptions.Singleline));

            var db = new LocalizationDatabase();
            new DialogueGraphLocalizationAdapter().ScanAndIndex(db);

            Assert.AreEqual("Speakers_Chapitre1", Find(db, Id("clash")).Group);
        }

        [Test]
        public void LibName_IsTheSharedRuntimeConstant()
            => Assert.AreEqual(DialogueLocalizationKeys.LibName, new DialogueGraphLocalizationAdapter().LibName);
    }
}
