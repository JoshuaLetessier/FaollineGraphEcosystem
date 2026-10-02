using System;
using System.Linq;
using Faolline.GraphDialogue.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>
    /// The Speaker inspector's localization-group dropdown (052 follow-up): the groups already used by speakers in
    /// the project, plus "(None)" and "New group…", instead of a free-text field — no typo can create a stray table.
    /// </summary>
    public class SpeakerGroupCatalogTests
    {
        private string _folder;
        private string _sfx;

        [SetUp]
        public void SetUp()
        {
            _sfx = Guid.NewGuid().ToString("N").Substring(0, 8);
            AssetDatabase.CreateFolder("Assets", "__GDGroupCatalogTest_" + _sfx);
            _folder = "Assets/__GDGroupCatalogTest_" + _sfx;
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(_folder);

        private void CreateSpeaker(string file, string group)
        {
            var s = ScriptableObject.CreateInstance<Speaker>();
            s.SpeakerId = $"{file}_{_sfx}";
            s.LocalizationGroup = group;
            AssetDatabase.CreateAsset(s, $"{_folder}/{file}.asset");
        }

        // ── Collect ─────────────────────────────────────────────────────────────

        [Test]
        public void Collect_ListsEachUsedGroupOnce_FirstSpellingByPathWins_NoEmptyGroup()
        {
            CreateSpeaker("a", $"Chap{_sfx}");
            CreateSpeaker("b", $" chap{_sfx} ");
            CreateSpeaker("c", $"Other{_sfx}");
            CreateSpeaker("d", "");

            var mine = SpeakerGroupCatalog.Collect().Where(g => g.EndsWith(_sfx, StringComparison.OrdinalIgnoreCase)).ToList();

            CollectionAssert.AreEqual(new[] { $"Chap{_sfx}", $"Other{_sfx}" }, mine);
            CollectionAssert.DoesNotContain(SpeakerGroupCatalog.Collect(), string.Empty);
        }

        [Test]
        public void Collect_IsSortedCaseInsensitively()
        {
            CreateSpeaker("a", $"b_{_sfx}");
            CreateSpeaker("b", $"A_{_sfx}");

            var all = SpeakerGroupCatalog.Collect();
            Assert.Less(all.ToList().IndexOf($"A_{_sfx}"), all.ToList().IndexOf($"b_{_sfx}"));
        }

        // ── BuildPopup ──────────────────────────────────────────────────────────

        private static readonly string[] Groups = { "Chapitre1", "Chapitre2" };

        [Test]
        public void Popup_IsNoneThenGroupsThenNewGroup()
        {
            var p = SpeakerGroupCatalog.BuildPopup(Groups, "");

            CollectionAssert.AreEqual(new[] { "(None)", "Chapitre1", "Chapitre2", "New group…" }, p.Labels);
            Assert.AreEqual(string.Empty, p.Values[0]);
            Assert.AreEqual("Chapitre2", p.Values[2]);
            Assert.AreEqual(3, p.NewGroupIndex);
            Assert.AreEqual(0, p.Selected);
        }

        [TestCase(null)]
        [TestCase("   ")]
        public void Popup_BlankCurrent_SelectsNone(string current)
            => Assert.AreEqual(0, SpeakerGroupCatalog.BuildPopup(Groups, current).Selected);

        [Test]
        public void Popup_CurrentGroup_IsSelected()
            => Assert.AreEqual(2, SpeakerGroupCatalog.BuildPopup(Groups, "Chapitre2").Selected);

        [Test]
        public void Popup_CaseVariantOfAGroup_SelectsThatGroup()
            => Assert.AreEqual(2, SpeakerGroupCatalog.BuildPopup(Groups, " chapitre2 ").Selected);

        [Test]
        public void Popup_UnknownCurrent_IsKeptAndSelected()
        {
            var p = SpeakerGroupCatalog.BuildPopup(Groups, "Prologue");

            Assert.AreEqual("Prologue", p.Values[p.Selected]);
            Assert.AreEqual(p.Labels.Length - 1, p.NewGroupIndex, "New group… stays last");
            Assert.AreEqual(5, p.Labels.Length);
        }

        [Test]
        public void Popup_NoGroupsYet_IsNoneAndNewGroup()
        {
            var p = SpeakerGroupCatalog.BuildPopup(new string[0], "");
            CollectionAssert.AreEqual(new[] { "(None)", "New group…" }, p.Labels);
            Assert.AreEqual(1, p.NewGroupIndex);
        }
    }
}
