using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphCore.Tests
{
    /// <summary>BaseGraphEditorWindow.Refresh / OnRefresh: the central "reload dynamic data" hook (Save + ↻ button).</summary>
    [TestFixture]
    public class BaseGraphEditorWindowRefreshTests
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

        private class RefreshSpyWindow : BaseGraphEditorWindow
        {
            public int OnRefreshCount;
            protected override BaseGraphView CreateGraphView() => new StubGraphView();
            protected override void OnRefresh() => OnRefreshCount++;
        }

        private class MinimalWindow : BaseGraphEditorWindow
        {
            protected override BaseGraphView CreateGraphView() => new StubGraphView();
        }

        [Test]
        public void Refresh_InvokesOnRefresh()
        {
            var window = ScriptableObject.CreateInstance<RefreshSpyWindow>();
            try
            {
                int before = window.OnRefreshCount;
                window.Refresh();
                Assert.AreEqual(before + 1, window.OnRefreshCount, "Refresh() must invoke OnRefresh().");
            }
            finally { Object.DestroyImmediate(window); }
        }

        [Test]
        public void OnRefresh_DefaultImplementationIsNoOp()
        {
            var window = ScriptableObject.CreateInstance<MinimalWindow>();
            try
            {
                Assert.DoesNotThrow(() => window.Refresh(), "Default OnRefresh must be a safe no-op.");
            }
            finally { Object.DestroyImmediate(window); }
        }
    }
}
