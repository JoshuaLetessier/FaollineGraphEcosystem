using System.Collections.Generic;
using NUnit.Framework;
using Faolline.GraphLocalization.Editor;

namespace Faolline.GraphLocalization.Tests
{
    /// <summary>
    /// CSV carry-over (052, research R9): when a key moves to another file of the same lib (speaker group
    /// change, graph rename, legacy migration), its existing translations follow it. Precedence per cell:
    /// the target file's own value, then the carried value, then the source hint.
    /// </summary>
    public class CsvLocalizationExporterCarryTests
    {
        private static readonly string[] Locales = { "en", "fr" };

        private static List<(string, string)> Keys(params (string key, string hint)[] items)
            => new List<(string, string)>(items);

        private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Carry(
            string key, params (string locale, string value)[] cells)
        {
            var byLocale = new Dictionary<string, string>();
            foreach (var (locale, value) in cells) byLocale[locale] = value;
            return new Dictionary<string, IReadOnlyDictionary<string, string>> { [key] = byLocale };
        }

        [Test]
        public void KeyAbsentFromFile_TakesEveryLocaleFromCarry()
        {
            var csv = CsvLocalizationExporter.BuildCsv(null, Keys(("speaker_a", "Hint")), Locales, "en",
                Carry("speaker_a", ("en", "Aubergiste"), ("fr", "Aubergiste FR")), out var coverage, out _);

            StringAssert.Contains("speaker_a,Aubergiste,Aubergiste FR", csv);
            Assert.AreEqual(("fr", 1, 1), coverage[1]);
        }

        [Test]
        public void ExistingNonEmptyCell_IsNeverOverwrittenByCarry()
        {
            var existing = "Key,en,fr\nspeaker_a,Mine,Le mien\n";
            var csv = CsvLocalizationExporter.BuildCsv(existing, Keys(("speaker_a", "Hint")), Locales, "en",
                Carry("speaker_a", ("en", "Other"), ("fr", "Autre")), out _, out _);

            StringAssert.Contains("speaker_a,Mine,Le mien", csv);
            StringAssert.DoesNotContain("Autre", csv);
        }

        [Test]
        public void ExistingEmptyCell_IsFilledFromCarry()
        {
            var existing = "Key,en,fr\nspeaker_a,Mine,\n";
            var csv = CsvLocalizationExporter.BuildCsv(existing, Keys(("speaker_a", "Hint")), Locales, "en",
                Carry("speaker_a", ("fr", "Le sien")), out _, out _);

            StringAssert.Contains("speaker_a,Mine,Le sien", csv);
        }

        [Test]
        public void SourceHint_OnlyWhenNeitherExistingNorCarryHasAValue()
        {
            var csv = CsvLocalizationExporter.BuildCsv(null, Keys(("speaker_a", "Hint"), ("speaker_b", "HintB")), Locales, "en",
                Carry("speaker_a", ("en", "Carried")), out _, out _);

            StringAssert.Contains("speaker_a,Carried,", csv);
            StringAssert.Contains("speaker_b,HintB,", csv);
            StringAssert.DoesNotContain("speaker_a,Hint,", csv);
        }

        [Test]
        public void NullCarry_IsIdenticalToTheClassicOverload()
        {
            var existing = "Key,en,fr\nline_a,Hello,Bonjour\nline_old,Stale,\n";
            var desired = Keys(("line_a", "Hello"), ("line_b", "Bye"));

            var classic = CsvLocalizationExporter.BuildCsv(existing, desired, Locales, "en", out var c1, out var r1);
            var withNull = CsvLocalizationExporter.BuildCsv(existing, desired, Locales, "en", null, out var c2, out var r2);

            Assert.AreEqual(classic, withNull);
            CollectionAssert.AreEqual(c1, c2);
            Assert.AreEqual(r1, r2);
        }
    }
}
