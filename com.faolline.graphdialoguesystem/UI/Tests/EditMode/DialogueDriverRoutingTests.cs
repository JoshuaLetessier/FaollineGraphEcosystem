using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;
using Faolline.GraphCore;
using Faolline.GraphDialogue;
using Faolline.GraphDialogue.UI;
using Faolline.GraphLocalization;

namespace Faolline.GraphDialogue.UI.Tests
{
    /// <summary>
    /// EditMode tests for DialogueDriver: player steps route to the view, choices route by id,
    /// advance is gated during choices, and a null view is tolerated.
    /// </summary>
    public class DialogueDriverRoutingTests
    {
        // Start → Line "l" → Choice [a → Line "la" → End, b → Line "lb" → End]
        private const string Csv =
            "Key,en\n" +
            "line_l,Hello\n" +
            "line_la,AfterA\n" +
            "line_lb,AfterB\n" +
            "speaker_npc,NPC\n";

        private static DialogueGraph BuildGraph()
        {
            var g = ScriptableObject.CreateInstance<DialogueGraph>();
            var s  = new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId };
            var l  = new DialogueLineNodeData { Id = "l", NodeType = DialogueLineNodeData.NodeTypeId, SpeakerKey = "npc" };
            var c  = new ChoiceNodeData { Id = "c", NodeType = ChoiceNodeData.NodeTypeId };
            c.Choices.Add(new DialogueChoice { Id = "a", Title = "Option A" });
            c.Choices.Add(new DialogueChoice { Id = "b", Title = "Option B" });
            var la = new DialogueLineNodeData { Id = "la", NodeType = DialogueLineNodeData.NodeTypeId };
            var lb = new DialogueLineNodeData { Id = "lb", NodeType = DialogueLineNodeData.NodeTypeId };
            var ea = new EndNodeData { Id = "ea", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };
            var eb = new EndNodeData { Id = "eb", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };

            g.AddNode(s); g.AddNode(l); g.AddNode(c); g.AddNode(la); g.AddNode(lb); g.AddNode(ea); g.AddNode(eb);
            g.EntryNodeId = "s";
            g.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s",  ToNodeId = "l",  PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "l",  ToNodeId = "c",  PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = "e3", FromNodeId = "c",  ToNodeId = "la", PortName = "a" });
            g.AddEdge(new BaseEdgeData { Id = "e4", FromNodeId = "c",  ToNodeId = "lb", PortName = "b" });
            g.AddEdge(new BaseEdgeData { Id = "e5", FromNodeId = "la", ToNodeId = "ea", PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = "e6", FromNodeId = "lb", ToNodeId = "eb", PortName = "out" });
            return g;
        }

        private static DialogueDriver NewDriver(out RecordingDialogueView view, out DialogueGraph graph)
        {
            graph = BuildGraph();
            view = new RecordingDialogueView();
            var go = new GameObject("driver");
            var driver = go.AddComponent<DialogueDriver>();
            driver.View = view;
            driver.Provider = new CsvLocalizationProvider(Csv, "en");
            return driver;
        }

        private static void Cleanup(DialogueDriver driver, DialogueGraph graph)
        {
            if (driver != null) Object.DestroyImmediate(driver.gameObject);
            if (graph != null) Object.DestroyImmediate(graph);
        }

        [Test]
        public void Start_EmitsFirstLine_ToView()
        {
            var driver = NewDriver(out var view, out var graph);
            try
            {
                driver.StartDialogue(graph);
                Assert.IsNotNull(view.LastLine, "First line must route to the view.");
                Assert.AreEqual("Hello", view.LastLine.ResolvedText);
            }
            finally { Cleanup(driver, graph); }
        }

        [Test]
        public void Advance_FromLine_ShowsChoices()
        {
            var driver = NewDriver(out var view, out var graph);
            try
            {
                driver.StartDialogue(graph);
                driver.Advance();
                Assert.IsNotNull(view.LastChoices, "Advancing the line must reach the choices.");
                Assert.AreEqual(2, view.LastChoices.Options.Count);
            }
            finally { Cleanup(driver, graph); }
        }

        [Test]
        public void ChoiceSelected_RoutesByChoiceId()
        {
            var driver = NewDriver(out var view, out var graph);
            try
            {
                driver.StartDialogue(graph);
                driver.Advance();                 // → choices
                view.RaiseChoiceSelected("a");     // pick option A
                Assert.IsNotNull(view.LastLine, "Choosing A routes to its branch line.");
                Assert.AreEqual("AfterA", view.LastLine.ResolvedText);
            }
            finally { Cleanup(driver, graph); }
        }

        [Test]
        public void Advance_IsIgnored_DuringChoices()
        {
            var driver = NewDriver(out var view, out var graph);
            try
            {
                driver.StartDialogue(graph);
                driver.Advance();                  // → choices
                var choicesBefore = view.ShowChoicesCount;
                driver.Advance();                  // should be ignored
                Assert.IsNull(view.LastLine, "Advance during choices must not produce a line.");
                Assert.AreEqual(choicesBefore, view.ShowChoicesCount, "No extra step from advancing during choices.");
            }
            finally { Cleanup(driver, graph); }
        }

        [Test]
        public void ChooseA_ThenAdvance_ReachesEnd_HidesView()
        {
            var driver = NewDriver(out var view, out var graph);
            try
            {
                driver.StartDialogue(graph);
                driver.Advance();                  // choices
                view.RaiseChoiceSelected("a");     // → line la
                driver.Advance();                  // la → end
                Assert.AreEqual(1, view.HideAllCount, "Reaching the end hides the view.");
            }
            finally { Cleanup(driver, graph); }
        }

        [Test]
        public void NullView_RunsLogicAndWarns()
        {
            var graph = BuildGraph();
            var go = new GameObject("driver");
            var driver = go.AddComponent<DialogueDriver>();
            driver.Provider = new CsvLocalizationProvider(Csv, "en");
            try
            {
                LogAssert.Expect(LogType.Warning, new Regex("no IDialogueView"));
                Assert.DoesNotThrow(() => driver.StartDialogue(graph));
            }
            finally { Cleanup(driver, graph); }
        }
    }
}
