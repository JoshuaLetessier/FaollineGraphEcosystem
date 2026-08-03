using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Faolline.GraphCore;
using Faolline.GraphGameFlow;
using Faolline.GraphGameFlow.Addressables;

namespace Faolline.GraphGameFlow.Addressables.Tests
{
    /// <summary>
    /// Deterministic, EditMode-safe coverage: argument guards and interface compliance that don't require
    /// Addressables content to be built — mirrors <c>AddressablesSceneLoaderTests</c>'s scope split.
    /// </summary>
    public class AddressablesGraphCatalogTests
    {
        [Test]
        public void ImplementsIGraphCatalog()
        {
            Assert.IsInstanceOf<IGraphCatalog>(new AddressablesGraphCatalog());
        }

        [Test]
        public void Resolve_NullOrEmptyGraphId_InvokesOnFailed_LogsError()
        {
            var catalog = new AddressablesGraphCatalog();
            LogAssert.Expect(LogType.Error, "[GraphGameFlow] AddressablesGraphCatalog.Resolve called with a null or empty graphId.");

            BaseGraph resolved = null;
            string failReason = null;
            catalog.Resolve("", g => resolved = g, r => failReason = r);

            Assert.IsNull(resolved);
            Assert.IsNotNull(failReason);
        }
    }
}
