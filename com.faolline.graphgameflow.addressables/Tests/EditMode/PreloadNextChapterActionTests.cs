using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Faolline.GraphCore;
using Faolline.GraphGameFlow;
using Faolline.GraphGameFlow.Addressables;

namespace Faolline.GraphGameFlow.Addressables.Tests
{
    /// <summary>
    /// Deterministic, EditMode-safe coverage: argument guards and default configuration that don't require
    /// Addressables content to be built. The real preload/signal/PendingNextGraph behaviour needs Addressables
    /// running with real content — out of scope for this EditMode-only suite, same split as
    /// <c>AddressablesSceneLoaderTests</c>/<c>AddressablesSceneLoaderPlayModeTests</c>.
    /// </summary>
    public class PreloadNextChapterActionTests
    {
        private PreloadNextChapterAction _action;

        [TearDown]
        public void TearDown() { if (_action != null) Object.DestroyImmediate(_action); }

        private PreloadNextChapterAction NewAction() => _action = ScriptableObject.CreateInstance<PreloadNextChapterAction>();

        [Test]
        public void DefaultConfiguration_IsEmpty()
        {
            var action = NewAction();
            Assert.IsNull(action.NextChapter);
            Assert.IsNull(action.CompletedSignal);
            Assert.IsNull(action.FailedSignal);
            Assert.IsNull(action.SignalDriver, "null by default — falls back to GraphFlowDriver.Active.");
        }

        [Test]
        public void SignalDriver_IsSettable()
        {
            var action = NewAction();
            var go = new GameObject("driver");
            try
            {
                var driver = go.AddComponent<GraphFlowDriver>();
                action.SignalDriver = driver;
                Assert.AreSame(driver, action.SignalDriver, "a graph built in code needs to target a specific driver, not just GraphFlowDriver.Active.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ReleaseNextChapter_NoNextChapter_DoesNotThrow()
        {
            var action = NewAction();
            Assert.DoesNotThrow(() => action.ReleaseNextChapter());
        }

        [Test]
        public void Execute_NoNextChapter_LogsErrorNoThrow()
        {
            var action = NewAction();
            LogAssert.Expect(LogType.Error, "[GraphGameFlow] PreloadNextChapterAction.Execute called with no valid NextChapter reference; ignored.");
            Assert.DoesNotThrow(() => action.Execute(new BaseContext()));
        }

        [Test]
        public void IsBaseAction()
        {
            Assert.IsInstanceOf<BaseAction>(NewAction());
        }
    }
}
