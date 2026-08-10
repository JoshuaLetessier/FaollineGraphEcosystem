using NUnit.Framework;

namespace Faolline.GraphImport.Editor.Tests
{
    public class BatchArgsTests
    {
        [Test]
        public void Parse_FlagValuePairs_MapsEachFlagToItsValue()
        {
            var args = BatchArgs.Parse(new[] { "Unity.exe", "-batchmode", "-mappingJson", "mapping.json", "-QuetesCsv", "Quetes.csv" });

            Assert.AreEqual("mapping.json", args["-mappingJson"]);
            Assert.AreEqual("Quetes.csv", args["-QuetesCsv"]);
        }

        [Test]
        public void Parse_TrailingFlagWithNoValue_IsIgnoredRatherThanThrowing()
        {
            var args = BatchArgs.Parse(new[] { "Unity.exe", "-mappingJson", "mapping.json", "-quit" });

            Assert.AreEqual("mapping.json", args["-mappingJson"]);
            Assert.IsFalse(args.ContainsKey("-quit"));
        }

        [Test]
        public void Parse_AdjacentValuelessFlags_TreatsTheSecondAsTheFirstsValue()
        {
            // Every non-final token starting with "-" is treated as a flag taking the next token as
            // its value — "-batchmode" absorbs "-nographics" as its value here. This is a known shape
            // limitation (not a bug to fix): every batch entry point's OWN flags always take a real
            // value, so this only ever bites Unity's own valueless launch flags, which callers already
            // place before this project's flags on the command line.
            var args = BatchArgs.Parse(new[] { "Unity.exe", "-batchmode", "-nographics" });

            Assert.AreEqual("-nographics", args["-batchmode"]);
        }
    }
}
