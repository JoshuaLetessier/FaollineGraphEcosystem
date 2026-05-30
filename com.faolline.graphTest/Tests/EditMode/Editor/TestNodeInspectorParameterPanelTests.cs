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

        [Test]
        public void ParameterPanel_AddIntParam_StoresTypeAndDefault()
        {
            _inspector.SetGraph(_graph);

            _inspector.AddParameter("score", ParameterType.Int, "5");

            Assert.AreEqual(1, _graph.Parameters.Count);
            Assert.AreEqual("score", _graph.Parameters[0].Key);
            Assert.AreEqual(ParameterType.Int, _graph.Parameters[0].Type);
            Assert.AreEqual("5", _graph.Parameters[0].DefaultValue);
        }

        [Test]
        public void ParameterPanel_TypedParams_SurviveSerializationRoundTrip()
        {
            _inspector.SetGraph(_graph);
            _inspector.AddParameter("score", ParameterType.Int, "5");
            _inspector.AddParameter("hp", ParameterType.Float, "0.5");
            _inspector.AddParameter("name", ParameterType.String, "hero");

            var clone = Object.Instantiate(_graph);
            try
            {
                Assert.AreEqual(3, clone.Parameters.Count);
                Assert.AreEqual(ParameterType.Int,    clone.Parameters[0].Type);
                Assert.AreEqual("5",                  clone.Parameters[0].DefaultValue);
                Assert.AreEqual(ParameterType.Float,  clone.Parameters[1].Type);
                Assert.AreEqual("0.5",                clone.Parameters[1].DefaultValue);
                Assert.AreEqual(ParameterType.String, clone.Parameters[2].Type);
                Assert.AreEqual("hero",               clone.Parameters[2].DefaultValue);
            }
            finally { Object.DestroyImmediate(clone); }
        }

        [Test]
        public void RemoveParameter_RemovesNonBoolParam()
        {
            _inspector.SetGraph(_graph);
            _inspector.AddParameter("score", ParameterType.Int, "5");

            _inspector.RemoveParameter("score");

            Assert.AreEqual(0, _graph.Parameters.Count,
                "RemoveParameter must remove a parameter of any type");
        }
    }
}
