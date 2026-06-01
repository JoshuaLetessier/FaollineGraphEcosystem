using Faolline.GraphLocalization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>EditMode tests for the default CSV localization provider.</summary>
    public class CsvLocalizationProviderTests
    {
        private const string Csv = "Key,en,fr\n" +
                                   "dlg.hi,Hello,Bonjour\n" +
                                   "dlg.bye,Goodbye,Au revoir\n";

        [Test]
        public void Resolve_ReturnsTextForLocale()
        {
            var p = new CsvLocalizationProvider(Csv, "en");
            Assert.AreEqual("Hello", p.Resolve("dlg.hi", "en"));
            Assert.AreEqual("Bonjour", p.Resolve("dlg.hi", "fr"));
            Assert.AreEqual("Au revoir", p.Resolve("dlg.bye", "fr"));
        }

        [Test]
        public void CurrentLocale_DefaultsToConstructorArg()
        {
            var p = new CsvLocalizationProvider(Csv, "fr");
            Assert.AreEqual("fr", p.CurrentLocale);
        }

        [Test]
        public void Resolve_MissingKey_ReturnsFallback_AndWarns()
        {
            var p = new CsvLocalizationProvider(Csv, "en");
            LogAssert.Expect(LogType.Warning, new Regex("not found"));
            Assert.AreEqual("#dlg.nope", p.Resolve("dlg.nope", "en"));
        }

        [Test]
        public void Resolve_QuotedFieldWithComma_IsParsed()
        {
            var csv = "Key,en\n" + "dlg.list,\"a, b, c\"\n";
            var p = new CsvLocalizationProvider(csv, "en");
            Assert.AreEqual("a, b, c", p.Resolve("dlg.list", "en"));
        }
    }
}
