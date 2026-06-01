using Faolline.GraphLocalization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>P2a-iii â€” runtime reaction to missing keys per LocalizationStrictMode.</summary>
    public class DialoguePlayerStrictModeTests
    {
        // Empty table â†’ the Linear graph's line key "line_l" cannot be resolved.
        private static CsvLocalizationProvider EmptyProvider() => new CsvLocalizationProvider("Key,en\n", "en");

        [Test]
        public void Permissive_UsesFallback_NoThrow_NoAudit()
        {
            var graph = DialoguePlayerTestGraphs.Linear();
            try
            {
                var player = new DialoguePlayer(graph, new DialogueContext(), EmptyProvider(),
                    strictMode: LocalizationStrictMode.Permissive);

                LineStep line = null;
                player.OnLine += s => line = s;
                player.Start();

                Assert.AreEqual("#line_l", line.ResolvedText, "Permissive returns the provider fallback.");
                Assert.AreEqual(0, player.MissingKeys.Count, "Permissive records nothing.");
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void Audit_RecordsMissingKey_AndRaisesEvent_NoThrow()
        {
            var graph = DialoguePlayerTestGraphs.Linear();
            try
            {
                var player = new DialoguePlayer(graph, new DialogueContext(), EmptyProvider(),
                    strictMode: LocalizationStrictMode.Audit);

                string raised = null;
                player.OnMissingKey += k => raised = k;

                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Missing localization key 'line_l'"));
                player.Start();

                Assert.AreEqual(1, player.MissingKeys.Count, "Audit records the missing key once.");
                Assert.AreEqual("line_l", player.MissingKeys[0]);
                Assert.AreEqual("line_l", raised, "OnMissingKey fires with the missing key.");
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void Strict_Throws_OnMissingKey()
        {
            var graph = DialoguePlayerTestGraphs.Linear();
            try
            {
                var player = new DialoguePlayer(graph, new DialogueContext(), EmptyProvider(),
                    strictMode: LocalizationStrictMode.Strict);

                var ex = Assert.Throws<LocalizationException>(() => player.Start());
                Assert.AreEqual("line_l", ex.Key);
            }
            finally { Object.DestroyImmediate(graph); }
        }
    }
}
