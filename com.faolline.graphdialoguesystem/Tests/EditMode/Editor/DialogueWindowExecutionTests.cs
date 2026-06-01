using Faolline.GraphLocalization;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphDialogue.Editor;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>EditMode tests: window playback session (Run / Choose / Continue).</summary>
    public class DialogueWindowExecutionTests
    {
        private DialogueGraphEditorWindow _window;

        [TearDown]
        public void TearDown() { if (_window != null) Object.DestroyImmediate(_window); }

        private static DialogueGraph Linear()
        {
            var g = ScriptableObject.CreateInstance<DialogueGraph>();
            g.AddNode(new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId });
            g.AddNode(new DialogueLineNodeData { Id = "l", NodeType = DialogueLineNodeData.NodeTypeId });
            g.AddNode(new EndNodeData { Id = "e", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed });
            g.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "l", PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "l", ToNodeId = "e", PortName = "out" });
            g.EntryNodeId = "s";
            return g;
        }

        private static DialogueGraph Choice()
        {
            var g = ScriptableObject.CreateInstance<DialogueGraph>();
            var c = new ChoiceNodeData { Id = "c", NodeType = ChoiceNodeData.NodeTypeId };
            c.Choices.Add(new DialogueChoice { Id = "a" });
            c.Choices.Add(new DialogueChoice { Id = "b" });
            g.AddNode(new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId });
            g.AddNode(c);
            g.AddNode(new EndNodeData { Id = "e1", NodeType = EndNodeData.NodeTypeId });
            g.AddNode(new EndNodeData { Id = "e2", NodeType = EndNodeData.NodeTypeId });
            g.AddEdge(new BaseEdgeData { Id = "es", FromNodeId = "s", ToNodeId = "c", PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = "ea", FromNodeId = "c", ToNodeId = "e1", PortName = "a" });
            g.AddEdge(new BaseEdgeData { Id = "eb", FromNodeId = "c", ToNodeId = "e2", PortName = "b" });
            g.EntryNodeId = "s";
            return g;
        }

        [Test]
        public void Run_Linear_PausesAtLine_ThenContinueEnds()
        {
            _window = ScriptableObject.CreateInstance<DialogueGraphEditorWindow>();
            var graph = Linear();
            try
            {
                _window.LoadGraphForTest(graph);
                _window.RunGraph();
                Assert.IsTrue(_window.HasActiveSession);
                Assert.AreEqual(RunnerState.NodeReady, _window.State, "Paused at the line.");

                _window.Continue();
                Assert.AreEqual(RunnerState.Ended, _window.State);
                Assert.IsInstanceOf<EndStep>(_window.CurrentStep);
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void Run_Choice_PausesAtChoice_ThenChooseEnds()
        {
            _window = ScriptableObject.CreateInstance<DialogueGraphEditorWindow>();
            var graph = Choice();
            try
            {
                _window.LoadGraphForTest(graph);
                _window.RunGraph();
                Assert.IsTrue(_window.IsWaitingForChoice, "Paused at the choice.");
                Assert.AreEqual(2, _window.AvailableChoices.Count);

                _window.Choose("a");
                Assert.IsFalse(_window.IsWaitingForChoice, "Choosing resumes execution.");
                Assert.AreEqual(RunnerState.Ended, _window.State);
            }
            finally { Object.DestroyImmediate(graph); }
        }
    }
}
