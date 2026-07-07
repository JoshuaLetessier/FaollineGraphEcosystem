using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.Tests
{
    [TestFixture]
    public class PlayDialogueActionTests
    {
        [TearDown]
        public void TearDown() => DialogueBus.Stop();

        [Test]
        public void Execute_StartsDialogueAndRaisesSignalOnEnd()
        {
            var graph = DialoguePlayerTestGraphs.Linear();
            var action = ScriptableObject.CreateInstance<PlayDialogueAction>();
            action.DialogueGraph = graph;
            action.SignalDef = "test_done";
            try
            {
                var ctx = new BaseContext();
                string raisedSignal = null;
                ctx.OnSignal("test_done", args => raisedSignal = args.Name);

                action.Execute(ctx);
                Assert.IsTrue(DialogueBus.IsPlaying);

                DialogueBus.Advance();
                Assert.AreEqual("test_done", raisedSignal);
                Assert.IsFalse(DialogueBus.IsPlaying);
            }
            finally { Object.DestroyImmediate(action); Object.DestroyImmediate(graph); }
        }

        [Test]
        public void Execute_NullGraph_LogsWarningAndDoesNotPark()
        {
            var action = ScriptableObject.CreateInstance<PlayDialogueAction>();
            action.DialogueGraph = null;
            try
            {
                var ctx = new BaseContext();
                UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
                    "[GraphDialogue] PlayDialogueAction: no dialogue graph assigned; skipping.");
                action.Execute(ctx);

                Assert.IsFalse(DialogueBus.IsPlaying);
            }
            finally { Object.DestroyImmediate(action); }
        }

        [Test]
        public void Execute_EmptySignalName_AutoDerivesFromGraphId()
        {
            var graph = DialoguePlayerTestGraphs.Linear();
            var action = ScriptableObject.CreateInstance<PlayDialogueAction>();
            action.DialogueGraph = graph;
            action.SignalDef = "";
            try
            {
                var ctx = new BaseContext();
                var expectedSignal = "dialogue_done_" + graph.GraphId;
                string raisedSignal = null;
                ctx.OnSignal(expectedSignal, args => raisedSignal = args.Name);

                action.Execute(ctx);
                DialogueBus.Advance();

                Assert.AreEqual(expectedSignal, raisedSignal);
            }
            finally { Object.DestroyImmediate(action); Object.DestroyImmediate(graph); }
        }
    }
}
