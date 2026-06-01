using Faolline.GraphLocalization;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>EditMode tests: choice presentation, availability, and routing through the player.</summary>
    public class DialoguePlayerChoiceTests
    {
        [Test]
        public void Choice_ListsOptions_WithLocalizedLabels()
        {
            var graph = DialoguePlayerTestGraphs.WithChoice();
            try
            {
                var provider = new CsvLocalizationProvider(DialoguePlayerTestGraphs.Csv, "en");
                var player = new DialoguePlayer(graph, new DialogueContext(), provider);

                ChoiceStep choices = null;
                player.OnChoices += s => choices = s;
                player.Start();

                Assert.IsNotNull(choices, "OnChoices should fire at the choice node.");
                Assert.AreEqual(2, choices.Options.Count);
                Assert.AreEqual("Yes", choices.Options[0].ResolvedLabel);
                Assert.AreEqual("No", choices.Options[1].ResolvedLabel);
                Assert.IsTrue(choices.Options[0].Available);
                Assert.IsTrue(choices.Options[1].Available);
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void Choose_AvailableOption_RoutesAndEnds()
        {
            var graph = DialoguePlayerTestGraphs.WithChoice();
            try
            {
                var provider = new CsvLocalizationProvider(DialoguePlayerTestGraphs.Csv, "en");
                var player = new DialoguePlayer(graph, new DialogueContext(), provider);

                EndStep end = null;
                player.OnEnded += s => end = s;
                player.Start();
                player.Choose("a");

                Assert.IsNotNull(end, "Choosing an available option should reach an end.");
                Assert.AreEqual(EndReason.Completed, end.EndReason);
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void GatedOption_IsUnavailable_AndChooseIsNoOp()
        {
            var gate = ScriptableObject.CreateInstance<AlwaysFalseCondition>();
            var graph = DialoguePlayerTestGraphs.WithChoice(gate);
            try
            {
                var provider = new CsvLocalizationProvider(DialoguePlayerTestGraphs.Csv, "en");
                var player = new DialoguePlayer(graph, new DialogueContext(), provider);

                ChoiceStep choices = null;
                EndStep end = null;
                player.OnChoices += s => choices = s;
                player.OnEnded += s => end = s;
                player.Start();

                Assert.IsFalse(choices.Options[1].Available, "Option 'b' is gated false.");

                player.Choose("b"); // unavailable â†’ no-op
                Assert.IsNull(end, "Choosing an unavailable option must not advance.");
                Assert.AreEqual(RunnerState.NodeReady, player.State);
            }
            finally { Object.DestroyImmediate(gate); Object.DestroyImmediate(graph); }
        }
    }
}
