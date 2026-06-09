using NUnit.Framework;
using Faolline.GraphCore;
using Faolline.GraphGameFlow;

namespace Faolline.GraphGameFlow.Tests
{
    /// <summary>
    /// Constitution VI: a typed context subclass must clone to its own type and carry its domain fields, or
    /// history snapshot restore silently breaks. For slice 1 the domain field is the scene loader service.
    /// </summary>
    public class GameFlowContextTests
    {
        [Test]
        public void DeepClone_ReturnsGameFlowContext_CarryingSceneLoaderAndValues()
        {
            var ctx = new GameFlowContext();
            var stub = new StubSceneLoader();
            ctx.SceneLoader = stub;
            ctx.Set<int>("hp", 5);

            var clone = ctx.DeepClone();

            Assert.IsInstanceOf<GameFlowContext>(clone, "clone must be the subclass, not a bare BaseContext.");
            Assert.AreSame(stub, ((GameFlowContext)clone).SceneLoader, "the loader reference must carry through.");
            Assert.AreEqual(5, clone.Get<int>("hp"), "base parameter values must carry through.");
        }

        [Test]
        public void DeepClone_IsIndependentForValues()
        {
            var ctx = new GameFlowContext();
            ctx.Set<int>("hp", 1);
            var clone = ctx.DeepClone();

            clone.Set<int>("hp", 99);

            Assert.AreEqual(1, ctx.Get<int>("hp"), "mutating the clone must not affect the source.");
        }
    }
}
