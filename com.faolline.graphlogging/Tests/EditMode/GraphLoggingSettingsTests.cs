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
        public void EnsureCategoryKnown_AddsEntry_DefaultingBothLevelsEnabled()
        {
            _settings.EnsureCategoryKnown("GraphLocalization.AutoBuild");

            Assert.AreEqual(1, _settings.Categories.Count);
            Assert.AreEqual("GraphLocalization.AutoBuild", _settings.Categories[0].Category);
            Assert.IsTrue(_settings.Categories[0].InfoEnabled);
            Assert.IsTrue(_settings.Categories[0].WarningEnabled);
        }

        [Test]
        public void EnsureCategoryKnown_CalledTwiceForSameCategory_AddsOnlyOneEntry()
        {
            _settings.EnsureCategoryKnown("GraphLocalization.AutoBuild");
            _settings.EnsureCategoryKnown("GraphLocalization.AutoBuild");

            Assert.AreEqual(1, _settings.Categories.Count);
        }

        [Test]
        public void IsInfoEnabled_AfterTogglingOff_ReflectsTheStoredValue()
        {
            _settings.EnsureCategoryKnown("GraphLocalization.AutoBuild");
            _settings.Categories[0].InfoEnabled = false;

            Assert.IsFalse(_settings.IsInfoEnabled("GraphLocalization.AutoBuild"));
            Assert.IsTrue(_settings.IsWarningEnabled("GraphLocalization.AutoBuild"), "Warning toggle must be independent of Info.");
        }
    }
}
