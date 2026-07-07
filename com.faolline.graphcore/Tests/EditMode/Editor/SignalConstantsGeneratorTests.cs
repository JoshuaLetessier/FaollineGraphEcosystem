using System.Collections.Generic;
using NUnit.Framework;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// The pure generation core: display-name → C# symbol sanitization, GUID as the value, and blocking
    /// collision detection. No AssetDatabase — exercises <see cref="SignalConstantsGenerator.TryBuildSource"/>
    /// and <see cref="SignalConstantsGenerator.Sanitize"/> directly.
    /// </summary>
    public class SignalConstantsGeneratorTests
    {
        [TestCase("player interacted", "PlayerInteracted")]
        [TestCase("flow_complete", "FlowComplete")]
        [TestCase("door-opened", "DoorOpened")]
        [TestCase("1st clue", "_1stClue")]
        [TestCase("already Pascal", "AlreadyPascal")]
        [TestCase("  spaced  out  ", "SpacedOut")]
        [TestCase("123", "_123")]
        [TestCase("", "_")]
        [TestCase("!!!", "_")]
        public void Sanitize_ProducesValidPascalIdentifier(string display, string expected)
        {
            Assert.AreEqual(expected, SignalConstantsGenerator.Sanitize(display));
        }

        [Test]
        public void TryBuildSource_EmitsSymbolFromNameAndGuidAsValue()
        {
            var signals = new List<(string, string)>
            {
                ("player interacted", "guid-aaa"),
                ("door-opened",       "guid-bbb"),
            };

            var ok = SignalConstantsGenerator.TryBuildSource(signals, out var source, out var errors);

            Assert.IsTrue(ok);
            Assert.IsEmpty(errors);
            StringAssert.Contains("public static class GraphSignals", source);
            StringAssert.Contains("public const string PlayerInteracted = \"guid-aaa\";", source);
            StringAssert.Contains("public const string DoorOpened = \"guid-bbb\";", source);
        }

        [Test]
        public void TryBuildSource_SymbolCollision_IsBlockingError_NoSource()
        {
            // Two DISTINCT signals whose display names sanitize to the same symbol must not be merged.
            var signals = new List<(string, string)>
            {
                ("player interacted", "guid-1"),
                ("Player Interacted", "guid-2"),   // → same symbol "PlayerInteracted"
            };

            var ok = SignalConstantsGenerator.TryBuildSource(signals, out var source, out var errors);

            Assert.IsFalse(ok, "a symbol collision must abort generation");
            Assert.IsNull(source);
            Assert.IsNotEmpty(errors);
            StringAssert.Contains("PlayerInteracted", errors[0]);
        }

        [Test]
        public void TryBuildSource_RenameChangesSymbolNotValue()
        {
            // The free-rename property: the same signal (same GUID) under a new display name yields a new
            // symbol but the SAME value — so data keeps matching while stale code breaks at compile.
            var before = new List<(string, string)> { ("boss defeated", "guid-x") };
            var after  = new List<(string, string)> { ("dragon slain",  "guid-x") };

            SignalConstantsGenerator.TryBuildSource(before, out var srcBefore, out _);
            SignalConstantsGenerator.TryBuildSource(after,  out var srcAfter,  out _);

            StringAssert.Contains("public const string BossDefeated = \"guid-x\";", srcBefore);
            StringAssert.Contains("public const string DragonSlain = \"guid-x\";", srcAfter);
            StringAssert.DoesNotContain("BossDefeated", srcAfter);   // old symbol gone → stale code fails to compile
        }
    }
}
