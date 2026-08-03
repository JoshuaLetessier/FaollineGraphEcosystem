using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Faolline.GraphCore;
using Faolline.GraphGameFlow;

namespace Faolline.GraphGameFlow.Tests
{
    /// <summary>
    /// <see cref="DirectGraphCatalog"/> is the zero-dependency <see cref="IGraphCatalog"/> — proves the seam
    /// works with no asynchronous asset-loading technology installed at all (spec 047 FR-006/SC-006).
    /// </summary>
    public class DirectGraphCatalogTests
    {
        [Test]
        public void Resolve_RegisteredId_InvokesOnResolved_WithCorrectGraph()
        {
            var graph = ScriptableObject.CreateInstance<BaseGraph>();
            try
            {
                var catalog = new DirectGraphCatalog();
                catalog.Register("chapter-1", graph);

                BaseGraph resolved = null;
                bool failed = false;
                catalog.Resolve("chapter-1", g => resolved = g, _ => failed = true);

                Assert.AreSame(graph, resolved);
                Assert.IsFalse(failed);
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void Resolve_UnregisteredId_InvokesOnFailed_NeverOnResolved()
        {
            var catalog = new DirectGraphCatalog();
            LogAssert.Expect(LogType.Error, "[GraphGameFlow] DirectGraphCatalog has no graph registered for id 'ghost'.");

            BaseGraph resolved = null;
            string failReason = null;
            catalog.Resolve("ghost", g => resolved = g, r => failReason = r);

            Assert.IsNull(resolved, "an unregistered id must never call onResolved, not even with null.");
            Assert.IsNotNull(failReason);
        }

        [Test]
        public void Resolve_NullOrEmptyId_InvokesOnFailed()
        {
            var catalog = new DirectGraphCatalog();
            string failReason = null;

            catalog.Resolve(null, _ => Assert.Fail("must not resolve"), r => failReason = r);

            Assert.IsNotNull(failReason);
        }

        [Test]
        public void Unregister_RemovesMapping()
        {
            var graph = ScriptableObject.CreateInstance<BaseGraph>();
            try
            {
                var catalog = new DirectGraphCatalog();
                catalog.Register("chapter-1", graph);
                catalog.Unregister("chapter-1");
                LogAssert.Expect(LogType.Error, "[GraphGameFlow] DirectGraphCatalog has no graph registered for id 'chapter-1'.");

                bool failed = false;
                catalog.Resolve("chapter-1", _ => Assert.Fail("must not resolve after unregister"), _ => failed = true);

                Assert.IsTrue(failed);
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void Register_ReplacesExistingMapping()
        {
            var first = ScriptableObject.CreateInstance<BaseGraph>();
            var second = ScriptableObject.CreateInstance<BaseGraph>();
            try
            {
                var catalog = new DirectGraphCatalog();
                catalog.Register("chapter-1", first);
                catalog.Register("chapter-1", second);

                BaseGraph resolved = null;
                catalog.Resolve("chapter-1", g => resolved = g, null);

                Assert.AreSame(second, resolved);
            }
            finally { Object.DestroyImmediate(first); Object.DestroyImmediate(second); }
        }
    }
}
