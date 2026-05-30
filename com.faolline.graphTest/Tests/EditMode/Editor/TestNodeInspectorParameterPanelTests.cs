using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphTest.Editor;

namespace Faolline.GraphTest.Tests
{
    [TestFixture]
    public class TestNodeInspectorParameterPanelTests
    {
        private TestNodeInspectorView _inspector;
        private TestGraph _graph;

        [SetUp]
        public void SetUp()
        {
            _inspector = new TestNodeInspectorView();
            _graph = ScriptableObject.CreateInstance<TestGraph>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_graph);
        }

        [Test]
        public void ClearInspector_WithoutGraph_ShowsNoChildren()
        {
            _inspector.ClearInspector();
            Assert.AreEqual(0, _inspector.childCount,
                "ClearInspector without a loaded graph must show no children");
        }

        [Test]
        public void ClearInspector_WithGraph_ShowsParameterPanel()
        {
            _inspector.SetGraph(_graph);
            _inspector.ClearInspector();

            Assert.Greater(_inspector.childCount, 0,
                "ClearInspector with a loaded graph must render a parameter panel (at least one child element)");
        }

        [Test]
        public void ParameterPanel_AddBoolParam_AppearsInGraphParameters()
        {
            _inspector.SetGraph(_graph);

            // Simulate adding a bool parameter programmatically (same effect as clicking the button)
            _inspector.AddBoolParameter("door_open", false);

            Assert.AreEqual(1, _graph.Parameters.Count,
                "AddBoolParameter must add one ParameterData entry to the graph");
            Assert.AreEqual("door_open", _graph.Parameters[0].Key);
            Assert.AreEqual(ParameterType.Bool, _graph.Parameters[0].Type);
        }

        [Test]
        public void ParameterPanel_RemoveBoolParam_RemovesFromGraphParameters()
        {
            _inspector.SetGraph(_graph);
            _inspector.AddBoolParameter("door_open", false);
            Assert.AreEqual(1, _graph.Parameters.Count);

            _inspector.RemoveBoolParameter("door_open");

            Assert.AreEqual(0, _graph.Parameters.Count,
                "RemoveBoolParameter must remove the entry from the graph");
        }
    }
}
