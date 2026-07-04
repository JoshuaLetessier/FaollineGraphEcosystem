using System.Collections.Generic;
using NUnit.Framework;
using Faolline.GraphLocalization.Editor;

namespace Faolline.GraphLocalization.Tests
{
    /// <summary>Tests for the pure CSV build core (merge, prefill, orphan removal, quoting).</summary>
    public class CsvLocalizationExporterTests
    {
        private static List<(string, string)> Keys(params (string key, string hint)[] items)
            => new List<(string, string)>(items);

        [Test]
        public void BuildCsv_NewFile_PrefillsSourceLocaleFromHint()
        {
            var desired = Keys(("line_a", "Hello"), ("line_b", "Bye"));
            var csv = CsvLocalizationExporter.BuildCsv(null, desired,
                new[] { "en", "fr" }, "en", out var coverage, out var removed);

            StringAssert.Contains("Key,en,fr", csv);
            StringAssert.Contains("line_a,Hello,", csv);
            StringAssert.Contains("line_b,Bye,", csv);
            Assert.AreEqual(0, removed);

            // en (source) fully filled from hints; fr empty.
            Assert.AreEqual(("en", 2, 2), coverage[0]);
            Assert.AreEqual(("fr", 0, 2), coverage[1]);
        }

        [Test]
        public void BuildCsv_PreservesExistingTranslations()
        {
            var existing = "Key,en,fr\nline_a,Hello,Bonjour\n";
            var desired = Keys(("line_a", "Hello"));

            var csv = CsvLocalizationExporter.BuildCsv(existing, desired,
                new[] { "en", "fr" }, "en", out var coverage, out _);

            StringAssert.Contains("line_a,Hello,Bonjour", csv);
            Assert.AreEqual(("fr", 1, 1), coverage[1], "Existing fr translation preserved.");
        }

        [Test]
        public void BuildCsv_DoesNotOverwriteSourceWhenAlreadyTranslated()
        {
            var existing = "Key,en\nline_a,Custom EN\n";
            var desired = Keys(("line_a", "Hint EN"));

            var csv = CsvLocalizationExporter.BuildCsv(existing, desired,
                new[] { "en" }, "en", out _, out _);

            StringAssert.Contains("line_a,Custom EN", csv);
            StringAssert.DoesNotContain("Hint EN", csv);
        }

        [Test]
        public void BuildCsv_RemovesOrphanKeys()
        {
            var existing = "Key,en\nline_a,Hello\nline_old,Stale\n";
            var desired = Keys(("line_a", "Hello"));

            var csv = CsvLocalizationExporter.BuildCsv(existing, desired,
                new[] { "en" }, "en", out _, out var removed);

            StringAssert.Contains("line_a,Hello", csv);
            StringAssert.DoesNotContain("line_old", csv);
            Assert.AreEqual(1, removed);
        }

        [Test]
        public void BuildCsv_QuotesFieldsWithCommas()
        {
            var desired = Keys(("line_a", "Hello, friend"));
            var csv = CsvLocalizationExporter.BuildCsv(null, desired,
                new[] { "en" }, "en", out _, out _);

            StringAssert.Contains("line_a,\"Hello, friend\"", csv);
        }

        [Test]
        public void BuildCsv_RoundTripsThroughCsvProvider()
        {
            var desired = Keys(("line_a", "Hello"), ("line_b", "Bye"));
            var csv = CsvLocalizationExporter.BuildCsv(null, desired,
                new[] { "en", "fr" }, "en", out _, out _);

            // The generated CSV must be consumable by the runtime provider.
            var provider = new CsvLocalizationProvider(csv, "en");
            Assert.AreEqual("Hello", provider.Resolve("line_a", "en"));
            Assert.AreEqual("Bye", provider.Resolve("line_b", "en"));
        }

        [Test]
        public void BuildCsv_OnlyEmitsRequestedLocaleColumns()
        {
            var existing = "Key,en,fr,de\nline_a,Hello,Bonjour,Hallo\n";
            var desired = Keys(("line_a", "Hello"));

            // Drop "de" from the configured locales — its column must not appear.
            var csv = CsvLocalizationExporter.BuildCsv(existing, desired,
                new[] { "en", "fr" }, "en", out _, out _);

            StringAssert.Contains("Key,en,fr", csv);
            StringAssert.DoesNotContain("Hallo", csv);
        }

        [Test]
        public void BuildCsv_MultiLineValue_SurvivesMergeRebuild()
        {
            // A translation containing a newline (multi-line dialogue text, or a translator's
            // spreadsheet cell) must survive the merge-preserve pass of the next rebuild.
            var desired = Keys(("line_a", "Hello\nfriend"), ("line_b", "Bye"));
            var firstBuild = CsvLocalizationExporter.BuildCsv(null, desired,
                new[] { "en" }, "en", out _, out _);

            var rebuild = CsvLocalizationExporter.BuildCsv(firstBuild, desired,
                new[] { "en" }, "en", out var coverage, out var removed);

            Assert.AreEqual(0, removed, "The multi-line row must not be read as extra orphan rows.");
            Assert.AreEqual(("en", 2, 2), coverage[0]);
            StringAssert.Contains("\"Hello\nfriend\"", rebuild);
        }

        [Test]
        public void BuildCsv_MultiLineValue_RoundTripsThroughCsvProvider()
        {
            var desired = Keys(("line_a", "Hello\nfriend"));
            var csv = CsvLocalizationExporter.BuildCsv(null, desired,
                new[] { "en" }, "en", out _, out _);

            var provider = new CsvLocalizationProvider(csv, "en");
            Assert.AreEqual("Hello\nfriend", provider.Resolve("line_a", "en"));
        }

        [Test]
        public void BuildCsv_CarriageReturnValue_IsQuotedAndPreserved()
        {
            var desired = Keys(("line_a", "Hello\r\nfriend"));
            var csv = CsvLocalizationExporter.BuildCsv(null, desired,
                new[] { "en" }, "en", out _, out _);

            StringAssert.Contains("\"Hello\r\nfriend\"", csv);
            var rebuild = CsvLocalizationExporter.BuildCsv(csv, desired,
                new[] { "en" }, "en", out var coverage, out var removed);
            Assert.AreEqual(0, removed);
            Assert.AreEqual(("en", 1, 1), coverage[0]);
        }
    }
}
