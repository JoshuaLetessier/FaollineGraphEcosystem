using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;
using Faolline.GraphLocalization;

namespace Faolline.GraphLocalization.Tests
{
    /// <summary>Missing keys/locales fall back safely, never empty; reacting loudly is StrictMode's job.</summary>
    public class LocalizationFallbackTests
    {
        [Test]
        public void MissingKey_ReturnsDefinedFallback_Silently()
        {
            var provider = new CsvLocalizationProvider("Key,en\ndlg.hi,Hello\n", "en");
            // The provider returns the #key marker WITHOUT logging — reacting to a missing key belongs to
            // the StrictMode-aware layer (LocalizationSettings / DialoguePresenter), so Permissive is silent.
            var resolved = provider.Resolve("dlg.absent", "en");
            Assert.AreEqual("#dlg.absent", resolved);
            Assert.IsNotEmpty(resolved);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void MissingLocaleColumn_FallsBack()
        {
            var provider = new CsvLocalizationProvider("Key,en\ndlg.hi,Hello\n", "en");
            // "de" column absent → fallback rather than empty/broken (no provider-side log)
            Assert.AreEqual("#dlg.hi", provider.Resolve("dlg.hi", "de"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Settings_Permissive_ReturnsMarkerSilently()
        {
            var settings = new LocalizationSettings(new CsvLocalizationProvider("Key,en\ndlg.hi,Hello\n", "en"), "en")
            {
                StrictMode = LocalizationStrictMode.Permissive
            };
            Assert.AreEqual("#dlg.absent", settings.Resolve("dlg.absent"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Settings_Audit_WarnsOncePerKey_ThenStaysQuiet()
        {
            var settings = new LocalizationSettings(new CsvLocalizationProvider("Key,en\ndlg.hi,Hello\n", "en"), "en")
            {
                StrictMode = LocalizationStrictMode.Audit
            };
            LogAssert.Expect(LogType.Warning, new Regex("Missing localization key 'dlg.absent'"));
            Assert.AreEqual("#dlg.absent", settings.Resolve("dlg.absent"));
            // Second resolve of the SAME key must not warn again (deduped per key+locale).
            Assert.AreEqual("#dlg.absent", settings.Resolve("dlg.absent"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Settings_Strict_ThrowsOnMissingKey()
        {
            var settings = new LocalizationSettings(new CsvLocalizationProvider("Key,en\ndlg.hi,Hello\n", "en"), "en")
            {
                StrictMode = LocalizationStrictMode.Strict
            };
            var ex = Assert.Throws<LocalizationException>(() => settings.Resolve("dlg.absent"));
            Assert.AreEqual("dlg.absent", ex.Key);
            Assert.AreEqual("en", ex.Locale);
        }

        [Test]
        public void Settings_PresentKey_ResolvesInEveryMode()
        {
            foreach (var mode in new[] { LocalizationStrictMode.Permissive, LocalizationStrictMode.Audit, LocalizationStrictMode.Strict })
            {
                var settings = new LocalizationSettings(new CsvLocalizationProvider("Key,en\ndlg.hi,Hello\n", "en"), "en")
                {
                    StrictMode = mode
                };
                Assert.AreEqual("Hello", settings.Resolve("dlg.hi"), $"mode {mode}");
            }
            LogAssert.NoUnexpectedReceived();
        }
    }
}
