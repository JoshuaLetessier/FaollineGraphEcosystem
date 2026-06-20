using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// A selected `GraphLinkNodeData` gets a DEDICATED, minimal inspector via
    /// <see cref="BaseNodeInspectorView.AddGraphLinkSection"/>: a real object picker for the target graph
    /// (never a fragile string) plus a note — and NONE of the execution fields (no "Node Properties"),
    /// because a GraphLink is never executed.
    /// </summary>
    public class GraphLinkInspectorTests
    {
        private sealed class FakeGraph : BaseGraph { }

        // A minimal concrete inspector mirroring the lib pattern: a GraphLink takes the dedicated panel and
        // returns early; any other node would get the (here stubbed) execution "Node Properties" section.
        private sealed class StubInspector : BaseNodeInspectorView
        {
            public override void BindNode(BaseNodeData node)
            {
                if (node == null) { ClearInspector(); return; }
                Clear();
                BoundNode = node;
                if (node is GraphLinkNodeData link) { AddGraphLinkSection(link, MarkGraphDirty); return; }
                Add(new Foldout { text = "Node Properties" }); // stand-in for the execution fields
            }

            public override void ClearInspector() { Clear(); BoundNode = null; }
        }

        private static ObjectField TargetPicker(VisualElement root) =>
            root.Query<ObjectField>().ToList().FirstOrDefault(f => f.label == "Target Graph");

        [Test]
        public void GraphLink_ShowsTargetPicker_AndNoExecutionFields()
        {
            var inspector = new StubInspector();
            var node = new GraphLinkNodeData { Id = "link", NodeType = GraphLinkNodeData.NodeTypeId };

            inspector.BindNode(node);

            var picker = TargetPicker(inspector);
            Assert.IsNotNull(picker, "the GraphLink inspector shows a 'Target Graph' object picker.");
            Assert.AreEqual(typeof(BaseGraph), picker.objectType, "the picker is a real BaseGraph ObjectField, not a string.");
            Assert.IsNull(inspector.Query<Foldout>().ToList().FirstOrDefault(f => f.text == "Node Properties"),
                "a GraphLink is never executed, so its inspector omits the execution 'Node Properties' section.");
        }

        [Test]
        public void GraphLink_Picker_ReflectsExistingTarget()
        {
            var inspector = new StubInspector();
            var target = ScriptableObject.CreateInstance<FakeGraph>();
            var node = new GraphLinkNodeData { Id = "link", NodeType = GraphLinkNodeData.NodeTypeId, TargetGraph = target };
            try
            {
                inspector.BindNode(node);
                var picker = TargetPicker(inspector);
                Assert.IsNotNull(picker);
                Assert.AreSame(target, picker.value, "the object picker shows the link's current target graph.");
            }
            finally { Object.DestroyImmediate(target); }
        }

        [Test]
        public void NonGraphLink_StillGetsExecutionSection()
        {
            var inspector = new StubInspector();
            inspector.BindNode(new EndNodeData { Id = "end", NodeType = "graphcore/end" });

            Assert.IsNotNull(inspector.Query<Foldout>().ToList().FirstOrDefault(f => f.text == "Node Properties"),
                "a normal (executable) node still shows its execution fields — the trim is GraphLink-only.");
        }
    }
}
