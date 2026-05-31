using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>US4 — missing keys/locales fall back safely, never empty.</summary>
    public class LocalizationFallbackTests
    {
        [Test]
        public void MissingKey_ReturnsDefinedFallback_AndWarns()
        {
            var provider = new CsvLocalizationProvider("Key,en\ndlg.hi,Hello\n", "en");
            LogAssert.Expect(LogType.Warning, new Regex("not found"));
            var resolved = provider.Resolve("dlg.absent", "en");
            Assert.AreEqual("#dlg.absent", resolved);
            Assert.IsNotEmpty(resolved);
        }

        [Test]
        public void MissingLocaleColumn_FallsBack()
        {
            var provider = new CsvLocalizationProvider("Key,en\ndlg.hi,Hello\n", "en");
            LogAssert.Expect(LogType.Warning, new Regex("not found"));
            // "de" column absent → fallback rather than empty/broken
            Assert.AreEqual("#dlg.hi", provider.Resolve("dlg.hi", "de"));
        }
    }
}
