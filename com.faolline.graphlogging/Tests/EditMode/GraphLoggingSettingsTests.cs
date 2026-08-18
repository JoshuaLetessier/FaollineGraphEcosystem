using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphLogging.Tests
{
    public class GraphLoggingSettingsTests
    {
        private GraphLoggingSettings _settings;

        [SetUp]
        public void SetUp() => _settings = ScriptableObject.CreateInstance<GraphLoggingSettings>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_settings);

        [Test]
        public void IsInfoEnabled_UnknownCategory_DefaultsToTrue()
        {
            Assert.IsTrue(_settings.IsInfoEnabled("Unknown.Category"));
        }

        [Test]
        public void IsWarningEnabled_UnknownCategory_DefaultsToTrue()
        {
            Assert.IsTrue(_settings.IsWarningEnabled("Unknown.Category"));
        }

        [Test]
        public void EnsureCategoryKnown_CreatesGroup_DefaultingBothLevelsEnabled()
        {
            _settings.EnsureCategoryKnown("GraphLocalization.AutoBuild");

            Assert.AreEqual(1, _settings.Groups.Count);
            Assert.AreEqual("GraphLocalization", _settings.Groups[0].Prefix);
            Assert.IsTrue(_settings.Groups[0].DefaultInfoEnabled);
            Assert.IsTrue(_settings.Groups[0].DefaultWarningEnabled);
            CollectionAssert.Contains(_settings.Groups[0].KnownCategories, "GraphLocalization.AutoBuild");
        }

        [Test]
        public void EnsureCategoryKnown_CalledTwiceForSameCategory_AddsOnlyOneKnownCategoryEntry()
        {
            _settings.EnsureCategoryKnown("GraphLocalization.AutoBuild");
            _settings.EnsureCategoryKnown("GraphLocalization.AutoBuild");

            Assert.AreEqual(1, _settings.Groups.Count);
            Assert.AreEqual(1, _settings.Groups[0].KnownCategories.Count);
        }

        [Test]
        public void EnsureCategoryKnown_TwoCategoriesSameGroup_ShareOneGroupEntry()
        {
            _settings.EnsureCategoryKnown("GraphSave.Store");
            _settings.EnsureCategoryKnown("GraphSave.Loader");

            Assert.AreEqual(1, _settings.Groups.Count);
            Assert.AreEqual(2, _settings.Groups[0].KnownCategories.Count);
        }

        [Test]
        public void SetGroupInfoEnabled_NewCategoryUnderSameGroup_InheritsGroupDefault()
        {
            // The exact gap this redesign closes: a lib silenced as a whole must stay silenced for
            // categories added to it later, without the user ever having to revisit the settings.
            _settings.EnsureCategoryKnown("GraphSave.Store");
            _settings.SetGroupInfoEnabled("GraphSave", false);

            Assert.IsFalse(_settings.IsInfoEnabled("GraphSave.BrandNewCategory"));
        }

        [Test]
        public void SetCategoryInfoEnabled_DivergingFromGroupDefault_OnlyAffectsThatCategory()
        {
            _settings.EnsureCategoryKnown("GraphSave.Store");
            _settings.EnsureCategoryKnown("GraphSave.Loader");

            _settings.SetCategoryInfoEnabled("GraphSave.Store", false);

            Assert.IsFalse(_settings.IsInfoEnabled("GraphSave.Store"));
            Assert.IsTrue(_settings.IsInfoEnabled("GraphSave.Loader"));
            Assert.AreEqual(1, _settings.Overrides.Count);
        }

        [Test]
        public void SetCategoryInfoEnabled_MatchingGroupDefaultAgain_ClearsTheOverride()
        {
            _settings.EnsureCategoryKnown("GraphSave.Store");
            _settings.SetCategoryInfoEnabled("GraphSave.Store", false);

            _settings.SetCategoryInfoEnabled("GraphSave.Store", true);

            Assert.AreEqual(0, _settings.Overrides.Count);
            Assert.IsTrue(_settings.IsInfoEnabled("GraphSave.Store"));
        }

        [Test]
        public void SetGroupInfoEnabled_PrunesOverridesThatNoLongerDiverge()
        {
            _settings.EnsureCategoryKnown("GraphSave.Store");
            _settings.SetCategoryInfoEnabled("GraphSave.Store", false);
            Assert.AreEqual(1, _settings.Overrides.Count);

            _settings.SetGroupInfoEnabled("GraphSave", false);

            Assert.AreEqual(0, _settings.Overrides.Count);
            Assert.IsFalse(_settings.IsInfoEnabled("GraphSave.Store"));
        }

        [Test]
        public void SetCategoryWarningEnabled_IsIndependentOfInfo()
        {
            _settings.EnsureCategoryKnown("GraphSave.Store");
            _settings.SetCategoryWarningEnabled("GraphSave.Store", false);

            Assert.IsFalse(_settings.IsWarningEnabled("GraphSave.Store"));
            Assert.IsTrue(_settings.IsInfoEnabled("GraphSave.Store"), "Info toggle must be independent of Warning.");
        }
    }
}
