using Faolline.GraphLocalization;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>
    /// US4 â€” the same graph resolves through any <see cref="ILocalizationProvider"/>. A stand-in
    /// "engine" provider (implementing the same contract) proves the adapter seam without requiring
    /// com.unity.localization to be installed in the test run.
    /// </summary>
    public class ProviderSwapTests
    {
        // Minimal stand-in for an engine-backed provider (e.g. Unity Localization).
        private sealed class StubEngineProvider : ILocalizationProvider
        {
            public string CurrentLocale => "en";
            public void SetLocale(string locale) { }
            public string Resolve(string key, string locale) => key == "line_l" ? "EngineHello" : $"#{key}";
        }

        [Test]
        public void SameGraph_ResolvesThroughEitherProvider()
        {
            var graphA = DialoguePlayerTestGraphs.Linear();
            var graphB = DialoguePlayerTestGraphs.Linear();
            try
            {
                var csv = new DialoguePlayer(graphA, new DialogueContext(),
                    new CsvLocalizationProvider(DialoguePlayerTestGraphs.Csv, "en"));
                var engine = new DialoguePlayer(graphB, new DialogueContext(), new StubEngineProvider());

                string csvText = null, engineText = null;
                csv.OnLine += s => csvText = s.ResolvedText;
                engine.OnLine += s => engineText = s.ResolvedText;

                csv.Start();
                engine.Start();

                Assert.AreEqual("Hello", csvText, "CSV provider resolves its table.");
                Assert.AreEqual("EngineHello", engineText, "Engine provider resolves the same key/graph.");
            }
            finally { Object.DestroyImmediate(graphA); Object.DestroyImmediate(graphB); }
        }
    }
}
