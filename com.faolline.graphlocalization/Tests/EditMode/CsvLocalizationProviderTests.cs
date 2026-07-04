using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;
using Faolline.GraphLocalization;

namespace Faolline.GraphLocalization.Tests
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

        [Test]
        public void SetLocale_ChangesCurrentLocale()
        {
            var p = new CsvLocalizationProvider(Csv, "en");
            Assert.AreEqual("en", p.CurrentLocale);
            p.SetLocale("fr");
            Assert.AreEqual("fr", p.CurrentLocale);
        }

        [Test]
        public void SetLocale_ResolvesInNewLocale()
        {
            var p = new CsvLocalizationProvider(Csv, "en");
            Assert.AreEqual("Hello", p.Resolve("dlg.hi", p.CurrentLocale));
            p.SetLocale("fr");
            Assert.AreEqual("Bonjour", p.Resolve("dlg.hi", p.CurrentLocale));
        }

        [Test]
        public void SetLocale_IgnoresNullOrEmpty()
        {
            var p = new CsvLocalizationProvider(Csv, "en");
            p.SetLocale(null);
            Assert.AreEqual("en", p.CurrentLocale);
            p.SetLocale("");
            Assert.AreEqual("en", p.CurrentLocale);
        }

        [Test]
        public void Resolve_QuotedFieldWithNewline_IsParsed()
        {
            var csv = "Key,en,fr\n" +
                      "dlg.multi,\"Hello\nfriend\",\"Bonjour\nl'ami\"\n" +
                      "dlg.next,After,Apres\n";
            var p = new CsvLocalizationProvider(csv, "en");
            Assert.AreEqual("Hello\nfriend", p.Resolve("dlg.multi", "en"));
            Assert.AreEqual("Bonjour\nl'ami", p.Resolve("dlg.multi", "fr"));
            Assert.AreEqual("After", p.Resolve("dlg.next", "en"), "The row after a multi-line field must still parse.");
        }

        [Test]
        public void Resolve_CrLfLineEndings_AndBlankLines_AreHandled()
        {
            var csv = "Key,en\r\n" +
                      "dlg.hi,Hello\r\n" +
                      "\r\n" +
                      "dlg.bye,Goodbye\r\n";
            var p = new CsvLocalizationProvider(csv, "en");
            Assert.AreEqual("Hello", p.Resolve("dlg.hi", "en"));
            Assert.AreEqual("Goodbye", p.Resolve("dlg.bye", "en"));
        }

        [Test]
        public void LocalizationSettings_SetLocale_PropagatesToProvider()
        {
            var p = new CsvLocalizationProvider(Csv, "en");
            var settings = new LocalizationSettings(p, "en");
            settings.CurrentLocale = "fr";
            Assert.AreEqual("fr", p.CurrentLocale, "Setting CurrentLocale on LocalizationSettings must propagate to the provider");
        }
    }
}
