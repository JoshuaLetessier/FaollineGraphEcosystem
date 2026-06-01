using Faolline.GraphLocalization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;
using Faolline.GraphCore;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>EditMode tests: end signalling and degenerate-graph safety.</summary>
    public class DialoguePlayerEndTests
    {
        [Test]
        public void End_FiresExactlyOnce_WithReason()
        {
            var graph = DialoguePlayerTestGraphs.Linear();
            try
            {
                var provider = new CsvLocalizationProvider(DialoguePlayerTestGraphs.Csv, "en");
                var player = new DialoguePlayer(graph, new DialogueContext(), provider);

                int ended = 0;
                EndReason reason = EndReason.Cancelled;
                player.OnEnded += s => { ended++; reason = s.EndReason; };

                player.Start();    // pauses at line
                player.Advance();  // line â†’ end

                Assert.AreEqual(1, ended, "OnEnded must fire exactly once.");
                Assert.AreEqual(EndReason.Completed, reason);
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void MissingEntry_LogsError_NoCrash()
        {
            var graph = ScriptableObject.CreateInstance<DialogueGraph>();
            try
            {
                var player = new DialoguePlayer(graph, new DialogueContext(),
                    new CsvLocalizationProvider(string.Empty, "en"));

                LogAssert.Expect(LogType.Error, new Regex("Cannot start dialogue"));
                player.Start();

                Assert.AreEqual(RunnerState.Idle, player.State);
            }
            finally { Object.DestroyImmediate(graph); }
        }
    }
}
