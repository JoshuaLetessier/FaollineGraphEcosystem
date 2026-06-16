using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;
using Faolline.GraphQuest.Editor;

namespace Faolline.GraphQuest.Tests
{
    /// <summary>The visual quest editor: node-view dispatch, inspector binding, and the window opening.</summary>
    public sealed class QuestEditorTests : QuestTestBase
    {
        // Exposes the protected CreateNodeView for assertion.
        private sealed class ProbeGraphView : QuestGraphView
        {
            public BaseNodeView Make(BaseNodeData node) => CreateNodeView(node);
        }

        [Test]
        public void GraphView_DispatchesObjective_ToObjectiveNodeView()
        {
            var probe = new ProbeGraphView();
            var view = probe.Make(new ObjectiveNodeData { Id = "a", NodeType = ObjectiveNodeData.NodeTypeId });
            Assert.IsInstanceOf<ObjectiveNodeView>(view);
        }

        [Test]
        public void GraphView_ReturnsNull_ForUnknownNodeType()
        {
            var probe = new ProbeGraphView();
            Assert.IsNull(probe.Make(new ObjectiveNodeData { Id = "x", NodeType = "something.else" }));
        }

        [Test]
        public void Inspector_BindsObjectiveAndQuest_WithoutError()
        {
            var quest = TrackGraph(QuestBuilder.Create("q").Named("My Quest")
                .AddObjective("a").Named("Find it").Describe("Look around").CompleteWhen(Flag("a"))
                .Build());

            var inspector = new QuestNodeInspectorView();
            inspector.SetGraph(quest);

            Assert.DoesNotThrow(() => inspector.ClearInspector(), "no-selection ⇒ quest section + params");
            Assert.DoesNotThrow(() => inspector.BindNode(quest.Nodes[0]), "objective section binds");
            Assert.DoesNotThrow(() => inspector.BindNode(null), "null ⇒ back to no-selection");
        }

        [Test]
        public void EntryAndTerminal_AreDerivedFromTheTopology()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .AddObjective("a").CompleteWhen(Flag("a"))
                .AddObjective("b").Requires("a").CompleteWhen(Flag("b"))
                .AddObjective("c").Requires("b").CompleteWhen(Flag("c"))
                .AddObjective("side").CompleteWhen(Flag("s"))    // standalone ⇒ entry AND terminal
                .Build());

            // a: entry (no prerequisite), not terminal (b depends on it)
            Assert.IsFalse(QuestGraphView.HasPrerequisite(quest, "a"), "a is an entry");
            Assert.IsTrue(QuestGraphView.HasDependent(quest, "a"), "a is not terminal");
            // c: not entry (requires b), terminal (nothing depends on it)
            Assert.IsTrue(QuestGraphView.HasPrerequisite(quest, "c"));
            Assert.IsFalse(QuestGraphView.HasDependent(quest, "c"), "c is terminal");
            // side: both entry and terminal
            Assert.IsFalse(QuestGraphView.HasPrerequisite(quest, "side"));
            Assert.IsFalse(QuestGraphView.HasDependent(quest, "side"));
        }

        [Test]
        public void Window_Opens_AndLoadsAQuest_WithoutError()
        {
            var quest = TrackGraph(QuestBuilder.Create("q")
                .AddObjective("a").CompleteWhen(Flag("a")).Build());

            var window = ScriptableObject.CreateInstance<QuestGraphEditorWindow>();
            try
            {
                Assert.DoesNotThrow(() => window.LoadGraphForTest(quest));
            }
            finally { Object.DestroyImmediate(window); }
        }
    }
}
