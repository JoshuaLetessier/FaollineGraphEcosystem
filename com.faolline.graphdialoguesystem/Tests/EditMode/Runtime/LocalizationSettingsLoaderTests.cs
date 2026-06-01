using Faolline.GraphLocalization;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>P1 â€” localization settings auto-discovery and loading.</summary>
    public class LocalizationSettingsLoaderTests
    {
        [Test]
        public void LocalizationContext_Current_FallsBackToDefaultWhenNoAsset()
        {
            // Clear any previously loaded asset from the context
            LocalizationContext.Current = null;

            var settings = LocalizationContext.Current;
            Assert.IsNotNull(settings, "LocalizationContext.Current should never be null.");
            Assert.IsInstanceOf<CsvLocalizationProvider>(settings.Provider, "Fallback is CSV when no asset found.");
        }

        [Test]
        public void LocalizationContext_CanBeSetExplicitly()
        {
            var provider = new CsvLocalizationProvider("Key,en\n", "en");
            var custom = new LocalizationSettings(provider, "en");

            LocalizationContext.Current = custom;
            Assert.AreSame(custom, LocalizationContext.Current);
        }
    }
}
