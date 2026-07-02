using Faolline.GraphCore;
using Faolline.GraphLocalization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>
    /// A router choice node (plain BaseChoice branches, no player-facing DialogueChoice) is auto-resolved by
    /// condition during Drain — it must never surface as an OnChoices prompt. #4 dogfood finding.
    /// </summary>
    public class DialoguePlayerRouterTests
    {
        private sealed class BoolGate : BaseCondition
        {
            public bool Open;
            public override bool Evaluate(BaseContext c) => Open;
        }

        private static DialoguePlayer NewPlayer(DialogueGraph g, BaseContext ctx) =>
            new DialoguePlayer(g, ctx, new CsvLocalizationProvider(DialoguePlayerTestGraphs.Csv, "en"));

        [Test]
        public void Router_AutoRoutesFirstPassingBranch_WithoutPrompting()
        {
            var condA = ScriptableObject.CreateInstance<BoolGate>(); condA.Open = true;   // take "a" → Completed
            var condB = ScriptableObject.CreateInstance<BoolGate>(); condB.Open = false;
            var g = DialoguePlayerTestGraphs.WithRouter(condA, condB);
            try
            {
                var player = NewPlayer(g, new BaseContext());

                bool choicesShown = false;
                EndStep ended = null;
                player.OnChoices += _ => choicesShown = true;
                player.OnEnded += e => ended = e;

                player.Start();

                Assert.IsFalse(choicesShown, "a router must never be shown as a player choice prompt");
                Assert.IsNotNull(ended, "playback should flow through the router to the end");
                Assert.AreEqual(EndReason.Completed, ended.EndReason, "the passing branch 'a' leads to the Completed end");
            }
            finally { Object.DestroyImmediate(condA); Object.DestroyImmediate(condB); Object.DestroyImmediate(g); }
        }

        [Test]
        public void Router_TakesOtherBranch_WhenOnlyItPasses()
        {
            var condA = ScriptableObject.CreateInstance<BoolGate>(); condA.Open = false;
            var condB = ScriptableObject.CreateInstance<BoolGate>(); condB.Open = true;   // take "b" → Cancelled
            var g = DialoguePlayerTestGraphs.WithRouter(condA, condB);
            try
            {
                var player = NewPlayer(g, new BaseContext());
                EndStep ended = null;
                player.OnEnded += e => ended = e;

                player.Start();

                Assert.IsNotNull(ended);
                Assert.AreEqual(EndReason.Cancelled, ended.EndReason, "only branch 'b' passes → Cancelled end");
            }
            finally { Object.DestroyImmediate(condA); Object.DestroyImmediate(condB); Object.DestroyImmediate(g); }
        }

        [Test]
        public void Router_NoBranchPasses_IsStuck_NotPrompted()
        {
            var condA = ScriptableObject.CreateInstance<BoolGate>(); condA.Open = false;
            var condB = ScriptableObject.CreateInstance<BoolGate>(); condB.Open = false;
            var g = DialoguePlayerTestGraphs.WithRouter(condA, condB);
            try
            {
                var player = NewPlayer(g, new BaseContext());
                bool stuck = false, choicesShown = false;
                player.OnStuck += () => stuck = true;
                player.OnChoices += _ => choicesShown = true;

                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"Router 'c' has no branch"));
                player.Start();

                Assert.IsTrue(stuck, "a dead router (no branch passes) is stuck");
                Assert.IsFalse(choicesShown, "a dead router still must not prompt");
            }
            finally { Object.DestroyImmediate(condA); Object.DestroyImmediate(condB); Object.DestroyImmediate(g); }
        }

        [Test]
        public void PlayerChoiceNode_StillPrompts_NotAutoRouted()
        {
            var g = DialoguePlayerTestGraphs.WithChoice();   // DialogueChoice options
            try
            {
                var player = NewPlayer(g, new BaseContext());
                ChoiceStep shown = null;
                player.OnChoices += s => shown = s;

                player.Start();

                Assert.IsNotNull(shown, "a real player-choice node (DialogueChoice) must still prompt");
                Assert.AreEqual(2, shown.Options.Count);
            }
            finally { Object.DestroyImmediate(g); }
        }
    }
}
