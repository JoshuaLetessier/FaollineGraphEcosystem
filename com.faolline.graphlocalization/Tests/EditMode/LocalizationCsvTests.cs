using NUnit.Framework;

namespace Faolline.GraphLocalization.Tests
{
    /// <summary>The shared RFC4180 parser/escaper (052, research R9) — replaces the two "kept in sync" copies.</summary>
    public class LocalizationCsvTests
    {
        [Test]
        public void ParseRecords_PlainRows()
        {
            var r = LocalizationCsv.ParseRecords("Key,en\nline_a,Hello\n");
            Assert.AreEqual(2, r.Count);
            CollectionAssert.AreEqual(new[] { "Key", "en" }, r[0]);
            CollectionAssert.AreEqual(new[] { "line_a", "Hello" }, r[1]);
        }

        [Test]
        public void ParseRecords_CommaInsideQuotes()
            => CollectionAssert.AreEqual(new[] { "k", "Hello, friend" }, LocalizationCsv.ParseRecords("k,\"Hello, friend\"")[0]);

        [Test]
        public void ParseRecords_DoubledQuotes()
            => CollectionAssert.AreEqual(new[] { "k", "say \"hi\"" }, LocalizationCsv.ParseRecords("k,\"say \"\"hi\"\"\"")[0]);

        [Test]
        public void ParseRecords_NewlineInsideQuotes_StaysOneRecord()
        {
            var r = LocalizationCsv.ParseRecords("Key,en\nk,\"line1\nline2\"\nk2,x\n");
            Assert.AreEqual(3, r.Count);
            Assert.AreEqual("line1\nline2", r[1][1]);
        }

        [Test]
        public void ParseRecords_CrLfAndLoneCr_EndRecords()
        {
            Assert.AreEqual(3, LocalizationCsv.ParseRecords("a,b\r\nc,d\re,f").Count);
        }

        [Test]
        public void ParseRecords_BlankLinesSkipped()
            => Assert.AreEqual(2, LocalizationCsv.ParseRecords("a,b\n\n   \nc,d\n").Count);

        [Test]
        public void ParseRecords_TrailingRecordWithoutNewline()
            => CollectionAssert.AreEqual(new[] { "c", "d" }, LocalizationCsv.ParseRecords("a,b\nc,d")[1]);

        [TestCase(null)]
        [TestCase("")]
        public void ParseRecords_NullOrEmpty_IsEmpty(string csv)
            => Assert.IsEmpty(LocalizationCsv.ParseRecords(csv));

        [Test]
        public void Escape_PlainText_Unchanged()
            => Assert.AreEqual("Hello", LocalizationCsv.Escape("Hello"));

        [Test]
        public void Escape_Null_IsEmpty()
            => Assert.AreEqual(string.Empty, LocalizationCsv.Escape(null));

        [TestCase("a,b")]
        [TestCase("say \"hi\"")]
        [TestCase("line1\nline2")]
        [TestCase("line1\r\nline2")]
        public void Escape_QuotesAndRoundTrips(string value)
        {
            var escaped = LocalizationCsv.Escape(value);
            StringAssert.StartsWith("\"", escaped);
            var parsed = LocalizationCsv.ParseRecords("k," + escaped + "\n");
            Assert.AreEqual(value, parsed[0][1]);
        }
    }
}
