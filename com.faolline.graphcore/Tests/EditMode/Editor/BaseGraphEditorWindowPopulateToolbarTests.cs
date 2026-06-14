using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEditor.UIElements;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// Tests for BaseGraphEditorWindow.PopulateToolbar protected virtual hook.
    /// </summary>
    [TestFixture]
    public class BaseGraphEditorWindowPopulateToolbarTests
    {
        private class StubNodeData : BaseNodeData { }

        private class StubNodeView : BaseNodeView
        {
            public StubNodeView(BaseNodeData data) { Initialize(data); }
            protected override void OnBuildView() { }
        }

        private class StubGraphView : BaseGraphView
        {
            protected override BaseNodeView CreateNodeView(BaseNodeData node) => new StubNodeView(node);
            protected override BaseEdgeView CreateEdgeView(BaseEdgeData edge) => null;
        }

        private class WindowWithPopulateToolbarSpy : BaseGraphEditorWindow
        {
            public bool PopulateToolbarCalled;
            public bool PopulateToolbarRightCalled;
            public Toolbar ReceivedToolbar;
            public Toolbar ReceivedRightToolbar;

            protected override BaseGraphView CreateGraphView() => new StubGraphView();

            protected override void PopulateToolbar(Toolbar toolbar)
            {
                PopulateToolbarCalled = true;
                ReceivedToolbar = toolbar;
            }

            protected override void PopulateToolbarRight(Toolbar toolbar)
            {
                PopulateToolbarRightCalled = true;
                ReceivedRightToolbar = toolbar;
            }
        }

        [Test]
        public void PopulateToolbar_CalledDuringBuildToolbar()
        {
            // CreateInstance triggers OnEnable which calls BuildToolbar
            var window = ScriptableObject.CreateInstance<WindowWithPopulateToolbarSpy>();
            try
            {
                Assert.IsTrue(window.PopulateToolbarCalled,
                    "PopulateToolbar must be called during OnEnable/BuildToolbar");
            }
            finally { Object.DestroyImmediate(window); }
        }

        [Test]
        public void PopulateToolbar_ReceivesNonNullToolbar()
        {
            var window = ScriptableObject.CreateInstance<WindowWithPopulateToolbarSpy>();
            try
            {
                Assert.IsNotNull(window.ReceivedToolbar,
                    "PopulateToolbar must receive a non-null Toolbar instance");
            }
            finally { Object.DestroyImmediate(window); }
        }

        [Test]
        public void PopulateToolbarRight_CalledWithNonNullToolbar()
        {
            var window = ScriptableObject.CreateInstance<WindowWithPopulateToolbarSpy>();
            try
            {
                Assert.IsTrue(window.PopulateToolbarRightCalled,
                    "PopulateToolbarRight must be called during OnEnable/BuildToolbar");
                Assert.IsNotNull(window.ReceivedRightToolbar,
                    "PopulateToolbarRight must receive a non-null Toolbar instance");
            }
            finally { Object.DestroyImmediate(window); }
        }

        [Test]
        public void ToolbarSeparator_IsANonNullVisualElement()
        {
            Assert.IsNotNull(ToolbarSeparatorProbe.Make(), "ToolbarSeparator must produce a divider element.");
        }

        // Exposes the protected static helper for assertion.
        private class ToolbarSeparatorProbe : BaseGraphEditorWindow
        {
            protected override BaseGraphView CreateGraphView() => new StubGraphView();
            public static UnityEngine.UIElements.VisualElement Make() => ToolbarSeparator();
        }

        [Test]
        public void PopulateToolbar_DefaultImplementationIsNoOp()
        {
            // A window that does NOT override PopulateToolbar should compile and open without error
            var window = ScriptableObject.CreateInstance<MinimalWindow>();
            try
            {
                Assert.IsNotNull(window, "Window with default PopulateToolbar must open without exception");
            }
            finally { Object.DestroyImmediate(window); }
        }

        private class MinimalWindow : BaseGraphEditorWindow
        {
            protected override BaseGraphView CreateGraphView() => new StubGraphView();
        }
    }
}
