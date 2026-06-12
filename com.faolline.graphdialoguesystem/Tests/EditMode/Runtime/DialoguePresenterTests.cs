using System.Linq;
using Faolline.GraphLocalization;
using Faolline.GraphCore;
using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>
    /// Slice 9 — the runner-agnostic presenter resolves a dialogue node owned by ANY runner (here a plain
    /// BaseRunner, not a DialoguePlayer) into the same LineStep/ChoiceStep the player emits.
    /// </summary>
    public class DialoguePresenterTests
    {
        private sealed class FalseCond : BaseCondition { public override bool Evaluate(BaseContext c) => false; }

        [Test]
        public void ResolveLine_OnExternalRunnerNode_ReturnsResolvedStep()
        {
            var g = DialoguePlayerTestGraphs.Linear();
            var speaker = ScriptableObject.CreateInstance<Speaker>();
            speaker.SpeakerId = "npc"; speaker.DisplayNameFallback = "NPC";
            try
            {
                var ctx = new BaseContext();
                var runner = new BaseRunner();
                runner.Start(g, ctx, DialogueExecutorRegistryFactory.Create());   // at start
                runner.Proceed();                                                  // start → line "l"
                Assert.IsInstanceOf<DialogueLineNodeData>(runner.CurrentNode);

                var presenter = new DialoguePresenter(
                    new CsvLocalizationProvider(DialoguePlayerTestGraphs.Csv, "en"), speakerLookup: _ => speaker);
                var step = presenter.Resolve(runner.CurrentNode, ctx) as LineStep;

                Assert.IsNotNull(step);
                Assert.AreEqual("Hello", step.ResolvedText);
                Assert.AreEqual("NPC", step.ResolvedSpeakerName);
                Assert.AreEqual("npc", step.SpeakerId);
            }
            finally { Object.DestroyImmediate(speaker); Object.DestroyImmediate(g); }
        }

        [Test]
        public void ResolveChoice_OptionsCarryLabelAndAvailability()
        {
            var gate = ScriptableObject.CreateInstance<FalseCond>();   // option "b" unavailable
            var g = DialoguePlayerTestGraphs.WithChoice(gate);
            try
            {
                var ctx = new BaseContext();
                var runner = new BaseRunner();
                runner.Start(g, ctx, DialogueExecutorRegistryFactory.Create());
                runner.Proceed();                                                  // start → choice
                Assert.IsInstanceOf<ChoiceNodeData>(runner.CurrentNode);

                var presenter = new DialoguePresenter(new CsvLocalizationProvider(DialoguePlayerTestGraphs.Csv, "en"));
                var step = presenter.Resolve(runner.CurrentNode, ctx) as ChoiceStep;

                Assert.IsNotNull(step);
                Assert.AreEqual(2, step.Options.Count);
                var a = step.Options.First(o => o.ChoiceId == "a");
                var b = step.Options.First(o => o.ChoiceId == "b");
                Assert.AreEqual("Yes", a.ResolvedLabel);
                Assert.IsTrue(a.Available);
                Assert.IsFalse(b.Available, "the gated option is unavailable");
            }
            finally { Object.DestroyImmediate(gate); Object.DestroyImmediate(g); }
        }

        [Test]
        public void ResolveLine_TitleFallback_UsesAuthoredTitleWhenKeyMissing()
        {
            var line = new DialogueLineNodeData
            {
                Id = "l", NodeType = DialogueLineNodeData.NodeTypeId, Title = "Bonjour aventurier"
            };
            var ctx = new BaseContext();
            var emptyProvider = new CsvLocalizationProvider("Key,en\n", "en");   // no line_l key

            // opt-in: missing key falls back to the authored Title
            var withFallback = new DialoguePresenter(emptyProvider, titleFallback: true);
            Assert.AreEqual("Bonjour aventurier", withFallback.ResolveLine(line, ctx).ResolvedText);

            // default: no fallback (the bare #key marker, not the Title)
            var noFallback = new DialoguePresenter(emptyProvider);
            Assert.AreNotEqual("Bonjour aventurier", noFallback.ResolveLine(line, ctx).ResolvedText);
        }

        [Test]
        public void Resolve_NonDialogueNode_ReturnsNull()
        {
            var g = DialoguePlayerTestGraphs.Linear();
            try
            {
                var ctx = new BaseContext();
                var runner = new BaseRunner();
                runner.Start(g, ctx, DialogueExecutorRegistryFactory.Create());   // at the Start node
                var presenter = new DialoguePresenter(new CsvLocalizationProvider(DialoguePlayerTestGraphs.Csv, "en"));
                Assert.IsNull(presenter.Resolve(runner.CurrentNode, ctx), "a non-dialogue node resolves to null");
            }
            finally { Object.DestroyImmediate(g); }
        }
    }
}
