using Faolline.GraphLocalization;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>P1 â€” project-wide localization mode selection (CSV vs. Unity Localization).</summary>
    public class LocalizationModeTests
    {
        [Test]
        public void LocalizationSettingsAsset_CsvMode_CreatesCsvProvider()
        {
            var asset = ScriptableObject.CreateInstance<LocalizationSettingsAsset>();
            try
            {
                var settings = asset.CreateSettings("en");
                Assert.IsInstanceOf<CsvLocalizationProvider>(settings.Provider);
            }
            finally { Object.DestroyImmediate(asset); }
        }

        [Test]
        public void LocalizationSettings_Constructor_AcceptsProviderAndLocale()
        {
            var provider = new CsvLocalizationProvider("Key,en\ntest,hello\n", "en");
            var settings = new LocalizationSettings(provider, "fr");

            Assert.AreSame(provider, settings.Provider);
            Assert.AreEqual("fr", settings.CurrentLocale);
        }

        [Test]
        public void LocalizationSettings_Resolve_UsesProviderAndLocale()
        {
            var provider = new CsvLocalizationProvider("Key,en,fr\ntest,hello,bonjour\n", "en");
            var settings = new LocalizationSettings(provider, "fr");

            var result = settings.Resolve("test");
            Assert.AreEqual("bonjour", result, "Should resolve via provider in the current locale.");
        }
    }
}
