using System.Collections.Generic;
using NUnit.Framework;
using Faolline.GraphLocalization.Editor;

namespace Faolline.GraphLocalization.Tests
{
    /// <summary>
    /// 052 US3 (research R10): one translation CSV of global keys (dialogue-studio's single speakers.csv) is split
    /// by key into per-collection CSVs. A key held by no collection, or by several, is a failure — never guessed.
    /// </summary>
    public class GlobalKeyCsvRouterTests
    {
        private static System.Func<string, IReadOnlyList<string>> Index(params (string key, string collection)[] entries)
        {
            var map = new Dictionary<string, List<string>>();
            foreach (var (key, collection) in entries)
            {
                if (!map.TryGetValue(key, out var list)) map[key] = list = new List<string>();
                list.Add(collection);
            }
            return key => map.TryGetValue(key, out var list) ? list : (IReadOnlyList<string>)new string[0];
        }

        [Test]
        public void Rows_AreRoutedToTheCollectionHoldingTheirKey_HeaderKept()
        {
            var csv = "Key,en,fr\nspeaker_a,Innkeeper,Aubergiste\nspeaker_b,Smith,Forgeron\nspeaker_c,Guard,Garde\n";
            var result = GlobalKeyCsvRouter.Route(csv, Index(
                ("speaker_a", "Lib_Speakers_A_Text"), ("speaker_b", "Lib_Speakers_B_Text"), ("speaker_c", "Lib_Speakers_A_Text")));

            Assert.IsEmpty(result.Failures);
            Assert.AreEqual(2, result.CsvByCollection.Count);

            var a = LocalizationCsv.ParseRecords(result.CsvByCollection["Lib_Speakers_A_Text"]);
            CollectionAssert.AreEqual(new[] { "Key", "en", "fr" }, a[0]);
            Assert.AreEqual(3, a.Count);
            Assert.AreEqual("speaker_a", a[1][0]);
            Assert.AreEqual("speaker_c", a[2][0]);

            var b = LocalizationCsv.ParseRecords(result.CsvByCollection["Lib_Speakers_B_Text"]);
            CollectionAssert.AreEqual(new[] { "speaker_b", "Smith", "Forgeron" }, b[1]);
        }

        [Test]
        public void QuotedMultilineValue_SurvivesRouting()
        {
            var csv = "Key,en\nspeaker_a,\"Line one\nLine \"\"two\"\", end\"\n";
            var result = GlobalKeyCsvRouter.Route(csv, Index(("speaker_a", "T_Text")));

            var parsed = LocalizationCsv.ParseRecords(result.CsvByCollection["T_Text"]);
            Assert.AreEqual("Line one\nLine \"two\", end", parsed[1][1]);
        }

        [Test]
        public void UnknownKey_IsAFailureNamingTheKey_OtherRowsStillRouted()
        {
            var csv = "Key,en\nspeaker_a,A\nspeaker_ghost,G\n";
            var result = GlobalKeyCsvRouter.Route(csv, Index(("speaker_a", "T_Text")));

            Assert.AreEqual(1, result.Failures.Count);
            StringAssert.Contains("speaker_ghost", result.Failures[0]);
            Assert.AreEqual(1, result.CsvByCollection.Count);
            Assert.AreEqual(2, LocalizationCsv.ParseRecords(result.CsvByCollection["T_Text"]).Count);
        }

        [Test]
        public void KeyHeldBySeveralCollections_IsAnAmbiguityFailureNamingThem()
        {
            var csv = "Key,en\nspeaker_a,A\n";
            var result = GlobalKeyCsvRouter.Route(csv, Index(("speaker_a", "T1_Text"), ("speaker_a", "T2_Text")));

            Assert.AreEqual(1, result.Failures.Count);
            StringAssert.Contains("speaker_a", result.Failures[0]);
            StringAssert.Contains("T1_Text", result.Failures[0]);
            StringAssert.Contains("T2_Text", result.Failures[0]);
            Assert.IsEmpty(result.CsvByCollection);
        }

        // dialogue-studio (and this lib's own CSV exporter) name locale columns by bare code ("fr"); Unity
        // Localization's own CSV export names them "French(fr)". Both must map to the locale code.
        [TestCase("fr", "fr")]
        [TestCase(" en ", "en")]
        [TestCase("French(fr)", "fr")]
        [TestCase("French (fr)", "fr")]
        [TestCase("Chinese (Simplified)(zh-Hans)", "zh-Hans")]
        public void LocaleCodeOfColumn_AcceptsBareCodeAndUnityExportName(string column, string expected)
            => Assert.AreEqual(expected, GlobalKeyCsvRouter.LocaleCodeOfColumn(column));

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Key,en\n")]
        public void EmptyOrHeaderOnly_RoutesNothing(string csv)
        {
            var result = GlobalKeyCsvRouter.Route(csv, Index());
            Assert.IsEmpty(result.CsvByCollection);
            Assert.IsEmpty(result.Failures);
        }
    }
}
