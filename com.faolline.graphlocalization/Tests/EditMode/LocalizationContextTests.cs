using NUnit.Framework;
using Faolline.GraphLocalization;

namespace Faolline.GraphLocalization.Tests
{
    /// <summary>Unit tests for LocalizationContext — ambient accessor and safe default.</summary>
    public class LocalizationContextTests
    {
        [SetUp]
        public void SetUp() => LocalizationContext.Current = null;

        [TearDown]
        public void TearDown() => LocalizationContext.Current = null;

        [Test]
        public void Current_NeverNull_WhenNoAssetConfigured()
        {
            var settings = LocalizationContext.Current;
            Assert.IsNotNull(settings, "Current must never be null.");
        }

        [Test]
        public void DefaultSettings_FallBackToCsvProvider_WhenNoProviderConfigured()
        {
            // The genuine "nothing configured" path is a default-constructed LocalizationSettings; its
            // Provider lazily defaults to CSV. (LocalizationContext.Current intentionally honours the
            // project's LocalizationSettingsAsset, which may select the Unity provider.)
            Assert.IsInstanceOf<CsvLocalizationProvider>(new LocalizationSettings().Provider);
        }

        [Test]
        public void Current_CanBeSetExplicitly()
        {
            var provider = new CsvLocalizationProvider("Key,en\ntest,hello\n", "en");
            var settings = new LocalizationSettings(provider, "en");

            LocalizationContext.Current = settings;

            Assert.AreSame(settings, LocalizationContext.Current);
        }

        [Test]
        public void Resolve_DelegatesToCurrent()
        {
            var provider = new CsvLocalizationProvider("Key,en\ngreeting,Hello\n", "en");
            LocalizationContext.Current = new LocalizationSettings(provider, "en");

            Assert.AreEqual("Hello", LocalizationContext.Resolve("greeting"));
        }

        [Test]
        public void Resolve_ReturnsFallback_ForMissingKey()
        {
            LocalizationContext.Current = new LocalizationSettings(
                new CsvLocalizationProvider("Key,en\n", "en"), "en");

            var result = LocalizationContext.Resolve("missing_key");
            Assert.AreEqual("#missing_key", result);
        }
    }
}
