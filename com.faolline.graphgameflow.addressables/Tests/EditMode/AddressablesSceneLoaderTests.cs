using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Faolline.GraphGameFlow;
using Faolline.GraphGameFlow.Addressables;

namespace Faolline.GraphGameFlow.Addressables.Tests
{
    /// <summary>
    /// Deterministic, EditMode-safe coverage: argument guards and interface compliance that don't require
    /// Addressables to be initialised (no content, no Play Mode). The real load/unload/queue/gate behaviour
    /// — which DOES need Addressables running — is covered in PlayMode
    /// (<c>AddressablesSceneLoaderPlayModeTests</c>), mirroring the EditMode/PlayMode split already used for
    /// <c>AsyncSceneLoader</c>.
    /// </summary>
    public class AddressablesSceneLoaderTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown() { if (_go) Object.DestroyImmediate(_go); }

        private AddressablesSceneLoader NewLoader()
        {
            _go = new GameObject("addressables-loader");
            return _go.AddComponent<AddressablesSceneLoader>();
        }

        [Test]
        public void ImplementsISceneLoader_AndISceneUnloader()
        {
            var loader = NewLoader();
            Assert.IsInstanceOf<ISceneLoader>(loader);
            Assert.IsInstanceOf<ISceneUnloader>(loader);
        }

        [Test]
        public void LoadScene_EmptyKey_LogsErrorNoThrow()
        {
            var loader = NewLoader();
            LogAssert.Expect(LogType.Error, "[GraphGameFlow] AddressablesSceneLoader.LoadScene called with a null or empty key; ignored.");
            Assert.DoesNotThrow(() => loader.LoadScene("", LoadSceneMode.Single));
            Assert.IsFalse(loader.IsLoading, "an invalid request must never start the pump.");
        }

        [Test]
        public void LoadScene_NullKey_LogsErrorNoThrow()
        {
            var loader = NewLoader();
            LogAssert.Expect(LogType.Error, "[GraphGameFlow] AddressablesSceneLoader.LoadScene called with a null or empty key; ignored.");
            Assert.DoesNotThrow(() => loader.LoadScene(null, LoadSceneMode.Additive));
        }

        [Test]
        public void UnloadScene_EmptyKey_LogsErrorNoThrow()
        {
            var loader = NewLoader();
            LogAssert.Expect(LogType.Error, "[GraphGameFlow] AddressablesSceneLoader.UnloadScene called with a null or empty key; ignored.");
            Assert.DoesNotThrow(() => loader.UnloadScene(""));
            Assert.IsFalse(loader.IsLoading);
        }

        [Test]
        public void ActivateReadyScene_NoPendingLoad_WarnsNoThrow()
        {
            var loader = NewLoader();
            LogAssert.Expect(LogType.Warning, "[GraphGameFlow] AddressablesSceneLoader.ActivateReadyScene called with no scene ready to activate; ignored.");
            Assert.DoesNotThrow(() => loader.ActivateReadyScene());
        }

        [Test]
        public void DefaultConfiguration_MatchesAsyncSceneLoaderConventions()
        {
            // Same defaults as AsyncSceneLoader by design, so swapping loaders needs no reconfiguration.
            var loader = NewLoader();
            Assert.IsTrue(loader.AutoActivate);
            Assert.AreEqual(0f, loader.MinimumDisplayDuration);
            Assert.IsFalse(loader.PauseDriverWhileLoading);
            Assert.IsNull(loader.LoadCompletedSignal);
            Assert.IsNull(loader.UnloadCompletedSignal);
            Assert.IsNull(loader.SignalDriver);
            Assert.IsFalse(loader.IsLoading);
            Assert.AreEqual(0, loader.PendingCount);
        }
    }
}
