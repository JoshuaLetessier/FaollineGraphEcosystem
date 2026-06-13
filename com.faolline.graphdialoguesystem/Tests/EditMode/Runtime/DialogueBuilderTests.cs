using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>Fluent code-first dialogue building (DialogueGraphBuilder) + the table-less title provider.</summary>
    public class DialogueBuilderTests
    {
        private readonly List<Object> _created = new List<Object>();
        private DialogueGraph Track(DialogueGraph g) { _created.Add(g); return g; }

        [TearDown]
        public void Cleanup()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        [Test]
        public void Builds_Lines_Choices_Edges_AndEntry_WithCorrectNodeTypes()
        {
            var b = new DialogueGraphBuilder();
            var hi  = b.AddLine("guardian", "Bonjour").AsEntry();
            var hub = b.AddChoice();
            var ask = b.AddLine("guardian").Say("La ville est ancienne");
            var end = b.AddEnd();
            hi.To(hub);
            hub.Option("Demander").To(ask);
            hub.Option("Partir").To(end);
            ask.To(end);

            var g = Track(b.Build());

            var line = g.Nodes.OfType<DialogueLineNodeData>().First(n => n.Title == "Bonjour");
            Assert.AreEqual(g.EntryNodeId, line.Id, "the first line is the entry.");
            Assert.AreEqual(DialogueLineNodeData.NodeTypeId, line.NodeType, "#5: the builder sets NodeType.");
            Assert.AreEqual("guardian", line.SpeakerKey);

            var choice = g.Nodes.OfType<ChoiceNodeData>().Single();
            Assert.AreEqual(ChoiceNodeData.NodeTypeId, choice.NodeType);
            Assert.AreEqual(2, choice.Choices.Count, "two options were added.");
            Assert.IsTrue(choice.Choices.All(c => c is DialogueChoice), "options are DialogueChoices.");

            // Each option routes an edge from the choice keyed by the option's own id.
            foreach (var option in choice.Choices)
                Assert.IsTrue(g.Edges.Any(e => e.FromNodeId == choice.Id && e.PortName == option.Id),
                    "each option wires an edge keyed by its choice id.");
        }

        [Test]
        public void Option_When_SetsGatingCondition()
        {
            var cond = ScriptableObject.CreateInstance<DummyCondition>();
            try
            {
                var b = new DialogueGraphBuilder();
                var hub = b.AddChoice().AsEntry();
                var end = b.AddEnd();
                hub.Option("Gated").When(cond).To(end);

                var g = Track(b.Build());
                var choice = g.Nodes.OfType<ChoiceNodeData>().Single();
                Assert.AreSame(cond, choice.Choices[0].Condition);
            }
            finally { Object.DestroyImmediate(cond); }
        }

        [Test]
        public void TitleProvider_ResolvesAuthoredTitles_WithNoTable()
        {
            var b = new DialogueGraphBuilder();
            b.AddLine("g", "Hello there").AsEntry();
            var hub = b.AddChoice();
            hub.Option("Yes please");
            var g = Track(b.Build());

            var provider = DialogueTitleProvider.FromGraph(g);
            var line = g.Nodes.OfType<DialogueLineNodeData>().Single();
            var choice = g.Nodes.OfType<ChoiceNodeData>().Single().Choices[0];

            Assert.AreEqual("Hello there", provider.Resolve(DialogueLocalizationKeys.ForLine(line), "en"));
            Assert.AreEqual("Yes please", provider.Resolve(DialogueLocalizationKeys.ForChoice(choice), "en"));
            Assert.AreEqual("#unknown_key", provider.Resolve("unknown_key", "en"), "unknown key falls back to #key.");
        }

        private sealed class DummyCondition : BaseCondition
        {
            public override bool Evaluate(BaseContext context) => true;
        }
    }
}
