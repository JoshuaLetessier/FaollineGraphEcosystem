using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.Tests
{
    [TestFixture]
    public class DialogueBusTests
    {
        [TearDown]
        public void TearDown() => DialogueBus.Stop();

        [Test]
        public void Play_FiresOnDialogueStarted()
        {
            var graph = DialoguePlayerTestGraphs.Linear();
            try
            {
                DialogueGraph started = null;
                DialogueBus.OnDialogueStarted += g => started = g;
                DialogueBus.Play(graph, new DialogueContext(), titleFallback: true);

                Assert.AreSame(graph, started);
                Assert.IsTrue(DialogueBus.IsPlaying);

                DialogueBus.OnDialogueStarted -= g => started = g;
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void Play_RelaysOnLine()
        {
            var graph = DialoguePlayerTestGraphs.Linear();
            try
            {
                LineStep line = null;
                DialogueBus.OnLine += s => line = s;
                DialogueBus.Play(graph, new DialogueContext(), titleFallback: true);

                Assert.IsNotNull(line, "OnLine should relay from the player.");
                Assert.AreEqual("l", line.NodeId);

                DialogueBus.OnLine -= s => line = s;
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void Advance_RoutesToActivePlayer()
        {
            var graph = DialoguePlayerTestGraphs.Linear();
            try
            {
                EndStep end = null;
                DialogueBus.OnEnded += s => end = s;
                DialogueBus.Play(graph, new DialogueContext(), titleFallback: true);

                Assert.IsTrue(DialogueBus.IsPlaying);
                DialogueBus.Advance();

                Assert.IsNotNull(end, "Advancing past the line should end the dialogue.");
                Assert.IsFalse(DialogueBus.IsPlaying);

                DialogueBus.OnEnded -= s => end = s;
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void Play_RelaysOnChoices_AndChooseRoutes()
        {
            var graph = DialoguePlayerTestGraphs.WithChoice();
            try
            {
                ChoiceStep choices = null;
                EndStep end = null;
                DialogueBus.OnChoices += s => choices = s;
                DialogueBus.OnEnded += s => end = s;
                DialogueBus.Play(graph, new DialogueContext(), titleFallback: true);

                Assert.IsNotNull(choices, "OnChoices should relay.");
                DialogueBus.Choose("a");

                Assert.IsNotNull(end);
                Assert.AreEqual(EndReason.Completed, end.EndReason);

                DialogueBus.OnChoices -= s => choices = s;
                DialogueBus.OnEnded -= s => end = s;
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void OnEnded_ClearsActivePlayer()
        {
            var graph = DialoguePlayerTestGraphs.Linear();
            try
            {
                DialogueBus.Play(graph, new DialogueContext(), titleFallback: true);
                Assert.IsNotNull(DialogueBus.ActivePlayer);

                DialogueBus.Advance();
                Assert.IsNull(DialogueBus.ActivePlayer);
                Assert.IsFalse(DialogueBus.IsPlaying);
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void Play_WhilePlaying_StopsPreviousAndStartsNew()
        {
            var g1 = DialoguePlayerTestGraphs.Linear();
            var g2 = DialoguePlayerTestGraphs.Linear();
            try
            {
                int startCount = 0;
                DialogueBus.OnDialogueStarted += _ => startCount++;

                DialogueBus.Play(g1, new DialogueContext(), titleFallback: true);
                Assert.AreEqual(1, startCount);

                DialogueBus.Play(g2, new DialogueContext(), titleFallback: true);
                Assert.AreEqual(2, startCount);
                Assert.IsTrue(DialogueBus.IsPlaying);

                DialogueBus.OnDialogueStarted -= _ => startCount++;
            }
            finally { Object.DestroyImmediate(g1); Object.DestroyImmediate(g2); }
        }
    }
}
